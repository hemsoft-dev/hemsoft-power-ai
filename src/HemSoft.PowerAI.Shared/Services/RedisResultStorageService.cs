// <copyright file="RedisResultStorageService.cs" company="HemSoft">
// Copyright © 2025 HemSoft
// </copyright>

namespace HemSoft.PowerAI.Common.Services;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using StackExchange.Redis;

/// <summary>
/// Redis-based implementation of result storage for large payloads.
/// Stores data in Redis keys with automatic expiration.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Requires Redis infrastructure for integration testing.")]
public sealed class RedisResultStorageService : IResultStorageService, IAsyncDisposable
{
    /// <summary>
    /// Default expiration time for stored results (24 hours).
    /// </summary>
    public static readonly TimeSpan DefaultExpiry = TimeSpan.FromHours(24);

    private const string StorageKeyPrefix = "agents:results:data:";

    private readonly ConnectionMultiplexer? ownedConnection;
    private readonly IDatabase database;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisResultStorageService"/> class.
    /// </summary>
    /// <param name="connectionString">Redis connection string.</param>
    public RedisResultStorageService(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        this.ownedConnection = ConnectionMultiplexer.Connect(connectionString);
        this.database = this.ownedConnection.GetDatabase();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisResultStorageService"/> class
    /// using an existing connection.
    /// </summary>
    /// <param name="connection">An existing Redis connection multiplexer.</param>
    public RedisResultStorageService(ConnectionMultiplexer connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        this.ownedConnection = null;
        this.database = connection.GetDatabase();
    }

    /// <inheritdoc/>
    public Task<string> StoreResultAsync(
        string taskId,
        JsonDocument data,
        CancellationToken cancellationToken) =>
        this.StoreResultAsync(taskId, data, DefaultExpiry, cancellationToken);

    /// <inheritdoc/>
    public Task<string> StoreResultAsync(
        string taskId,
        JsonDocument data,
        TimeSpan expiry,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        ArgumentNullException.ThrowIfNull(data);

        return this.StoreResultCoreAsync(taskId, data, expiry);
    }

    /// <inheritdoc/>
    public Task<JsonDocument?> RetrieveResultAsync(
        string storageKey,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);

        return this.RetrieveResultCoreAsync(storageKey);
    }

    /// <inheritdoc/>
    public Task<bool> DeleteResultAsync(
        string storageKey,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);

        return this.database.Context.Keys.DeleteAsync(storageKey, cancellationToken: cancellationToken).AsTask();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;

        // Only dispose the connection if we own it (created from connection string)
        if (this.ownedConnection is not null)
        {
            await this.ownedConnection.CloseAsync().ConfigureAwait(false);
            await this.ownedConnection.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task<string> StoreResultCoreAsync(
        string taskId,
        JsonDocument data,
        TimeSpan expiry)
    {
        var storageKey = StorageKeyPrefix + taskId;
        var json = data.RootElement.GetRawText();

        _ = await this.database.StringSetAsync(
            storageKey,
            json,
            expiry).ConfigureAwait(false);

        return storageKey;
    }

    private async Task<JsonDocument?> RetrieveResultCoreAsync(string storageKey)
    {
        var json = await this.database.StringGetAsync(storageKey).ConfigureAwait(false);

        return json.IsNullOrEmpty
            ? null
            : JsonDocument.Parse(json.ToString());
    }
}
