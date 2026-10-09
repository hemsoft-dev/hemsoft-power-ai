// <copyright file="RedisAgentTaskBroker.cs" company="HemSoft">
// Copyright © 2025 HemSoft
// </copyright>

namespace HemSoft.PowerAI.Common.Services;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using HemSoft.PowerAI.Common.Models;

using StackExchange.Redis;

/// <summary>
/// Redis-based implementation of the agent task broker.
/// Uses Redis pub/sub for real-time task and result delivery.
/// For large result payloads, stores data in Redis keys and passes a reference through pub/sub.
/// </summary>
/// <remarks>
/// Excluded from code coverage because it requires actual Redis infrastructure.
/// Constructor argument validation is tested; Redis operations require integration tests.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Requires Redis infrastructure for integration testing.")]
public sealed class RedisAgentTaskBroker : IAgentTaskBroker, IAsyncDisposable
{
    /// <summary>
    /// Threshold in bytes above which result data is stored externally instead of inline in pub/sub messages.
    /// Default is 64KB to provide safety margin below Redis pub/sub limits.
    /// </summary>
    public const int LargeResultThreshold = 64 * 1024;

    private const string TasksChannel = "agents:tasks";
    private const string ResultsChannelPrefix = "agents:results:";
    private const string ProgressChannelPrefix = "agents:progress:";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly ConnectionMultiplexer connection;
    private readonly ISubscriber subscriber;
    private readonly IResultStorageService? resultStorage;
    private readonly bool ownsResultStorage;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisAgentTaskBroker"/> class.
    /// </summary>
    /// <param name="connectionString">Redis connection string.</param>
    public RedisAgentTaskBroker(string connectionString)
        : this(connectionString, resultStorage: null, ownsResultStorage: false)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisAgentTaskBroker"/> class
    /// with result storage for large payloads. The broker will own and dispose the storage service.
    /// </summary>
    /// <param name="connectionString">Redis connection string.</param>
    /// <param name="resultStorage">Storage service for large result payloads.</param>
    public RedisAgentTaskBroker(string connectionString, IResultStorageService resultStorage)
        : this(connectionString, resultStorage, ownsResultStorage: true)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisAgentTaskBroker"/> class
    /// with result storage for large payloads.
    /// </summary>
    /// <param name="connectionString">Redis connection string.</param>
    /// <param name="resultStorage">Optional storage service for large result payloads.</param>
    /// <param name="ownsResultStorage">If true, the broker will dispose the storage service when disposed.</param>
    public RedisAgentTaskBroker(
        string connectionString,
        IResultStorageService? resultStorage,
        bool ownsResultStorage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        this.connection = ConnectionMultiplexer.Connect(connectionString);
        this.subscriber = this.connection.GetSubscriber();
        this.resultStorage = resultStorage;
        this.ownsResultStorage = ownsResultStorage && resultStorage is not null;
    }

    /// <summary>
    /// Creates a new broker with built-in result storage for large payloads.
    /// The broker manages the lifetime of the storage service.
    /// </summary>
    /// <param name="connectionString">Redis connection string.</param>
    /// <returns>A new broker instance with result storage.</returns>
    public static RedisAgentTaskBroker CreateWithStorage(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        RedisResultStorageService? storage = null;

        try
        {
            storage = new RedisResultStorageService(connectionString);
            var broker = new RedisAgentTaskBroker(connectionString, storage, ownsResultStorage: true);

            // Ownership transferred to broker, prevent double-dispose
            storage = null;
            return broker;
        }
        finally
        {
            // Dispose only if ownership was not transferred
            storage?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    /// <inheritdoc/>
    public Task SubmitTaskAsync(AgentTaskRequest request) =>
        this.SubmitTaskAsync(request, CancellationToken.None);

    /// <inheritdoc/>
    public Task SubmitTaskAsync(AgentTaskRequest request, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        ArgumentNullException.ThrowIfNull(request);

        return this.SubmitTaskCoreAsync(request);
    }

    /// <inheritdoc/>
    public Task SubscribeToTasksAsync(
        Func<AgentTaskRequest, CancellationToken, Task> handler,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        ArgumentNullException.ThrowIfNull(handler);

        return this.SubscribeToTasksCoreAsync(handler, cancellationToken);
    }

    /// <inheritdoc/>
    public Task PublishResultAsync(AgentTaskResult result) =>
        this.PublishResultAsync(result, CancellationToken.None);

    /// <inheritdoc/>
    public Task PublishResultAsync(AgentTaskResult result, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        ArgumentNullException.ThrowIfNull(result);

        return this.PublishResultCoreAsync(result);
    }

    /// <inheritdoc/>
    public Task SubscribeToResultAsync(
        string taskId,
        Action<AgentTaskResult> handler,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        ArgumentNullException.ThrowIfNull(handler);

        return this.SubscribeToResultCoreAsync(taskId, handler, cancellationToken);
    }

    /// <inheritdoc/>
    public Task PublishProgressAsync(AgentTaskProgress progress, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        ArgumentNullException.ThrowIfNull(progress);

        return this.PublishProgressCoreAsync(progress);
    }

    /// <inheritdoc/>
    public Task SubscribeToProgressAsync(
        string taskId,
        Action<AgentTaskProgress> handler,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        ArgumentNullException.ThrowIfNull(handler);

        return this.SubscribeToProgressCoreAsync(taskId, handler, cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;

        // Dispose owned storage service first
        if (this.ownsResultStorage && this.resultStorage is IAsyncDisposable disposableStorage)
        {
            await disposableStorage.DisposeAsync().ConfigureAwait(false);
        }

        await this.connection.CloseAsync().ConfigureAwait(false);
        await this.connection.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a multiplexer-owned queue registration independently of its cancellable reader lifetime.
    /// Callers must finish reading and unsubscribe their queue before returning.
    /// </summary>
    /// <param name="channel">The channel to register.</param>
    /// <returns>The queue owning this subscription.</returns>
    private Task<ChannelMessageQueue> SubscribeChannelAsync(RedisChannel channel) =>
        this.subscriber.SubscribeAsync(channel);

    private async Task SubmitTaskCoreAsync(AgentTaskRequest request)
    {
        var json = JsonSerializer.Serialize(request, SerializerOptions);
        _ = await this.subscriber.PublishAsync(RedisChannel.Literal(TasksChannel), json).ConfigureAwait(false);
    }

    private async Task SubscribeToTasksCoreAsync(
        Func<AgentTaskRequest, CancellationToken, Task> handler,
        CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource();
        var registration = cancellationToken.Register(() => tcs.TrySetResult());
        await using (registration.ConfigureAwait(false))
        {
            var channel = RedisChannel.Literal(TasksChannel);
            var messageQueue = await this.SubscribeChannelAsync(channel).ConfigureAwait(false);

            _ = ProcessMessagesAsync(messageQueue, handler, cancellationToken).ConfigureAwait(false);

            await tcs.Task.ConfigureAwait(false);
            await messageQueue.UnsubscribeAsync().ConfigureAwait(false);
        }

        static async Task ProcessMessagesAsync(
            ChannelMessageQueue queue,
            Func<AgentTaskRequest, CancellationToken, Task> taskHandler,
            CancellationToken ct)
        {
            await foreach (var message in queue.WithCancellation(ct).ConfigureAwait(false))
            {
                if (ct.IsCancellationRequested)
                {
                    break;
                }

                var json = message.Message.ToString();
                var request = JsonSerializer.Deserialize<AgentTaskRequest>(json, SerializerOptions);
                if (request is not null)
                {
                    await taskHandler(request, ct).ConfigureAwait(false);
                }
            }
        }
    }

    private async Task PublishResultCoreAsync(AgentTaskResult result)
    {
        var channel = ResultsChannelPrefix + result.TaskId;

        // Check if result data is large and should be stored externally
        var resultToPublish = await this.PrepareResultForPublishAsync(result).ConfigureAwait(false);

        var json = JsonSerializer.Serialize(resultToPublish, SerializerOptions);
        _ = await this.subscriber.PublishAsync(RedisChannel.Literal(channel), json).ConfigureAwait(false);
    }

    private async Task<AgentTaskResult> PrepareResultForPublishAsync(AgentTaskResult result)
    {
        if (result.Data is null || this.resultStorage is null)
        {
            return result;
        }

        var dataJson = result.Data.RootElement.GetRawText();
        if (dataJson.Length <= LargeResultThreshold)
        {
            return result;
        }

        // Store data externally and create a reference
        var storageKey = await this.resultStorage.StoreResultAsync(
            result.TaskId,
            result.Data,
            CancellationToken.None).ConfigureAwait(false);

        return result with
        {
            Data = null,
            ResultStorageKey = storageKey,
        };
    }

    private async Task SubscribeToResultCoreAsync(
        string taskId,
        Action<AgentTaskResult> handler,
        CancellationToken cancellationToken)
    {
        var channel = ResultsChannelPrefix + taskId;
        var tcs = new TaskCompletionSource();
        var registration = cancellationToken.Register(() => tcs.TrySetResult());
        await using (registration.ConfigureAwait(false))
        {
            var redisChannel = RedisChannel.Literal(channel);
            var messageQueue = await this.SubscribeChannelAsync(redisChannel).ConfigureAwait(false);

            _ = this.ProcessResultMessagesAsync(messageQueue, handler, tcs, cancellationToken).ConfigureAwait(false);

            await tcs.Task.ConfigureAwait(false);
            await messageQueue.UnsubscribeAsync().ConfigureAwait(false);
        }
    }

    private async Task ProcessResultMessagesAsync(
        ChannelMessageQueue queue,
        Action<AgentTaskResult> resultHandler,
        TaskCompletionSource completionSource,
        CancellationToken ct)
    {
        await foreach (var message in queue.WithCancellation(ct).ConfigureAwait(false))
        {
            var json = message.Message.ToString();
            var result = JsonSerializer.Deserialize<AgentTaskResult>(json, SerializerOptions);
            if (result is null)
            {
                continue;
            }

            // Reconstitute the full result if data was stored externally
            var fullResult = await this.ResolveStoredResultAsync(result, ct).ConfigureAwait(false);

            resultHandler(fullResult);
            completionSource.TrySetResult();
            break;
        }
    }

    private async Task<AgentTaskResult> ResolveStoredResultAsync(
        AgentTaskResult result,
        CancellationToken cancellationToken)
    {
        if (!result.HasStoredResult || this.resultStorage is null)
        {
            return result;
        }

        var storedData = await this.resultStorage.RetrieveResultAsync(
            result.ResultStorageKey!,
            cancellationToken).ConfigureAwait(false);

        return storedData is not null
            ? result with { Data = storedData, ResultStorageKey = null }
            : result;
    }

    private async Task PublishProgressCoreAsync(AgentTaskProgress progress)
    {
        var channel = ProgressChannelPrefix + progress.TaskId;
        var json = JsonSerializer.Serialize(progress, SerializerOptions);
        _ = await this.subscriber.PublishAsync(RedisChannel.Literal(channel), json).ConfigureAwait(false);
    }

    private async Task SubscribeToProgressCoreAsync(
        string taskId,
        Action<AgentTaskProgress> handler,
        CancellationToken cancellationToken)
    {
        var channel = ProgressChannelPrefix + taskId;
        var tcs = new TaskCompletionSource();
        var registration = cancellationToken.Register(() => tcs.TrySetResult());
        await using (registration.ConfigureAwait(false))
        {
            var redisChannel = RedisChannel.Literal(channel);
            var messageQueue = await this.SubscribeChannelAsync(redisChannel).ConfigureAwait(false);

            _ = ProcessProgressMessagesAsync(messageQueue, handler, cancellationToken).ConfigureAwait(false);

            await tcs.Task.ConfigureAwait(false);
            await messageQueue.UnsubscribeAsync().ConfigureAwait(false);
        }

        static async Task ProcessProgressMessagesAsync(
            ChannelMessageQueue queue,
            Action<AgentTaskProgress> progressHandler,
            CancellationToken ct)
        {
            await foreach (var message in queue.WithCancellation(ct).ConfigureAwait(false))
            {
                var json = message.Message.ToString();
                var progress = JsonSerializer.Deserialize<AgentTaskProgress>(json, SerializerOptions);
                if (progress is not null)
                {
                    progressHandler(progress);
                }
            }
        }
    }
}
