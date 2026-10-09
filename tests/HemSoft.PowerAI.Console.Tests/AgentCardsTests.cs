// <copyright file="AgentCardsTests.cs" company="HemSoft">
// Copyright © 2025 HemSoft
// </copyright>

namespace HemSoft.PowerAI.Console.Tests;

using HemSoft.PowerAI.Console.Agents;

/// <summary>
/// Tests for the <see cref="AgentCards"/> class.
/// </summary>
public class AgentCardsTests
{
    private static readonly Uri TestBaseUri = new("http://localhost:5000/");

    /// <summary>
    /// Verifies that CreateResearchAgentCard returns a valid card with correct name.
    /// </summary>
    [Fact]
    public void CreateResearchAgentCardReturnsCardWithCorrectName()
    {
        // Act
        var card = AgentCards.CreateResearchAgentCard(TestBaseUri);

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
        var card = AgentCards.CreateResearchAgentCard(TestBaseUri);

        // Assert
        Assert.Equal(TestBaseUri.ToString(), Assert.Single(card.SupportedInterfaces).Url);
    }

    /// <summary>
    /// Verifies that CreateResearchAgentCard has a non-empty description.
    /// </summary>
    [Fact]
    public void CreateResearchAgentCardHasDescription()
    {
        // Act
        var card = AgentCards.CreateResearchAgentCard(TestBaseUri);

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
        var card = AgentCards.CreateResearchAgentCard(TestBaseUri);

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
        var card = AgentCards.CreateResearchAgentCard(TestBaseUri);

        // Assert
        Assert.NotNull(card.Capabilities);
    }

    /// <summary>
    /// Verifies that CreateCoordinatorAgentCard returns a valid card with correct name.
    /// </summary>
    [Fact]
    public void CreateCoordinatorAgentCardReturnsCardWithCorrectName()
    {
        // Act
        var card = AgentCards.CreateCoordinatorAgentCard(TestBaseUri);

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
        var card = AgentCards.CreateCoordinatorAgentCard(TestBaseUri);

        // Assert
        Assert.Equal(TestBaseUri.ToString(), Assert.Single(card.SupportedInterfaces).Url);
    }

    /// <summary>
    /// Verifies that CreateCoordinatorAgentCard has a non-empty description.
    /// </summary>
    [Fact]
    public void CreateCoordinatorAgentCardHasDescription()
    {
        // Act
        var card = AgentCards.CreateCoordinatorAgentCard(TestBaseUri);

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
        var card = AgentCards.CreateCoordinatorAgentCard(TestBaseUri);

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
        var card = AgentCards.CreateCoordinatorAgentCard(TestBaseUri);

        // Assert
        Assert.NotNull(card.Capabilities);
    }

    /// <summary>
    /// Verifies that both cards have version information.
    /// </summary>
    [Fact]
    public void BothCardsHaveVersionInformation()
    {
        // Act
        var researchCard = AgentCards.CreateResearchAgentCard(TestBaseUri);
        var coordinatorCard = AgentCards.CreateCoordinatorAgentCard(TestBaseUri);

        // Assert
        Assert.NotNull(researchCard.Version);
        Assert.NotEmpty(researchCard.Version);
        Assert.NotNull(coordinatorCard.Version);
        Assert.NotEmpty(coordinatorCard.Version);
    }

    /// <summary>
    /// Verifies that both cards have protocol version information.
    /// </summary>
    [Fact]
    public void BothCardsHaveProtocolVersionInformation()
    {
        // Act
        var researchCard = AgentCards.CreateResearchAgentCard(TestBaseUri);
        var coordinatorCard = AgentCards.CreateCoordinatorAgentCard(TestBaseUri);

        // Assert
        Assert.NotNull(Assert.Single(researchCard.SupportedInterfaces).ProtocolVersion);
        Assert.NotEmpty(Assert.Single(researchCard.SupportedInterfaces).ProtocolVersion);
        Assert.NotNull(Assert.Single(coordinatorCard.SupportedInterfaces).ProtocolVersion);
        Assert.NotEmpty(Assert.Single(coordinatorCard.SupportedInterfaces).ProtocolVersion);
    }

    /// <summary>
    /// Verifies that research agent skill has examples.
    /// </summary>
    [Fact]
    public void ResearchAgentSkillHasExamples()
    {
        // Act
        var card = AgentCards.CreateResearchAgentCard(TestBaseUri);
        var skill = card.Skills[0];

        // Assert
        Assert.NotNull(skill.Examples);
        Assert.NotEmpty(skill.Examples);
    }

    /// <summary>
    /// Verifies that coordinator agent has multiple skills.
    /// </summary>
    [Fact]
    public void CoordinatorAgentHasMultipleSkills()
    {
        // Act
        var card = AgentCards.CreateCoordinatorAgentCard(TestBaseUri);

        // Assert
        Assert.True(card.Skills.Count > 1);
    }
}
