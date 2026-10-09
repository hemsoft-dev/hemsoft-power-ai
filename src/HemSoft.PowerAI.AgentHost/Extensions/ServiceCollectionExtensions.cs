// <copyright file="ServiceCollectionExtensions.cs" company="HemSoft">
// Copyright © 2025 HemSoft
// </copyright>

namespace HemSoft.PowerAI.AgentHost.Extensions;

using System.Globalization;

using A2A;
using A2A.AspNetCore;

using HemSoft.PowerAI.AgentHost.Configuration;
using HemSoft.PowerAI.Common.Agents;

using Microsoft.Agents.AI;

/// <summary>
/// Extension methods for configuring agent host services.
/// </summary>
internal static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the ResearchAgent and its task handler to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">The agent host options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddResearchAgent(this IServiceCollection services, AgentHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        services.AddSingleton(_ =>
            string.IsNullOrEmpty(options.ModelId)
                ? ResearchAgent.Create()
                : ResearchAgent.Create(options.ModelId));

        var baseUrl = new Uri(string.Format(CultureInfo.InvariantCulture, "http://localhost:{0}", options.Port));
        var card = AgentCards.CreateResearchAgentCard(baseUrl);
        services.AddSingleton(provider => new AgentMessageHandler(async (text, ct) =>
        {
            var agent = provider.GetRequiredService<AIAgent>();
            var response = await agent.RunAsync(text, cancellationToken: ct).ConfigureAwait(false);
            return response.Text ?? "No response generated.";
        }));
        services.AddA2AAgent<AgentMessageHandler>(card);
        services.AddSingleton<IAgentHandler>(provider => provider.GetRequiredService<AgentMessageHandler>());
        return services;
    }
}
