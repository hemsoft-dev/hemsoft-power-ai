// <copyright file="AgentWorkerService.cs" company="HemSoft">
// Copyright © 2025 HemSoft
// </copyright>

namespace HemSoft.PowerAI.AgentWorker;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;

using A2A;

using HemSoft.PowerAI.AgentWorker.Configuration;
using HemSoft.PowerAI.Common.Models;
using HemSoft.PowerAI.Common.Services;

using Microsoft.Extensions.Options;

using AgentTaskStatus = HemSoft.PowerAI.Common.Models.AgentTaskStatus;

/// <summary>
/// Background service that processes agent tasks from the Redis pub/sub channel.
/// Subscribes to the agents:tasks channel and delegates execution to AgentHost via A2A.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Background service requires Redis infrastructure for integration testing.")]
internal sealed partial class AgentWorkerService : BackgroundService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly IAgentTaskBroker broker;
    private readonly ILogger<AgentWorkerService> logger;
    private readonly TimeProvider timeProvider;
    private readonly Uri agentHostUrl;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentWorkerService"/> class.
    /// </summary>
    /// <param name="broker">The task broker for receiving and publishing tasks.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    /// <param name="agentHostSettings">AgentHost configuration settings.</param>
    public AgentWorkerService(
        IAgentTaskBroker broker,
        ILogger<AgentWorkerService> logger,
        IOptions<AgentHostSettings> agentHostSettings)
        : this(broker, logger, agentHostSettings, TimeProvider.System)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentWorkerService"/> class.
    /// </summary>
    /// <param name="broker">The task broker for receiving and publishing tasks.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    /// <param name="agentHostSettings">AgentHost configuration settings.</param>
    /// <param name="timeProvider">The time provider for timestamps.</param>
    public AgentWorkerService(
        IAgentTaskBroker broker,
        ILogger<AgentWorkerService> logger,
        IOptions<AgentHostSettings> agentHostSettings,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(broker);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(agentHostSettings);
        ArgumentNullException.ThrowIfNull(timeProvider);

        this.broker = broker;
        this.logger = logger;
        this.timeProvider = timeProvider;
        this.agentHostUrl = agentHostSettings.Value.GetRequiredBaseUrl();
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogServiceStarting(this.logger);

        try
        {
            await this.broker.SubscribeToTasksAsync(
                this.ProcessTaskAsync,
                stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            LogServiceStopping(this.logger);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "AgentWorkerService starting. Subscribing to task queue...")]
    private static partial void LogServiceStarting(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "AgentWorkerService stopping due to cancellation.")]
    private static partial void LogServiceStopping(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Processing task {TaskId} of type {AgentType}")]
    private static partial void LogProcessingTask(ILogger logger, string taskId, string agentType);

    [LoggerMessage(Level = LogLevel.Information, Message = "╔══════════════════════════════════════════════════════════════╗")]
    private static partial void LogBannerTop(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "║  AGENT: {AgentName,-52} ║")]
    private static partial void LogBannerAgent(ILogger logger, string agentName);

    [LoggerMessage(Level = LogLevel.Information, Message = "║  Task:  {TaskId,-52} ║")]
    private static partial void LogBannerTask(ILogger logger, string taskId);

    [LoggerMessage(Level = LogLevel.Information, Message = "╚══════════════════════════════════════════════════════════════╝")]
    private static partial void LogBannerBottom(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Task {TaskId} completed successfully")]
    private static partial void LogTaskCompleted(ILogger logger, string taskId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Task {TaskId} was cancelled")]
    private static partial void LogTaskCancelled(ILogger logger, string taskId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Task {TaskId} failed with error: {ErrorMessage}")]
    private static partial void LogTaskFailed(ILogger logger, Exception ex, string taskId, string errorMessage);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Calling AgentHost at {Url} for prompt: {Prompt}")]
    private static partial void LogCallingAgentHost(ILogger logger, Uri url, string prompt);

    [LoggerMessage(Level = LogLevel.Information, Message = "AgentHost resolved agent: {AgentName}")]
    private static partial void LogAgentResolved(ILogger logger, string agentName);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Message}")]
    private static partial void LogProgress(ILogger logger, string message);

    private static JsonDocument BuildIterativeResultJson(
        ResearchIterationState state,
        string agentName,
        Uri agentHostUrl,
        TimeProvider timeProvider)
    {
        var resultData = new
        {
            text = state.FinalSynthesis,
            agentType = "iterative-research",
            agentName,
            agentHostUrl = agentHostUrl.ToString(),
            iterations = state.CurrentIteration,
            finalScore = state.LatestEvaluation?.QualityScore ?? 0,
            originalQuery = state.OriginalQuery,
            timestamp = timeProvider.GetUtcNow(),
        };

        var json = JsonSerializer.Serialize(resultData, SerializerOptions);
        return JsonDocument.Parse(json);
    }

    private static Task ReportProgressAsync(AgentTaskContext? context, string message, CancellationToken cancellationToken) =>
        context?.ReportProgressAsync(message, cancellationToken) ?? Task.CompletedTask;

    private async Task ProcessTaskAsync(AgentTaskRequest request, CancellationToken cancellationToken)
    {
        LogProcessingTask(this.logger, request.TaskId, request.AgentType);

        // Display prominent agent banner
        var agentName = request.AgentType.ToUpperInvariant() switch
        {
            "RESEARCH" => "ResearchAgent",
            "ITERATIVE-RESEARCH" => "IterativeResearchAgent",
            _ => request.AgentType,
        };

        // Determine the model ID based on agent type
        var modelId = request.AgentType.ToUpperInvariant() switch
        {
            "RESEARCH" => Common.Agents.ResearchAgent.DefaultModelId,
            "ITERATIVE-RESEARCH" => Common.Agents.ResearchAgent.DefaultModelId,
            _ => "unknown",
        };

        LogBannerTop(this.logger);
        LogBannerAgent(this.logger, agentName);
        LogBannerTask(this.logger, request.TaskId);
        LogBannerBottom(this.logger);

        // Set up task context for progress reporting with agent info
        var taskContext = new AgentTaskContext(request.TaskId, this.broker, this.timeProvider)
        {
            CurrentAgentName = agentName,
            CurrentModelId = modelId,
        };
        using var scope = AgentTaskContext.SetCurrent(taskContext);

        AgentTaskResult result;

        try
        {
            var data = await this.ExecuteAgentAsync(request, cancellationToken).ConfigureAwait(false);

            result = new AgentTaskResult(
                TaskId: request.TaskId,
                Status: AgentTaskStatus.Completed,
                Data: data,
                Error: null,
                CompletedAt: this.timeProvider.GetUtcNow());

            LogTaskCompleted(this.logger, request.TaskId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            result = new AgentTaskResult(
                TaskId: request.TaskId,
                Status: AgentTaskStatus.Cancelled,
                Data: null,
                Error: "Task was cancelled.",
                CompletedAt: this.timeProvider.GetUtcNow());

            LogTaskCancelled(this.logger, request.TaskId);
        }
        catch (NotSupportedException ex)
        {
            result = new AgentTaskResult(
                TaskId: request.TaskId,
                Status: AgentTaskStatus.Failed,
                Data: null,
                Error: ex.Message,
                CompletedAt: this.timeProvider.GetUtcNow());

            LogTaskFailed(this.logger, ex, request.TaskId, ex.Message);
        }
        catch (HttpRequestException ex)
        {
            result = new AgentTaskResult(
                TaskId: request.TaskId,
                Status: AgentTaskStatus.Failed,
                Data: null,
                Error: ex.Message,
                CompletedAt: this.timeProvider.GetUtcNow());

            LogTaskFailed(this.logger, ex, request.TaskId, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            result = new AgentTaskResult(
                TaskId: request.TaskId,
                Status: AgentTaskStatus.Failed,
                Data: null,
                Error: ex.Message,
                CompletedAt: this.timeProvider.GetUtcNow());

            LogTaskFailed(this.logger, ex, request.TaskId, ex.Message);
        }

        await this.broker.PublishResultAsync(result, cancellationToken).ConfigureAwait(false);
    }

    private Task<JsonDocument?> ExecuteAgentAsync(
        AgentTaskRequest request,
        CancellationToken cancellationToken) =>
        request.AgentType.ToUpperInvariant() switch
        {
            "RESEARCH" => this.ExecuteResearchAgentAsync(request.Prompt, cancellationToken),
            "ITERATIVE-RESEARCH" => this.ExecuteIterativeResearchAgentAsync(request.Prompt, cancellationToken),
            _ => throw new NotSupportedException($"Unknown agent type: {request.AgentType}"),
        };

    private async Task<JsonDocument?> ExecuteResearchAgentAsync(
        string prompt,
        CancellationToken cancellationToken)
    {
        LogCallingAgentHost(this.logger, this.agentHostUrl, prompt);

        // Resolve the agent from AgentHost via A2A protocol
        var cardResolver = new A2ACardResolver(this.agentHostUrl);
        var agentCard = await cardResolver.GetAgentCardAsync(cancellationToken).ConfigureAwait(false);

        LogAgentResolved(this.logger, agentCard.Name);

        // Get AIAgent and execute the prompt
        var agent = await cardResolver.GetAIAgentAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        var response = await agent.RunAsync(prompt, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        // Wrap the text response in a structured JSON document
        var resultData = new
        {
            text = response.Text ?? string.Empty,
            agentType = "research",
            agentName = agentCard.Name,
            agentHostUrl = this.agentHostUrl.ToString(),
            timestamp = this.timeProvider.GetUtcNow(),
        };

        var json = JsonSerializer.Serialize(resultData, SerializerOptions);

        return JsonDocument.Parse(json);
    }

    private async Task<JsonDocument?> ExecuteIterativeResearchAgentAsync(
        string prompt,
        CancellationToken cancellationToken)
    {
        var taskContext = AgentTaskContext.Instance;

        // Resolve the research agent name for logging
        LogCallingAgentHost(this.logger, this.agentHostUrl, prompt);
        var cardResolver = new A2ACardResolver(this.agentHostUrl);
        var agentCard = await cardResolver.GetAgentCardAsync(cancellationToken).ConfigureAwait(false);

        LogAgentResolved(this.logger, agentCard.Name);

        await ReportProgressAsync(taskContext, "Starting decomposed iterative research", cancellationToken)
            .ConfigureAwait(false);

        // Use the new IterativeResearchService with task decomposition
        var researchService = new IterativeResearchService(ReportProgressCallback);

        var state = await researchService.ResearchAsync(prompt, cancellationToken)
            .ConfigureAwait(false);

        var completionMsg = string.Create(
            CultureInfo.InvariantCulture,
            $"Research complete after {state.CurrentIteration} iteration(s)");
        await ReportProgressAsync(taskContext, completionMsg, cancellationToken).ConfigureAwait(false);

        return BuildIterativeResultJson(state, agentCard.Name, this.agentHostUrl, this.timeProvider);

        // Local function for progress reporting to both logs and task context
        void ReportProgressCallback(string message)
        {
            LogProgress(this.logger, message);
            _ = ReportProgressAsync(taskContext, message, cancellationToken);
        }
    }
}
