// <copyright file="MailAgentTests.cs" company="HemSoft">
// Copyright © 2025 HemSoft
// </copyright>

namespace HemSoft.PowerAI.Console.Tests;

using System.Reflection;

using HemSoft.PowerAI.Console.Agents;

using Microsoft.Agents.AI;

/// <summary>
/// Unit tests for <see cref="MailAgent"/>.
/// </summary>
[Collection("EnvironmentVariableTests")]
public sealed class MailAgentTests : IDisposable
{
    private readonly string? originalBaseUrl = Environment.GetEnvironmentVariable("OPENROUTER_BASE_URL");
    private readonly string? originalApiKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");

    /// <summary>
    /// Initializes a new instance of the <see cref="MailAgentTests"/> class with offline client configuration.
    /// </summary>
    public MailAgentTests()
    {
        Environment.SetEnvironmentVariable("OPENROUTER_BASE_URL", "https://fixture.invalid/");
        Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", "offline-test-key");
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Environment.SetEnvironmentVariable("OPENROUTER_BASE_URL", this.originalBaseUrl);
        Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", this.originalApiKey);
    }

    /// <summary>
    /// Tests that Create returns a non-null AIAgent with correct name when using unconfigured Graph client.
    /// Note: Agent creation succeeds even with null client - the tool will return an error at runtime.
    /// </summary>
    [Fact]
    public void CreateReturnsValidAgentWithUnconfiguredClient()
    {
        // Arrange
        var mockProvider = new MockGraphClientProvider();

        // Act
        var agent = MailAgent.Create(mockProvider);

        // Assert
        Assert.NotNull(agent);
        Assert.Equal("MailAgent", agent.Name);
    }

    /// <summary>
    /// Tests that Create returns an agent with a description.
    /// </summary>
    [Fact]
    public void CreateReturnsAgentWithDescription()
    {
        // Arrange
        var mockProvider = new MockGraphClientProvider();

        // Act
        var agent = MailAgent.Create(mockProvider);

        // Assert
        Assert.NotNull(agent);
        Assert.Contains("mail", agent.Description ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Tests that Create accepts null spam storage without throwing.
    /// </summary>
    [Fact]
    public void CreateAcceptsNullSpamStorageWithoutThrowing()
    {
        // Arrange
        var mockProvider = new MockGraphClientProvider();

        // Act
        var exception = Record.Exception(() => MailAgent.Create(mockProvider, spamStorage: null));

        // Assert
        Assert.Null(exception);
    }

    /// <summary>
    /// Tests that the agent can be converted to an AI function for agent-as-tool pattern.
    /// </summary>
    [Fact]
    public void AgentCanBeConvertedToAIFunction()
    {
        // Arrange
        var mockProvider = new MockGraphClientProvider();
        var agent = MailAgent.Create(mockProvider);

        // Act
        var aiFunction = agent.AsAIFunction();

        // Assert
        Assert.NotNull(aiFunction);
    }

    /// <summary>
    /// Tests that ModelId constant exists and has expected value.
    /// </summary>
    [Fact]
    public void ModelIdConstantHasExpectedValue()
    {
        // Arrange
        var field = typeof(MailAgent).GetField(
            "ModelId",
            BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.GetField);

        // Assert
        Assert.NotNull(field);
        var value = field.GetValue(obj: null) as string;
        Assert.Equal("x-ai/grok-4.1-fast", value);
    }

    /// <summary>
    /// Tests that Instructions constant exists and contains key mail operations.
    /// </summary>
    [Fact]
    public void InstructionsContainsMailOperations()
    {
        // Arrange
        var field = typeof(MailAgent).GetField(
            "Instructions",
            BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.GetField);

        // Assert
        Assert.NotNull(field);
        var value = field.GetValue(obj: null) as string;
        Assert.NotNull(value);
        Assert.Contains("inbox", value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("send", value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("search", value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("delete", value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("junk", value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("blocklist", value, StringComparison.OrdinalIgnoreCase);
    }
}
