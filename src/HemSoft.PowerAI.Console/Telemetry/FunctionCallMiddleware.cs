// <copyright file="FunctionCallMiddleware.cs" company="HemSoft">
// Copyright © 2025 HemSoft
// </copyright>

namespace HemSoft.PowerAI.Console.Telemetry;

using System.Diagnostics;
using System.Text.Json;

using Microsoft.Extensions.AI;

/// <summary>
/// Middleware that logs AI function/tool invocations to OpenTelemetry.
/// Wraps the chat client to capture tool calls with their arguments and results.
/// </summary>
/// <param name="innerClient">The inner chat client to wrap.</param>
internal sealed class FunctionCallMiddleware(IChatClient innerClient) : DelegatingChatClient(innerClient)
{
    private static readonly ActivitySource ActivitySource = new("HemSoft.PowerAI.FunctionCalls");

    /// <inheritdoc/>
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("ChatCompletion");

        var toolsUsed = options?.Tools?.Count ?? 0;
        activity?.SetTag("ai.tools.count", toolsUsed);
        activity?.SetTag("ai.messages.count", messages.Count());

        var response = await base.GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);

        LogFunctionCalls(response, activity);

        return response;
    }

    /// <inheritdoc/>
    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("ChatCompletionStreaming");

        var toolsUsed = options?.Tools?.Count ?? 0;
        activity?.SetTag("ai.tools.count", toolsUsed);
        activity?.SetTag("ai.messages.count", messages.Count());

        await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken).ConfigureAwait(false))
        {
            LogFunctionCallsFromUpdate(update, activity);
            yield return update;
        }
    }

    private static void LogFunctionCallsFromUpdate(ChatResponseUpdate update, Activity? activity)
    {
        if (update.Contents == null)
        {
            return;
        }

        foreach (var content in update.Contents)
        {
            if (content is FunctionCallContent { Name: not null } functionCall)
            {
                LogFunctionCall(functionCall.Name, functionCall.Arguments, activity);
            }
        }
    }

    private static void LogFunctionCalls(ChatResponse response, Activity? activity)
    {
        var functionCalls = response.Messages
            .SelectMany(m => m.Contents ?? [])
            .OfType<FunctionCallContent>()
            .ToList();

        if (functionCalls.Count == 0)
        {
            return;
        }

        activity?.SetTag("ai.function_calls.count", functionCalls.Count);

        foreach (var call in functionCalls.Where(c => c.Name != null))
        {
            LogFunctionCall(call.Name, call.Arguments, activity);
        }
    }

    private static void LogFunctionCall(string functionName, IDictionary<string, object?>? arguments, Activity? activity)
    {
        var argsJson = arguments != null
            ? JsonSerializer.Serialize(arguments, JsonOptions.Indented)
            : "{}";

        // Truncate large arguments for logging
        var truncatedArgs = argsJson.Length > 500
            ? $"{argsJson[..500]}..."
            : argsJson;

        activity?.AddEvent(new ActivityEvent(
            "FunctionCall",
            tags: new ActivityTagsCollection
            {
                { "function.name", functionName },
                { "function.arguments", truncatedArgs },
            }));

        System.Console.WriteLine($"[FunctionCall] {functionName}: {truncatedArgs}");
    }

    private static class JsonOptions
    {
        public static readonly JsonSerializerOptions Indented = new()
        {
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };
    }
}
