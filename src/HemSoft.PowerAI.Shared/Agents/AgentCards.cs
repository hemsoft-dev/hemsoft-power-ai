// <copyright file="AgentCards.cs" company="HemSoft">
// Copyright © 2025 HemSoft
// </copyright>

namespace HemSoft.PowerAI.Common.Agents;

using A2A;

/// <summary>
/// Provides A2A Agent Cards for exposing agents via the A2A protocol.
/// Agent Cards describe agent capabilities for discovery and interoperability.
/// </summary>
public static class AgentCards
{
    private const string ProtocolVersion = "1.0";
    private const string TextPlainMimeType = "text/plain";

    private const string ResearchAgentDescription =
        "Web research specialist that searches and synthesizes findings into actionable insights with sources.";

    private const string WebResearchSkillDescription =
        "Searches web for information and creates structured summaries with findings, sources, and recommendations.";

    private const string CoordinatorDescription =
        "Orchestrator that breaks down complex tasks and delegates to specialized agents for research and files.";

    private const string OrchestrationSkillDescription =
        "Analyzes complex requests, creates subtasks, and delegates to specialized agents like ResearchAgent.";

    private const string FileOperationsDescription =
        "Read, write, and manage files and directories. Save research results and organize file structures.";

    /// <summary>
    /// Copies an unsigned agent card with the externally reachable JSON-RPC endpoint.
    /// </summary>
    /// <param name="card">The card whose metadata is preserved.</param>
    /// <param name="endpoint">The public JSON-RPC endpoint.</param>
    /// <returns>A new card without mutating shared discovery metadata.</returns>
    public static AgentCard WithEndpoint(AgentCard card, Uri endpoint)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(endpoint);
        return new AgentCard
        {
            Name = card.Name,
            Description = card.Description,
            Version = card.Version,
            DocumentationUrl = card.DocumentationUrl,
            IconUrl = card.IconUrl,
            SupportedInterfaces =
            [
                new AgentInterface
                {
                    Url = endpoint.ToString(),
                    ProtocolBinding = "JSONRPC",
                    ProtocolVersion = ProtocolVersion,
                },
            ],
            Capabilities = card.Capabilities,
            Provider = card.Provider,
            Skills = card.Skills,
            DefaultInputModes = card.DefaultInputModes,
            DefaultOutputModes = card.DefaultOutputModes,
            SecuritySchemes = card.SecuritySchemes,
            SecurityRequirements = card.SecurityRequirements,
        };
    }

    /// <summary>
    /// Gets the AgentCard for the ResearchAgent.
    /// </summary>
    /// <param name="baseUrl">The base URL where the agent is hosted.</param>
    /// <returns>An AgentCard describing the ResearchAgent.</returns>
    /// <exception cref="ArgumentNullException">Thrown when baseUrl is null.</exception>
    public static AgentCard CreateResearchAgentCard(Uri baseUrl)
    {
        ArgumentNullException.ThrowIfNull(baseUrl);

        return new AgentCard
        {
            Name = "ResearchAgent",
            Description = ResearchAgentDescription,
            SupportedInterfaces =
            [
                new AgentInterface
                {
                    Url = baseUrl.ToString(),
                    ProtocolBinding = "JSONRPC",
                    ProtocolVersion = ProtocolVersion,
                },
            ],
            Version = "1.0.0",
            DefaultInputModes = [TextPlainMimeType],
            DefaultOutputModes = [TextPlainMimeType],
            Capabilities = new AgentCapabilities
            {
                Streaming = false,
                PushNotifications = false,
            },
            Skills =
            [
                new AgentSkill
                {
                    Id = "web-research",
                    Name = "Web Research",
                    Description = WebResearchSkillDescription,
                    Tags = ["search", "web", "research", "information", "synthesis"],
                    Examples =
                    [
                        "Research the latest AI developments in 2025",
                        "Find information about Microsoft Agent Framework",
                        "Search for best practices in distributed systems",
                    ],
                },
            ],
        };
    }

    /// <summary>
    /// Gets the AgentCard for the CoordinatorAgent.
    /// </summary>
    /// <param name="baseUrl">The base URL where the agent is hosted.</param>
    /// <returns>An AgentCard describing the CoordinatorAgent.</returns>
    /// <exception cref="ArgumentNullException">Thrown when baseUrl is null.</exception>
    public static AgentCard CreateCoordinatorAgentCard(Uri baseUrl)
    {
        ArgumentNullException.ThrowIfNull(baseUrl);

        return new AgentCard
        {
            Name = "CoordinatorAgent",
            Description = CoordinatorDescription,
            SupportedInterfaces =
            [
                new AgentInterface
                {
                    Url = baseUrl.ToString(),
                    ProtocolBinding = "JSONRPC",
                    ProtocolVersion = ProtocolVersion,
                },
            ],
            Version = "1.0.0",
            DefaultInputModes = [TextPlainMimeType],
            DefaultOutputModes = [TextPlainMimeType],
            Capabilities = new AgentCapabilities
            {
                Streaming = false,
                PushNotifications = false,
            },
            Skills =
            [
                new AgentSkill
                {
                    Id = "task-orchestration",
                    Name = "Task Orchestration",
                    Description = OrchestrationSkillDescription,
                    Tags = ["orchestration", "delegation", "coordination", "planning"],
                    Examples =
                    [
                        "Research AI trends and save a report to research-report.md",
                        "Find information about distributed systems and summarize",
                    ],
                },
                new AgentSkill
                {
                    Id = "file-operations",
                    Name = "File Operations",
                    Description = FileOperationsDescription,
                    Tags = ["files", "filesystem", "read", "write", "organize"],
                    Examples =
                    [
                        "Save this report to output/report.md",
                        "Read the contents of README.md",
                        "List all files in the docs folder",
                    ],
                },
            ],
        };
    }
}
