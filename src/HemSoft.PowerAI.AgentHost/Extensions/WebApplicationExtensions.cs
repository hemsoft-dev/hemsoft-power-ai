// <copyright file="WebApplicationExtensions.cs" company="HemSoft">
// Copyright © 2025 HemSoft
// </copyright>

namespace HemSoft.PowerAI.AgentHost.Extensions;

using A2A.AspNetCore;

/// <summary>
/// Extension methods for configuring the web application.
/// </summary>
internal static class WebApplicationExtensions
{
    /// <summary>
    /// Maps the A2A endpoints and health check for the agent host.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <param name="agentName">The agent name for the health check response.</param>
    /// <returns>The web application for chaining.</returns>
    public static WebApplication MapAgentEndpoints(this WebApplication app, string agentName) =>
        MapAgentEndpoints(app, agentName, "/");

    /// <summary>
    /// Maps the A2A endpoints and health check for the agent host.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <param name="agentName">The agent name for the health check response.</param>
    /// <param name="routePath">The route path for A2A endpoints.</param>
    /// <returns>The web application for chaining.</returns>
    public static WebApplication MapAgentEndpoints(this WebApplication app, string agentName, string routePath)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentException.ThrowIfNullOrEmpty(agentName);

        app.MapA2A(routePath);
        app.MapGet("/health", () => Results.Ok(new { status = "healthy", agent = agentName }));

        return app;
    }
}
