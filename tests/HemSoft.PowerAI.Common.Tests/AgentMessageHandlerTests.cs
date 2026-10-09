// <copyright file="AgentMessageHandlerTests.cs" company="HemSoft">
// Copyright © 2025 HemSoft
// </copyright>

namespace HemSoft.PowerAI.Common.Tests;

using A2A;

using HemSoft.PowerAI.Common.Agents;

/// <summary>
/// Verifies the current A2A message-only transport bridge.
/// </summary>
public sealed class AgentMessageHandlerTests
{
    /// <summary>
    /// Preserves text ordering, context and cancellation through inference and the response queue.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task ExecutePreservesMessageAndCancellationAsync()
    {
        using var source = new CancellationTokenSource();
        var context = new RequestContext
        {
            Message = new Message { MessageId = "incoming", Role = Role.User, Parts = [Part.FromText("one"), Part.FromText("two")] },
            ContextId = "conversation",
            TaskId = "task",
            StreamingResponse = false,
        };
        var handler = new AgentMessageHandler((text, token) =>
        {
            Assert.Equal("one\ntwo", text);
            Assert.Equal(source.Token, token);
            return Task.FromResult("answer");
        });
        var queue = new AgentEventQueue();
        await handler.ExecuteAsync(context, queue, source.Token);
        queue.Complete();
        var count = 0;
        await foreach (var item in queue.WithCancellation(source.Token).ConfigureAwait(true))
        {
            count++;
            Assert.Equal(Role.Agent, item.Message!.Role);
            Assert.Equal("conversation", item.Message.ContextId);
            Assert.Equal("answer", Assert.Single(item.Message.Parts).Text);
            Assert.True(Guid.TryParse(item.Message.MessageId, out _));
        }

        Assert.Equal(1, count);
    }

    /// <summary>
    /// Rejected and canceled requests must not call inference.
    /// </summary>
    /// <returns>A task representing the test.</returns>
    [Fact]
    public async Task InvalidRequestsDoNotInvokeInferenceAsync()
    {
        var handler = new AgentMessageHandler((_, _) => throw new InvalidOperationException("Unexpected inference"));
        var context = new RequestContext
        {
            Message = new Message { MessageId = "incoming", Role = Role.User, Parts = [Part.FromText("one")] },
            ContextId = "conversation",
            TaskId = "task",
            StreamingResponse = false,
        };
        var queue = new AgentEventQueue();
        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.ExecuteAsync(null!, queue, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.ExecuteAsync(context, null!, CancellationToken.None));
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => handler.ExecuteAsync(context, queue, new CancellationToken(canceled: true)));
    }
}
