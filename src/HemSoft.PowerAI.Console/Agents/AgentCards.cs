// <copyright file="AgentCards.cs" company="HemSoft">
// Copyright © 2025 HemSoft
// </copyright>

namespace HemSoft.PowerAI.Console.Agents;

using A2A;

/// <summary>
/// Provides A2A Agent Cards for exposing agents via the A2A protocol.
/// Agent Cards describe agent capabilities for discovery and interoperability.
/// </summary>
internal static class AgentCards
{
    private const string ProtocolVersion = "1.0";
    private const string TextPlainMimeType = "text/plain";

    /// <summary>
    /// Gets the AgentCard for the ResearchAgent.
    /// </summary>
    /// <param name="baseUrl">The base URL where the agent is hosted.</param>
    /// <returns>An AgentCard describing the ResearchAgent.</returns>
    public static AgentCard CreateResearchAgentCard(Uri baseUrl) => new()
    {
        Name = "ResearchAgent",
        Description = """
            A web research and information synthesis specialist. Performs targeted
            web searches and synthesizes findings into clear, actionable insights
            with sources and recommendations.
            """,
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
                Description = """
                    Searches the web for current information on any topic and
                    synthesizes findings into structured summaries with key findings,
                    details, sources, and recommendations.
                    """,
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

    /// <summary>
    /// Gets the AgentCard for the CoordinatorAgent.
    /// </summary>
    /// <param name="baseUrl">The base URL where the agent is hosted.</param>
    /// <returns>An AgentCard describing the CoordinatorAgent.</returns>
    public static AgentCard CreateCoordinatorAgentCard(Uri baseUrl) => new()
    {
        Name = "CoordinatorAgent",
        Description = """
            An orchestrator agent that breaks down complex tasks and delegates to
            specialized agents. Coordinates research tasks, file operations, and
            synthesizes results.
            """,
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
                Description = """
                    Analyzes complex requests, breaks them into subtasks, and delegates
                    to specialized agents (like ResearchAgent) to accomplish the goal.
                    """,
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
                Description = """
                    Read, write, and manage files and directories. Can save research
                    results, read existing documents, and organize file structures.
                    """,
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
