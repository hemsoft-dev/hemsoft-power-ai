// <copyright file="SpamCleanupAgentTests.cs" company="HemSoft">
// Copyright © 2025 HemSoft
// </copyright>

namespace HemSoft.PowerAI.Console.Tests;

using System.Reflection;

using HemSoft.PowerAI.Console.Agents;
using HemSoft.PowerAI.Console.Configuration;

/// <summary>
/// Unit tests for <see cref="SpamCleanupAgent"/>.
/// </summary>
public class SpamCleanupAgentTests : IDisposable
{
    private readonly string testDirectory;
    private readonly SpamFilterSettings settings;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SpamCleanupAgentTests"/> class.
    /// </summary>
    public SpamCleanupAgentTests()
    {
        this.testDirectory = Path.Combine(Path.GetTempPath(), "SpamCleanupAgentTests_" + Guid.NewGuid().ToString("N")[..8]);
        _ = Directory.CreateDirectory(this.testDirectory);

        this.settings = new SpamFilterSettings
        {
            HumanReviewFilePath = Path.Combine(this.testDirectory, "human_review.json"),
            SpamDomainsFilePath = Path.Combine(this.testDirectory, "spam_domains.json"),
            SpamCandidatesFilePath = Path.Combine(this.testDirectory, "spam_candidates.json"),
        };
    }

    /// <summary>
    /// Tests that constructor creates an agent without throwing.
    /// </summary>
    [Fact]
    public void ConstructorCreatesAgentWithoutThrowing()
    {
        // Act & Assert
        using var agent = new SpamCleanupAgent(this.settings);
        Assert.NotNull(agent);
    }

    /// <summary>
    /// Tests that Dispose can be called multiple times without throwing.
    /// </summary>
    [Fact]
    public void DisposeCanBeCalledMultipleTimes()
    {
        // Arrange
        using var agent = new SpamCleanupAgent(this.settings);

        // Act
        var exception = Record.Exception(() =>
        {
            agent.Dispose();
            agent.Dispose();
        });

        // Assert
        Assert.Null(exception);
    }

    /// <summary>
    /// Tests that CleanupStats initializes with zero values.
    /// </summary>
    [Fact]
    public void CleanupStatsInitializesWithZeroValues()
    {
        // Arrange
        var statsType = typeof(SpamCleanupAgent).GetNestedType("CleanupStats", BindingFlags.NonPublic);
        Assert.NotNull(statsType);

        // Act
        var stats = Activator.CreateInstance(statsType);
        Assert.NotNull(stats);

        // Assert
        var domainsProcessed = statsType.GetProperty("DomainsProcessed")?.GetValue(stats);
        var totalEmailsMoved = statsType.GetProperty("TotalEmailsMoved")?.GetValue(stats);
        var totalEmailsDeleted = statsType.GetProperty("TotalEmailsDeleted")?.GetValue(stats);

        Assert.Equal(0, domainsProcessed);
        Assert.Equal(0, totalEmailsMoved);
        Assert.Equal(0, totalEmailsDeleted);
    }

    /// <summary>
    /// Tests that CleanupStats properties can be set.
    /// </summary>
    [Fact]
    public void CleanupStatsPropertiesCanBeSet()
    {
        // Arrange
        var statsType = typeof(SpamCleanupAgent).GetNestedType("CleanupStats", BindingFlags.NonPublic);
        Assert.NotNull(statsType);

        var stats = Activator.CreateInstance(statsType);
        Assert.NotNull(stats);

        // Act
        statsType.GetProperty("DomainsProcessed")?.SetValue(stats, 25);
        statsType.GetProperty("TotalEmailsMoved")?.SetValue(stats, 150);
        statsType.GetProperty("TotalEmailsDeleted")?.SetValue(stats, 10);

        // Assert
        Assert.Equal(25, statsType.GetProperty("DomainsProcessed")?.GetValue(stats));
        Assert.Equal(150, statsType.GetProperty("TotalEmailsMoved")?.GetValue(stats));
        Assert.Equal(10, statsType.GetProperty("TotalEmailsDeleted")?.GetValue(stats));
    }

    /// <summary>
    /// Tests that CleanupStats can be incremented.
    /// </summary>
    [Fact]
    public void CleanupStatsCanBeIncremented()
    {
        // Arrange
        var statsType = typeof(SpamCleanupAgent).GetNestedType("CleanupStats", BindingFlags.NonPublic);
        Assert.NotNull(statsType);

        var stats = Activator.CreateInstance(statsType);
        Assert.NotNull(stats);

        // Act - Simulate incremental updates
        var domainsProp = statsType.GetProperty("DomainsProcessed");
        var movedProp = statsType.GetProperty("TotalEmailsMoved");
        var deletedProp = statsType.GetProperty("TotalEmailsDeleted");

        domainsProp?.SetValue(stats, (int)(domainsProp.GetValue(stats) ?? 0) + 1);
        domainsProp?.SetValue(stats, (int)(domainsProp.GetValue(stats) ?? 0) + 1);
        domainsProp?.SetValue(stats, (int)(domainsProp.GetValue(stats) ?? 0) + 1);

        movedProp?.SetValue(stats, (int)(movedProp.GetValue(stats) ?? 0) + 5);
        movedProp?.SetValue(stats, (int)(movedProp.GetValue(stats) ?? 0) + 3);

        deletedProp?.SetValue(stats, (int)(deletedProp.GetValue(stats) ?? 0) + 2);

        // Assert
        Assert.Equal(3, domainsProp?.GetValue(stats));
        Assert.Equal(8, movedProp?.GetValue(stats));
        Assert.Equal(2, deletedProp?.GetValue(stats));
    }

    /// <summary>
    /// Tests that CleanupStats handles large values.
    /// </summary>
    [Fact]
    public void CleanupStatsHandlesLargeValues()
    {
        // Arrange
        var statsType = typeof(SpamCleanupAgent).GetNestedType("CleanupStats", BindingFlags.NonPublic);
        Assert.NotNull(statsType);

        var stats = Activator.CreateInstance(statsType);
        Assert.NotNull(stats);

        // Act - Set large values
        statsType.GetProperty("DomainsProcessed")?.SetValue(stats, 100_000);
        statsType.GetProperty("TotalEmailsMoved")?.SetValue(stats, 5_000_000);
        statsType.GetProperty("TotalEmailsDeleted")?.SetValue(stats, 50_000);

        // Assert
        Assert.Equal(100_000, statsType.GetProperty("DomainsProcessed")?.GetValue(stats));
        Assert.Equal(5_000_000, statsType.GetProperty("TotalEmailsMoved")?.GetValue(stats));
        Assert.Equal(50_000, statsType.GetProperty("TotalEmailsDeleted")?.GetValue(stats));
    }

    /// <summary>
    /// Tests that agent handles null settings gracefully.
    /// </summary>
    [Fact]
    public void ConstructorHandlesNullSettings() =>
        Assert.Throws<NullReferenceException>(() =>
        {
            using var agent = new SpamCleanupAgent(null!);
        });

    /// <summary>
    /// Tests that CleanupStats can be reset to zero.
    /// </summary>
    [Fact]
    public void CleanupStatsCanBeResetToZero()
    {
        // Arrange
        var statsType = typeof(SpamCleanupAgent).GetNestedType("CleanupStats", BindingFlags.NonPublic);
        Assert.NotNull(statsType);

        var stats = Activator.CreateInstance(statsType);
        Assert.NotNull(stats);

        // Set to non-zero values first
        statsType.GetProperty("DomainsProcessed")?.SetValue(stats, 100);
        statsType.GetProperty("TotalEmailsMoved")?.SetValue(stats, 500);
        statsType.GetProperty("TotalEmailsDeleted")?.SetValue(stats, 50);

        // Act - reset to zero
        statsType.GetProperty("DomainsProcessed")?.SetValue(stats, 0);
        statsType.GetProperty("TotalEmailsMoved")?.SetValue(stats, 0);
        statsType.GetProperty("TotalEmailsDeleted")?.SetValue(stats, 0);

        // Assert
        Assert.Equal(0, statsType.GetProperty("DomainsProcessed")?.GetValue(stats));
        Assert.Equal(0, statsType.GetProperty("TotalEmailsMoved")?.GetValue(stats));
        Assert.Equal(0, statsType.GetProperty("TotalEmailsDeleted")?.GetValue(stats));
    }

    /// <summary>
    /// Tests that CleanupStats properties have correct types.
    /// </summary>
    [Fact]
    public void CleanupStatsPropertiesHaveCorrectTypes()
    {
        // Arrange
        var statsType = typeof(SpamCleanupAgent).GetNestedType("CleanupStats", BindingFlags.NonPublic);
        Assert.NotNull(statsType);

        // Act & Assert
        var domainsProcessedProp = statsType.GetProperty("DomainsProcessed");
        var totalEmailsMovedProp = statsType.GetProperty("TotalEmailsMoved");
        var totalEmailsDeletedProp = statsType.GetProperty("TotalEmailsDeleted");

        Assert.NotNull(domainsProcessedProp);
        Assert.NotNull(totalEmailsMovedProp);
        Assert.NotNull(totalEmailsDeletedProp);

        Assert.Equal(typeof(int), domainsProcessedProp.PropertyType);
        Assert.Equal(typeof(int), totalEmailsMovedProp.PropertyType);
        Assert.Equal(typeof(int), totalEmailsDeletedProp.PropertyType);
    }

    /// <summary>
    /// Tests that CleanupStats is a sealed class.
    /// </summary>
    [Fact]
    public void CleanupStatsIsSealedClass()
    {
        // Arrange
        var statsType = typeof(SpamCleanupAgent).GetNestedType("CleanupStats", BindingFlags.NonPublic);
        Assert.NotNull(statsType);

        // Assert
        Assert.True(statsType.IsSealed);
        Assert.True(statsType.IsClass);
    }

    /// <summary>
    /// Tests that multiple instances of CleanupStats are independent.
    /// </summary>
    [Fact]
    public void MultipleCleanupStatsInstancesAreIndependent()
    {
        // Arrange
        var statsType = typeof(SpamCleanupAgent).GetNestedType("CleanupStats", BindingFlags.NonPublic);
        Assert.NotNull(statsType);

        var stats1 = Activator.CreateInstance(statsType);
        var stats2 = Activator.CreateInstance(statsType);
        Assert.NotNull(stats1);
        Assert.NotNull(stats2);

        // Act - modify stats1 only
        statsType.GetProperty("DomainsProcessed")?.SetValue(stats1, 100);
        statsType.GetProperty("TotalEmailsMoved")?.SetValue(stats1, 500);
        statsType.GetProperty("TotalEmailsDeleted")?.SetValue(stats1, 50);

        // Assert - stats2 should still be zero
        Assert.Equal(100, statsType.GetProperty("DomainsProcessed")?.GetValue(stats1));
        Assert.Equal(0, statsType.GetProperty("DomainsProcessed")?.GetValue(stats2));
        Assert.Equal(50, statsType.GetProperty("TotalEmailsDeleted")?.GetValue(stats1));
        Assert.Equal(0, statsType.GetProperty("TotalEmailsDeleted")?.GetValue(stats2));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes managed resources.
    /// </summary>
    /// <param name="disposing">Whether to dispose managed resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (this.disposed)
        {
            return;
        }

        if (disposing)
        {
            try
            {
                if (Directory.Exists(this.testDirectory))
                {
                    Directory.Delete(this.testDirectory, recursive: true);
                }
            }
            catch (IOException)
            {
                // Ignore cleanup errors in tests
            }
        }

        this.disposed = true;
    }
}
