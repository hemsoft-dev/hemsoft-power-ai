// <copyright file="AgentMessageHandler.cs" company="HemSoft">
// Copyright © 2025 HemSoft
// </copyright>

namespace HemSoft.PowerAI.Common.Agents;

using A2A;

/// <summary>
/// Bridges text inference to the A2A message-only response queue.
/// </summary>
/// <param name="respond">The inference operation.</param>
public sealed class AgentMessageHandler(Func<string, CancellationToken, Task<string>> respond) : IAgentHandler
{
    /// <inheritdoc/>
    public Task ExecuteAsync(RequestContext context, AgentEventQueue eventQueue, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(eventQueue);
        cancellationToken.ThrowIfCancellationRequested();
        return this.ExecuteCoreAsync(context, eventQueue, cancellationToken);
    }

    private async Task ExecuteCoreAsync(RequestContext context, AgentEventQueue eventQueue, CancellationToken cancellationToken)
    {
        var text = string.Join('\n', context.Message.Parts.Where(part => part.Text is not null).Select(part => part.Text));
        var response = await respond(text, cancellationToken).ConfigureAwait(false);
        await eventQueue.EnqueueMessageAsync(
            new Message
            {
                Role = Role.Agent,
                MessageId = Guid.NewGuid().ToString(),
                ContextId = context.ContextId,
                Parts = [Part.FromText(response)],
            },
            cancellationToken).ConfigureAwait(false);
    }
}
