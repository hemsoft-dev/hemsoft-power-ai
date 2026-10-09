// <copyright file="A2AAgentHost.cs" company="HemSoft">
// Copyright © 2025 HemSoft
// </copyright>

namespace HemSoft.PowerAI.Console.Hosting;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using A2A;
using A2A.AspNetCore;

using HemSoft.PowerAI.Common.Agents;

using Microsoft.Agents.AI;

/// <summary>
/// Hosts an AIAgent as an A2A server using the MapA2A pattern from MS Agent Framework.
/// Enables remote agent-to-agent communication via A2A protocol.
/// </summary>
/// <param name="agent">The AIAgent to host.</param>
/// <param name="agentCard">The AgentCard describing the agent's capabilities.</param>
/// <param name="port">The port to host the agent on.</param>
/// <param name="routePath">The route path for the A2A endpoint (default: "/").</param>
[ExcludeFromCodeCoverage(Justification = "A2A host requires ASP.NET Core integration testing with real HTTP endpoints")]
internal sealed class A2AAgentHost(
    AIAgent agent,
    AgentCard agentCard,
    int port,
    string routePath = "/") : IAsyncDisposable
{
    private WebApplication? app;

    /// <summary>
    /// Gets the listening address after startup, including an assigned ephemeral port.
    /// </summary>
    internal Uri? ListeningUri => this.app is null ? null : new Uri(this.app.Urls.Single());

    /// <summary>
    /// Starts the A2A server asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the server startup.</returns>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (this.app != null)
        {
            await this.app.DisposeAsync().ConfigureAwait(false);
        }

        var builder = WebApplication.CreateSlimBuilder();
        _ = builder.WebHost.UseUrls(string.Format(CultureInfo.InvariantCulture, "http://127.0.0.1:{0}", port));

        builder.Services.AddSingleton(_ => new AgentMessageHandler(async (text, ct) =>
        {
            var response = await agent.RunAsync(text, cancellationToken: ct).ConfigureAwait(false);
            return response.Text ?? "No response generated.";
        }));
        builder.Services.AddA2AAgent<AgentMessageHandler>(agentCard);
        builder.Services.AddSingleton<IAgentHandler>(provider => provider.GetRequiredService<AgentMessageHandler>());
        this.app = builder.Build();
        _ = this.app.MapA2A(routePath);
        IResult GetAgentCard(HttpRequest request)
        {
            var configuredEndpoint = new Uri(agentCard.SupportedInterfaces[0].Url);
            var endpoint = configuredEndpoint.IsLoopback
                ? new Uri($"{request.Scheme}://{request.Host}{request.PathBase}{routePath}")
                : configuredEndpoint;
            return Results.Ok(AgentCards.WithEndpoint(agentCard, endpoint));
        }

        _ = this.app.MapGet("/.well-known/agent-card.json", GetAgentCard);

        await this.app.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Waits for the server to shut down.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the shutdown wait.</returns>
    public async Task WaitForShutdownAsync(CancellationToken cancellationToken = default)
    {
        if (this.app != null)
        {
            await this.app.WaitForShutdownAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Stops the A2A server.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the server stop.</returns>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (this.app != null)
        {
            await this.app.StopAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (this.app != null)
        {
            await this.app.DisposeAsync().ConfigureAwait(false);
            this.app = null;
        }
    }
}
