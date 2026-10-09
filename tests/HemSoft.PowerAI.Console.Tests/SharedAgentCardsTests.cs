// <copyright file="SharedAgentCardsTests.cs" company="HemSoft">
// Copyright © 2025 HemSoft
// </copyright>

namespace HemSoft.PowerAI.Console.Tests;

using HemSoft.PowerAI.Common.Agents;

/// <summary>
/// Tests for the <see cref="AgentCards"/> class in the Shared project.
/// </summary>
public class SharedAgentCardsTests
{
    private static readonly Uri TestBaseUrl = new("http://localhost:5000/");

    /// <summary>
    /// Verifies that CreateResearchAgentCard returns a valid card with correct name.
    /// </summary>
    [Fact]
    public void CreateResearchAgentCardReturnsCardWithCorrectName()
    {
        // Act
        var card = AgentCards.CreateResearchAgentCard(TestBaseUrl);

        // Assert
        Assert.Equal("ResearchAgent", card.Name);
    }

    /// <summary>
    /// Verifies that CreateResearchAgentCard sets the URL correctly.
    /// </summary>
    [Fact]
    public void CreateResearchAgentCardSetsUrlCorrectly()
    {
        // Act
        var card = AgentCards.CreateResearchAgentCard(TestBaseUrl);

        // Assert
        Assert.Equal(TestBaseUrl.ToString(), Assert.Single(card.SupportedInterfaces).Url);
    }

    /// <summary>
    /// Verifies that CreateResearchAgentCard throws ArgumentNullException for null baseUrl.
    /// </summary>
    [Fact]
    public void CreateResearchAgentCardThrowsForNullBaseUrl() =>
        Assert.Throws<ArgumentNullException>(() => AgentCards.CreateResearchAgentCard(null!));

    /// <summary>
    /// Verifies that CreateResearchAgentCard has a non-empty description.
    /// </summary>
    [Fact]
    public void CreateResearchAgentCardHasDescription()
    {
        // Act
        var card = AgentCards.CreateResearchAgentCard(TestBaseUrl);

        // Assert
        Assert.NotNull(card.Description);
        Assert.NotEmpty(card.Description);
    }

    /// <summary>
    /// Verifies that CreateResearchAgentCard has at least one skill.
    /// </summary>
    [Fact]
    public void CreateResearchAgentCardHasSkills()
    {
        // Act
        var card = AgentCards.CreateResearchAgentCard(TestBaseUrl);

        // Assert
        Assert.NotNull(card.Skills);
        Assert.NotEmpty(card.Skills);
    }

    /// <summary>
    /// Verifies that CreateResearchAgentCard has capabilities set.
    /// </summary>
    [Fact]
    public void CreateResearchAgentCardHasCapabilities()
    {
        // Act
        var card = AgentCards.CreateResearchAgentCard(TestBaseUrl);

        // Assert
        Assert.NotNull(card.Capabilities);
    }

    /// <summary>
    /// Verifies that CreateResearchAgentCard has version information.
    /// </summary>
    [Fact]
    public void CreateResearchAgentCardHasVersion()
    {
        // Act
        var card = AgentCards.CreateResearchAgentCard(TestBaseUrl);

        // Assert
        Assert.NotNull(card.Version);
        Assert.Equal("1.0.0", card.Version);
    }

    /// <summary>
    /// Verifies that CreateResearchAgentCard has protocol version information.
    /// </summary>
    [Fact]
    public void CreateResearchAgentCardHasProtocolVersion()
    {
        // Act
        var card = AgentCards.CreateResearchAgentCard(TestBaseUrl);

        // Assert
        Assert.NotNull(Assert.Single(card.SupportedInterfaces).ProtocolVersion);
        Assert.Equal("1.0", Assert.Single(card.SupportedInterfaces).ProtocolVersion);
    }

    /// <summary>
    /// Verifies that CreateResearchAgentCard skill has correct ID.
    /// </summary>
    [Fact]
    public void CreateResearchAgentCardSkillHasCorrectId()
    {
        // Act
        var card = AgentCards.CreateResearchAgentCard(TestBaseUrl);
        var skill = card.Skills[0];

        // Assert
        Assert.Equal("web-research", skill.Id);
    }

    /// <summary>
    /// Verifies that CreateResearchAgentCard skill has name.
    /// </summary>
    [Fact]
    public void CreateResearchAgentCardSkillHasName()
    {
        // Act
        var card = AgentCards.CreateResearchAgentCard(TestBaseUrl);
        var skill = card.Skills[0];

        // Assert
        Assert.Equal("Web Research", skill.Name);
    }

    /// <summary>
    /// Verifies that CreateResearchAgentCard skill has description.
    /// </summary>
    [Fact]
    public void CreateResearchAgentCardSkillHasDescription()
    {
        // Act
        var card = AgentCards.CreateResearchAgentCard(TestBaseUrl);
        var skill = card.Skills[0];

        // Assert
        Assert.NotNull(skill.Description);
        Assert.NotEmpty(skill.Description);
    }

    /// <summary>
    /// Verifies that CreateResearchAgentCard skill has tags.
    /// </summary>
    [Fact]
    public void CreateResearchAgentCardSkillHasTags()
    {
        // Act
        var card = AgentCards.CreateResearchAgentCard(TestBaseUrl);
        var skill = card.Skills[0];

        // Assert
        Assert.NotNull(skill.Tags);
        Assert.Contains("search", skill.Tags, StringComparer.Ordinal);
        Assert.Contains("research", skill.Tags, StringComparer.Ordinal);
    }

    /// <summary>
    /// Verifies that CreateResearchAgentCard skill has examples.
    /// </summary>
    [Fact]
    public void CreateResearchAgentCardSkillHasExamples()
    {
        // Act
        var card = AgentCards.CreateResearchAgentCard(TestBaseUrl);
        var skill = card.Skills[0];

        // Assert
        Assert.NotNull(skill.Examples);
        Assert.NotEmpty(skill.Examples);
    }

    /// <summary>
    /// Verifies that CreateResearchAgentCard has default input modes.
    /// </summary>
    [Fact]
    public void CreateResearchAgentCardHasDefaultInputModes()
    {
        // Act
        var card = AgentCards.CreateResearchAgentCard(TestBaseUrl);

        // Assert
        Assert.NotNull(card.DefaultInputModes);
        Assert.Contains("text/plain", card.DefaultInputModes, StringComparer.Ordinal);
    }

    /// <summary>
    /// Verifies that CreateResearchAgentCard has default output modes.
    /// </summary>
    [Fact]
    public void CreateResearchAgentCardHasDefaultOutputModes()
    {
        // Act
        var card = AgentCards.CreateResearchAgentCard(TestBaseUrl);

        // Assert
        Assert.NotNull(card.DefaultOutputModes);
        Assert.Contains("text/plain", card.DefaultOutputModes, StringComparer.Ordinal);
    }

    /// <summary>
    /// Verifies that CreateResearchAgentCard capabilities are correctly set.
    /// </summary>
    [Fact]
    public void CreateResearchAgentCardCapabilitiesAreCorrect()
    {
        // Act
        var card = AgentCards.CreateResearchAgentCard(TestBaseUrl);
        var capabilities = card.Capabilities;

        // Assert
        Assert.False(capabilities.Streaming);
        Assert.False(capabilities.PushNotifications);
        Assert.Equal("JSONRPC", Assert.Single(card.SupportedInterfaces).ProtocolBinding);
    }

    /// <summary>
    /// Verifies that CreateCoordinatorAgentCard returns a valid card with correct name.
    /// </summary>
    [Fact]
    public void CreateCoordinatorAgentCardReturnsCardWithCorrectName()
    {
        // Act
        var card = AgentCards.CreateCoordinatorAgentCard(TestBaseUrl);

        // Assert
        Assert.Equal("CoordinatorAgent", card.Name);
    }

    /// <summary>
    /// Verifies that CreateCoordinatorAgentCard sets the URL correctly.
    /// </summary>
    [Fact]
    public void CreateCoordinatorAgentCardSetsUrlCorrectly()
    {
        // Act
        var card = AgentCards.CreateCoordinatorAgentCard(TestBaseUrl);

        // Assert
        Assert.Equal(TestBaseUrl.ToString(), Assert.Single(card.SupportedInterfaces).Url);
    }

    /// <summary>
    /// Verifies that CreateCoordinatorAgentCard throws ArgumentNullException for null baseUrl.
    /// </summary>
    [Fact]
    public void CreateCoordinatorAgentCardThrowsForNullBaseUrl() =>
        Assert.Throws<ArgumentNullException>(() => AgentCards.CreateCoordinatorAgentCard(null!));

    /// <summary>
    /// Verifies that CreateCoordinatorAgentCard has a non-empty description.
    /// </summary>
    [Fact]
    public void CreateCoordinatorAgentCardHasDescription()
    {
        // Act
        var card = AgentCards.CreateCoordinatorAgentCard(TestBaseUrl);

        // Assert
        Assert.NotNull(card.Description);
        Assert.NotEmpty(card.Description);
    }

    /// <summary>
    /// Verifies that CreateCoordinatorAgentCard has at least one skill.
    /// </summary>
    [Fact]
    public void CreateCoordinatorAgentCardHasSkills()
    {
        // Act
        var card = AgentCards.CreateCoordinatorAgentCard(TestBaseUrl);

        // Assert
        Assert.NotNull(card.Skills);
        Assert.NotEmpty(card.Skills);
    }

    /// <summary>
    /// Verifies that CreateCoordinatorAgentCard has capabilities set.
    /// </summary>
    [Fact]
    public void CreateCoordinatorAgentCardHasCapabilities()
    {
        // Act
        var card = AgentCards.CreateCoordinatorAgentCard(TestBaseUrl);

        // Assert
        Assert.NotNull(card.Capabilities);
    }

    /// <summary>
    /// Verifies that CreateCoordinatorAgentCard has version information.
    /// </summary>
    [Fact]
    public void CreateCoordinatorAgentCardHasVersion()
    {
        // Act
        var card = AgentCards.CreateCoordinatorAgentCard(TestBaseUrl);

        // Assert
        Assert.NotNull(card.Version);
        Assert.Equal("1.0.0", card.Version);
    }

    /// <summary>
    /// Verifies that CreateCoordinatorAgentCard has protocol version information.
    /// </summary>
    [Fact]
    public void CreateCoordinatorAgentCardHasProtocolVersion()
    {
        // Act
        var card = AgentCards.CreateCoordinatorAgentCard(TestBaseUrl);

        // Assert
        Assert.NotNull(Assert.Single(card.SupportedInterfaces).ProtocolVersion);
        Assert.Equal("1.0", Assert.Single(card.SupportedInterfaces).ProtocolVersion);
    }

    /// <summary>
    /// Verifies that CreateCoordinatorAgentCard has multiple skills.
    /// </summary>
    [Fact]
    public void CreateCoordinatorAgentCardHasMultipleSkills()
    {
        // Act
        var card = AgentCards.CreateCoordinatorAgentCard(TestBaseUrl);

        // Assert
        Assert.True(card.Skills.Count >= 2);
    }

    /// <summary>
    /// Verifies that CreateCoordinatorAgentCard has task orchestration skill.
    /// </summary>
    [Fact]
    public void CreateCoordinatorAgentCardHasTaskOrchestrationSkill()
    {
        // Act
        var card = AgentCards.CreateCoordinatorAgentCard(TestBaseUrl);
        var skill = card.Skills.First(s => string.Equals(s.Id, "task-orchestration", StringComparison.Ordinal));

        // Assert
        Assert.Equal("Task Orchestration", skill.Name);
        Assert.NotNull(skill.Description);
        Assert.NotNull(skill.Tags);
        Assert.Contains("orchestration", skill.Tags, StringComparer.Ordinal);
    }

    /// <summary>
    /// Verifies that CreateCoordinatorAgentCard has file operations skill.
    /// </summary>
    [Fact]
    public void CreateCoordinatorAgentCardHasFileOperationsSkill()
    {
        // Act
        var card = AgentCards.CreateCoordinatorAgentCard(TestBaseUrl);
        var skill = card.Skills.First(s => string.Equals(s.Id, "file-operations", StringComparison.Ordinal));

        // Assert
        Assert.Equal("File Operations", skill.Name);
        Assert.NotNull(skill.Description);
        Assert.NotNull(skill.Tags);
        Assert.Contains("files", skill.Tags, StringComparer.Ordinal);
    }

    /// <summary>
    /// Verifies that CreateCoordinatorAgentCard has default input modes.
    /// </summary>
    [Fact]
    public void CreateCoordinatorAgentCardHasDefaultInputModes()
    {
        // Act
        var card = AgentCards.CreateCoordinatorAgentCard(TestBaseUrl);

        // Assert
        Assert.NotNull(card.DefaultInputModes);
        Assert.Contains("text/plain", card.DefaultInputModes, StringComparer.Ordinal);
    }

    /// <summary>
    /// Verifies that CreateCoordinatorAgentCard has default output modes.
    /// </summary>
    [Fact]
    public void CreateCoordinatorAgentCardHasDefaultOutputModes()
    {
        // Act
        var card = AgentCards.CreateCoordinatorAgentCard(TestBaseUrl);

        // Assert
        Assert.NotNull(card.DefaultOutputModes);
        Assert.Contains("text/plain", card.DefaultOutputModes, StringComparer.Ordinal);
    }

    /// <summary>
    /// Verifies that CreateCoordinatorAgentCard capabilities are correctly set.
    /// </summary>
    [Fact]
    public void CreateCoordinatorAgentCardCapabilitiesAreCorrect()
    {
        // Act
        var card = AgentCards.CreateCoordinatorAgentCard(TestBaseUrl);
        var capabilities = card.Capabilities;

        // Assert
        Assert.False(capabilities.Streaming);
        Assert.False(capabilities.PushNotifications);
        Assert.Equal("JSONRPC", Assert.Single(card.SupportedInterfaces).ProtocolBinding);
    }

    /// <summary>
    /// Verifies that both cards work with HTTPS URL.
    /// </summary>
    [Fact]
    public void CardsWorkWithHttpsUrl()
    {
        // Arrange
        var httpsUrl = new Uri("https://api.example.com/agents/");

        // Act
        var researchCard = AgentCards.CreateResearchAgentCard(httpsUrl);
        var coordinatorCard = AgentCards.CreateCoordinatorAgentCard(httpsUrl);

        // Assert
        Assert.Equal(httpsUrl.ToString(), Assert.Single(researchCard.SupportedInterfaces).Url);
        Assert.Equal(httpsUrl.ToString(), Assert.Single(coordinatorCard.SupportedInterfaces).Url);
    }

    /// <summary>
    /// Verifies that both cards work with complex URL with port.
    /// </summary>
    [Fact]
    public void CardsWorkWithComplexUrl()
    {
        // Arrange
        var complexUrl = new Uri("http://localhost:7071/api/agents/");

        // Act
        var researchCard = AgentCards.CreateResearchAgentCard(complexUrl);
        var coordinatorCard = AgentCards.CreateCoordinatorAgentCard(complexUrl);

        // Assert
        Assert.Equal(complexUrl.ToString(), Assert.Single(researchCard.SupportedInterfaces).Url);
        Assert.Equal(complexUrl.ToString(), Assert.Single(coordinatorCard.SupportedInterfaces).Url);
    }

    /// <summary>
    /// Verifies that skill examples contain expected content.
    /// </summary>
    [Fact]
    public void SkillExamplesContainExpectedContent()
    {
        // Act
        var card = AgentCards.CreateResearchAgentCard(TestBaseUrl);
        var skill = card.Skills[0];

        // Assert
        Assert.NotNull(skill.Examples);
        var examples = skill.Examples.ToList();
        Assert.Contains(examples, e => e.Contains("AI", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Verifies that coordinator skill examples contain expected content.
    /// </summary>
    [Fact]
    public void CoordinatorSkillExamplesContainExpectedContent()
    {
        // Act
        var card = AgentCards.CreateCoordinatorAgentCard(TestBaseUrl);
        var orchestrationSkill = card.Skills.First(s => string.Equals(s.Id, "task-orchestration", StringComparison.Ordinal));
        var fileSkill = card.Skills.First(s => string.Equals(s.Id, "file-operations", StringComparison.Ordinal));

        // Assert
        Assert.NotNull(orchestrationSkill.Examples);
        Assert.NotEmpty(orchestrationSkill.Examples);
        Assert.NotNull(fileSkill.Examples);
        Assert.NotEmpty(fileSkill.Examples);
    }
}
