// <copyright file="A2AAgentClient.cs" company="HemSoft">
// Copyright © 2025 HemSoft
// </copyright>

namespace HemSoft.PowerAI.Console.Agents.Infrastructure;

using System.Diagnostics.CodeAnalysis;

using A2A;

using Microsoft.Agents.AI;

/// <summary>
/// Client for connecting to remote A2A agents.
/// Uses the MS Agent Framework A2ACardResolver pattern to return AIAgent directly.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "A2A client requires real network connection to A2A server for testing")]
internal static class A2AAgentClient
{
    /// <summary>
    /// Connects to a remote A2A agent at the specified URL and returns an AIAgent.
    /// Uses the MS Agent Framework A2ACardResolver to resolve the agent.
    /// </summary>
    /// <param name="agentUrl">The URL of the remote A2A agent.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A tuple containing the AIAgent and the AgentCard describing the remote agent.</returns>
    public static async Task<(AIAgent Agent, AgentCard Card)> ConnectAsync(
        Uri agentUrl,
        CancellationToken cancellationToken = default)
    {
        // Resolve the agent card from the remote URL
        var cardResolver = new A2ACardResolver(agentUrl);
        var resolvedCard = await cardResolver.GetAgentCardAsync(cancellationToken).ConfigureAwait(false);

        // Get AIAgent directly using MS Agent Framework A2A extension
        var agent = await cardResolver.GetAIAgentAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        return (agent, resolvedCard);
    }
}
