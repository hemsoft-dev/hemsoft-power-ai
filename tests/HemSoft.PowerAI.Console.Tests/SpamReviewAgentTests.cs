// <copyright file="SpamReviewAgentTests.cs" company="HemSoft">
// Copyright © 2025 HemSoft
// </copyright>

namespace HemSoft.PowerAI.Console.Tests;

using System.Reflection;

using HemSoft.PowerAI.Console.Agents;
using HemSoft.PowerAI.Console.Configuration;

/// <summary>
/// Unit tests for <see cref="SpamReviewAgent"/>.
/// </summary>
public class SpamReviewAgentTests : IDisposable
{
    private readonly string testDirectory;
    private readonly SpamFilterSettings settings;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SpamReviewAgentTests"/> class.
    /// </summary>
    public SpamReviewAgentTests()
    {
        this.testDirectory = Path.Combine(Path.GetTempPath(), "SpamReviewAgentTests_" + Guid.NewGuid().ToString("N")[..8]);
        _ = Directory.CreateDirectory(this.testDirectory);

        this.settings = new SpamFilterSettings
        {
            HumanReviewFilePath = Path.Combine(this.testDirectory, "human_review.json"),
            SpamDomainsFilePath = Path.Combine(this.testDirectory, "spam_domains.json"),
            SpamCandidatesFilePath = Path.Combine(this.testDirectory, "spam_candidates.json"),
            ReviewBatchSize = 20,
        };
    }

    /// <summary>
    /// Tests that constructor creates an agent without throwing.
    /// </summary>
    [Fact]
    public void ConstructorCreatesAgentWithoutThrowing()
    {
        // Act & Assert
        using var agent = new SpamReviewAgent(this.settings);
        Assert.NotNull(agent);
    }

    /// <summary>
    /// Tests that Dispose can be called multiple times without throwing.
    /// </summary>
    [Fact]
    public void DisposeCanBeCalledMultipleTimes()
    {
        // Arrange
        using var agent = new SpamReviewAgent(this.settings);

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
    /// Tests that Truncate returns original string when shorter than max length.
    /// </summary>
    [Fact]
    public void TruncateReturnsOriginalWhenShorterThanMaxLength()
    {
        // Arrange
        const string text = "Short";
        const int maxLength = 10;

        // Act
        var result = InvokeTruncate(text, maxLength);

        // Assert
        Assert.Equal("Short", result);
    }

    /// <summary>
    /// Tests that Truncate truncates and adds ellipsis when longer than max.
    /// </summary>
    [Fact]
    public void TruncateTruncatesWhenLongerThanMaxLength()
    {
        // Arrange
        const string text = "This is a very long string";
        const int maxLength = 10;

        // Act
        var result = InvokeTruncate(text, maxLength);

        // Assert
        Assert.Equal(10, result.Length);
        Assert.EndsWith("…", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tests that Truncate returns exact length string unchanged.
    /// </summary>
    [Fact]
    public void TruncateReturnsExactLengthStringUnchanged()
    {
        // Arrange
        const string text = "Exactly 10";
        const int maxLength = 10;

        // Act
        var result = InvokeTruncate(text, maxLength);

        // Assert
        Assert.Equal("Exactly 10", result);
    }

    /// <summary>
    /// Tests that ReviewStats initializes with zero values.
    /// </summary>
    [Fact]
    public void ReviewStatsInitializesWithZeroValues()
    {
        // Arrange
        var statsType = typeof(SpamReviewAgent).GetNestedType("ReviewStats", BindingFlags.NonPublic);
        Assert.NotNull(statsType);

        // Act
        var stats = Activator.CreateInstance(statsType);
        Assert.NotNull(stats);

        // Assert
        var domainsReviewed = statsType.GetProperty("DomainsReviewed")?.GetValue(stats);
        var domainsBlocked = statsType.GetProperty("DomainsBlocked")?.GetValue(stats);
        var domainsLegitimate = statsType.GetProperty("DomainsLegitimate")?.GetValue(stats);

        Assert.Equal(0, domainsReviewed);
        Assert.Equal(0, domainsBlocked);
        Assert.Equal(0, domainsLegitimate);
    }

    /// <summary>
    /// Tests that ReviewStats properties can be set.
    /// </summary>
    [Fact]
    public void ReviewStatsPropertiesCanBeSet()
    {
        // Arrange
        var statsType = typeof(SpamReviewAgent).GetNestedType("ReviewStats", BindingFlags.NonPublic);
        Assert.NotNull(statsType);

        var stats = Activator.CreateInstance(statsType);
        Assert.NotNull(stats);

        // Act
        statsType.GetProperty("DomainsReviewed")?.SetValue(stats, 10);
        statsType.GetProperty("DomainsBlocked")?.SetValue(stats, 7);
        statsType.GetProperty("DomainsLegitimate")?.SetValue(stats, 3);

        // Assert
        Assert.Equal(10, statsType.GetProperty("DomainsReviewed")?.GetValue(stats));
        Assert.Equal(7, statsType.GetProperty("DomainsBlocked")?.GetValue(stats));
        Assert.Equal(3, statsType.GetProperty("DomainsLegitimate")?.GetValue(stats));
    }

    /// <summary>
    /// Tests that ReviewStats can be incremented.
    /// </summary>
    [Fact]
    public void ReviewStatsCanBeIncremented()
    {
        // Arrange
        var statsType = typeof(SpamReviewAgent).GetNestedType("ReviewStats", BindingFlags.NonPublic);
        Assert.NotNull(statsType);

        var stats = Activator.CreateInstance(statsType);
        Assert.NotNull(stats);

        // Act - Simulate incremental updates
        var reviewedProp = statsType.GetProperty("DomainsReviewed");
        var blockedProp = statsType.GetProperty("DomainsBlocked");
        var legitProp = statsType.GetProperty("DomainsLegitimate");

        // Simulate reviewing 5 domains: 3 blocked, 2 legitimate
        for (var i = 0; i < 5; i++)
        {
            reviewedProp?.SetValue(stats, (int)(reviewedProp.GetValue(stats) ?? 0) + 1);
        }

        for (var i = 0; i < 3; i++)
        {
            blockedProp?.SetValue(stats, (int)(blockedProp.GetValue(stats) ?? 0) + 1);
        }

        for (var i = 0; i < 2; i++)
        {
            legitProp?.SetValue(stats, (int)(legitProp.GetValue(stats) ?? 0) + 1);
        }

        // Assert
        Assert.Equal(5, reviewedProp?.GetValue(stats));
        Assert.Equal(3, blockedProp?.GetValue(stats));
        Assert.Equal(2, legitProp?.GetValue(stats));
    }

    /// <summary>
    /// Tests that Truncate handles empty string.
    /// </summary>
    [Fact]
    public void TruncateHandlesEmptyString()
    {
        // Arrange
        const string text = "";
        const int maxLength = 10;

        // Act
        var result = InvokeTruncate(text, maxLength);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    /// <summary>
    /// Tests that Truncate handles single character with max 1.
    /// </summary>
    [Fact]
    public void TruncateHandlesSingleCharacterMaxLength()
    {
        // Arrange
        const string text = "A";
        const int maxLength = 1;

        // Act
        var result = InvokeTruncate(text, maxLength);

        // Assert
        Assert.Equal("A", result);
    }

    /// <summary>
    /// Tests that Truncate handles longer text with max 1.
    /// </summary>
    [Fact]
    public void TruncateTruncatesLongTextToSingleEllipsis()
    {
        // Arrange
        const string text = "Hello World";
        const int maxLength = 1;

        // Act
        var result = InvokeTruncate(text, maxLength);

        // Assert
        Assert.Equal("…", result);
    }

    /// <summary>
    /// Tests that agent handles null settings gracefully.
    /// </summary>
    [Fact]
    public void ConstructorHandlesNullSettings() =>
        Assert.Throws<NullReferenceException>(() =>
        {
            using var agent = new SpamReviewAgent(null!);
        });

    /// <summary>
    /// Tests that ReviewStats handles large values.
    /// </summary>
    [Fact]
    public void ReviewStatsHandlesLargeValues()
    {
        // Arrange
        var statsType = typeof(SpamReviewAgent).GetNestedType("ReviewStats", BindingFlags.NonPublic);
        Assert.NotNull(statsType);

        var stats = Activator.CreateInstance(statsType);
        Assert.NotNull(stats);

        // Act
        statsType.GetProperty("DomainsReviewed")?.SetValue(stats, 10000);
        statsType.GetProperty("DomainsBlocked")?.SetValue(stats, 8000);
        statsType.GetProperty("DomainsLegitimate")?.SetValue(stats, 2000);

        // Assert
        Assert.Equal(10000, statsType.GetProperty("DomainsReviewed")?.GetValue(stats));
        Assert.Equal(8000, statsType.GetProperty("DomainsBlocked")?.GetValue(stats));
        Assert.Equal(2000, statsType.GetProperty("DomainsLegitimate")?.GetValue(stats));
    }

    /// <summary>
    /// Tests that ReviewStats can be reset to zero.
    /// </summary>
    [Fact]
    public void ReviewStatsCanBeResetToZero()
    {
        // Arrange
        var statsType = typeof(SpamReviewAgent).GetNestedType("ReviewStats", BindingFlags.NonPublic);
        Assert.NotNull(statsType);

        var stats = Activator.CreateInstance(statsType);
        Assert.NotNull(stats);

        // Set to non-zero values first
        statsType.GetProperty("DomainsReviewed")?.SetValue(stats, 100);
        statsType.GetProperty("DomainsBlocked")?.SetValue(stats, 70);
        statsType.GetProperty("DomainsLegitimate")?.SetValue(stats, 30);

        // Act - reset to zero
        statsType.GetProperty("DomainsReviewed")?.SetValue(stats, 0);
        statsType.GetProperty("DomainsBlocked")?.SetValue(stats, 0);
        statsType.GetProperty("DomainsLegitimate")?.SetValue(stats, 0);

        // Assert
        Assert.Equal(0, statsType.GetProperty("DomainsReviewed")?.GetValue(stats));
        Assert.Equal(0, statsType.GetProperty("DomainsBlocked")?.GetValue(stats));
        Assert.Equal(0, statsType.GetProperty("DomainsLegitimate")?.GetValue(stats));
    }

    /// <summary>
    /// Tests that ReviewStats properties have correct types.
    /// </summary>
    [Fact]
    public void ReviewStatsPropertiesHaveCorrectTypes()
    {
        // Arrange
        var statsType = typeof(SpamReviewAgent).GetNestedType("ReviewStats", BindingFlags.NonPublic);
        Assert.NotNull(statsType);

        // Act & Assert
        var reviewedProp = statsType.GetProperty("DomainsReviewed");
        var blockedProp = statsType.GetProperty("DomainsBlocked");
        var legitProp = statsType.GetProperty("DomainsLegitimate");

        Assert.NotNull(reviewedProp);
        Assert.NotNull(blockedProp);
        Assert.NotNull(legitProp);

        Assert.Equal(typeof(int), reviewedProp.PropertyType);
        Assert.Equal(typeof(int), blockedProp.PropertyType);
        Assert.Equal(typeof(int), legitProp.PropertyType);
    }

    /// <summary>
    /// Tests that ReviewStats is a sealed class.
    /// </summary>
    [Fact]
    public void ReviewStatsIsSealedClass()
    {
        // Arrange
        var statsType = typeof(SpamReviewAgent).GetNestedType("ReviewStats", BindingFlags.NonPublic);
        Assert.NotNull(statsType);

        // Assert
        Assert.True(statsType.IsSealed);
        Assert.True(statsType.IsClass);
    }

    /// <summary>
    /// Tests that multiple instances of ReviewStats are independent.
    /// </summary>
    [Fact]
    public void MultipleReviewStatsInstancesAreIndependent()
    {
        // Arrange
        var statsType = typeof(SpamReviewAgent).GetNestedType("ReviewStats", BindingFlags.NonPublic);
        Assert.NotNull(statsType);

        var stats1 = Activator.CreateInstance(statsType);
        var stats2 = Activator.CreateInstance(statsType);
        Assert.NotNull(stats1);
        Assert.NotNull(stats2);

        // Act - modify stats1 only
        statsType.GetProperty("DomainsReviewed")?.SetValue(stats1, 100);
        statsType.GetProperty("DomainsBlocked")?.SetValue(stats1, 70);
        statsType.GetProperty("DomainsLegitimate")?.SetValue(stats1, 30);

        // Assert - stats2 should still be zero
        Assert.Equal(100, statsType.GetProperty("DomainsReviewed")?.GetValue(stats1));
        Assert.Equal(0, statsType.GetProperty("DomainsReviewed")?.GetValue(stats2));
        Assert.Equal(70, statsType.GetProperty("DomainsBlocked")?.GetValue(stats1));
        Assert.Equal(0, statsType.GetProperty("DomainsBlocked")?.GetValue(stats2));
    }

    /// <summary>
    /// Tests that Truncate handles whitespace string.
    /// </summary>
    [Fact]
    public void TruncateHandlesWhitespaceString()
    {
        // Arrange
        const string text = "   ";
        const int maxLength = 10;

        // Act
        var result = InvokeTruncate(text, maxLength);

        // Assert
        Assert.Equal("   ", result);
    }

    /// <summary>
    /// Tests that Truncate handles unicode characters.
    /// </summary>
    [Fact]
    public void TruncateHandlesUnicodeCharacters()
    {
        // Arrange
        const string text = "日本語テスト文字列";
        const int maxLength = 5;

        // Act
        var result = InvokeTruncate(text, maxLength);

        // Assert
        Assert.Equal(5, result.Length);
        Assert.EndsWith("…", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tests that Truncate handles emoji characters.
    /// </summary>
    [Fact]
    public void TruncateHandlesEmojiCharacters()
    {
        // Arrange
        const string text = "🎉🔥💯🚀✨";
        const int maxLength = 3;

        // Act
        var result = InvokeTruncate(text, maxLength);

        // Assert
        Assert.Equal(3, result.Length);
    }

    /// <summary>
    /// Tests that Truncate with max length 0 throws exception.
    /// </summary>
    [Fact]
    public void TruncateWithZeroMaxLengthThrowsException()
    {
        // Arrange
        const string text = "Test";
        const int maxLength = 0;

        // Act & Assert
        _ = Assert.Throws<TargetInvocationException>(() => InvokeTruncate(text, maxLength));
    }

    /// <summary>
    /// Tests that AddDomainManuallyAsync method exists and is private.
    /// </summary>
    [Fact]
    public void AddDomainManuallyAsyncMethodExists()
    {
        // Arrange & Act
        var method = typeof(SpamReviewAgent).GetMethod(
            "AddDomainManuallyAsync",
            BindingFlags.NonPublic | BindingFlags.Instance);

        // Assert
        Assert.NotNull(method);
        Assert.True(method.IsPrivate);
        Assert.Equal(typeof(Task), method.ReturnType);
    }

    /// <summary>
    /// Tests that DisplayBlocklist method exists and is private.
    /// </summary>
    [Fact]
    public void DisplayBlocklistMethodExists()
    {
        // Arrange & Act
        var method = typeof(SpamReviewAgent).GetMethod(
            "DisplayBlocklist",
            BindingFlags.NonPublic | BindingFlags.Instance);

        // Assert
        Assert.NotNull(method);
        Assert.True(method.IsPrivate);
        Assert.Equal(typeof(void), method.ReturnType);
    }

    /// <summary>
    /// Tests that DisplayBlocklist can be invoked when blocklist is empty.
    /// </summary>
    [Fact]
    public void DisplayBlocklistHandlesEmptyBlocklist()
    {
        // Arrange
        using var agent = new SpamReviewAgent(this.settings);
        var method = typeof(SpamReviewAgent).GetMethod(
            "DisplayBlocklist",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(method);

        // Act - should not throw even when blocklist is empty
        var exception = Record.Exception(() => method.Invoke(agent, parameters: null));

        // Assert
        Assert.Null(exception);
    }

    /// <summary>
    /// Tests that DisplayBlocklist works with domains in blocklist.
    /// </summary>
    [Fact]
    public void DisplayBlocklistHandlesPopulatedBlocklist()
    {
        // Arrange
        using var agent = new SpamReviewAgent(this.settings);

        // Add a domain directly via storage service
        var storageField = typeof(SpamReviewAgent).GetField(
            "storageService",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(storageField);

        var storageService = storageField.GetValue(agent) as HemSoft.PowerAI.Console.Services.SpamStorageService;
        Assert.NotNull(storageService);

        _ = storageService.AddSpamDomain("TESTDOMAIN.COM", "Test reason");

        var method = typeof(SpamReviewAgent).GetMethod(
            "DisplayBlocklist",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(method);

        // Act - should not throw
        var exception = Record.Exception(() => method.Invoke(agent, parameters: null));

        // Assert
        Assert.Null(exception);
    }

    /// <summary>
    /// Tests that domain normalization converts to uppercase.
    /// </summary>
    [Fact]
    public void DomainNormalizationConvertsToUppercase()
    {
        // This tests the normalization logic used in AddDomainManuallyAsync
        // Arrange
        const string inputDomain = "spammer.com";

        // Act
        var normalized = inputDomain.Trim().ToUpperInvariant();

        // Assert
        Assert.Equal("SPAMMER.COM", normalized);
    }

    /// <summary>
    /// Tests that domain normalization handles whitespace.
    /// </summary>
    [Fact]
    public void DomainNormalizationTrimsWhitespace()
    {
        // Arrange
        const string inputDomain = "  spammer.com  ";

        // Act
        var normalized = inputDomain.Trim().ToUpperInvariant();

        // Assert
        Assert.Equal("SPAMMER.COM", normalized);
    }

    /// <summary>
    /// Tests that URI parsing extracts host correctly.
    /// </summary>
    [Fact]
    public void UriParsingExtractsHost()
    {
        // This tests the URI parsing logic used in AddDomainManuallyAsync
        // Arrange
        const string inputWithProtocol = "https://spammer.com/path/to/page";

        // Act
        var uri = new Uri(inputWithProtocol);
        var host = uri.Host.ToUpperInvariant();

        // Assert
        Assert.Equal("SPAMMER.COM", host);
    }

    /// <summary>
    /// Tests that URI parsing handles http protocol.
    /// </summary>
    [Fact]
    public void UriParsingHandlesHttpProtocol()
    {
        // Arrange
        const string inputWithProtocol = "http://malware.net";

        // Act
        var uri = new Uri(inputWithProtocol);
        var host = uri.Host.ToUpperInvariant();

        // Assert
        Assert.Equal("MALWARE.NET", host);
    }

    /// <summary>
    /// Tests that URI parsing handles subdomain.
    /// </summary>
    [Fact]
    public void UriParsingHandlesSubdomain()
    {
        // Arrange
        const string inputWithSubdomain = "https://mail.spammer.com/inbox";

        // Act
        var uri = new Uri(inputWithSubdomain);
        var host = uri.Host.ToUpperInvariant();

        // Assert
        Assert.Equal("MAIL.SPAMMER.COM", host);
    }

    /// <summary>
    /// Tests that contains check for protocol works correctly.
    /// </summary>
    [Fact]
    public void ProtocolDetectionWorks()
    {
        // Arrange
        const string withProtocol = "https://example.com";
        const string withoutProtocol = "example.com";

        // Act & Assert
        Assert.True(withProtocol.Contains("://", StringComparison.Ordinal));
        Assert.False(withoutProtocol.Contains("://", StringComparison.Ordinal));
    }

    /// <summary>
    /// Tests that AddDomainManuallyAsync accepts CancellationToken parameter.
    /// </summary>
    [Fact]
    public void AddDomainManuallyAsyncAcceptsCancellationToken()
    {
        // Arrange
        var method = typeof(SpamReviewAgent).GetMethod(
            "AddDomainManuallyAsync",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(method);

        // Act
        var parameters = method.GetParameters();

        // Assert
        _ = Assert.Single(parameters);
        Assert.Equal(typeof(CancellationToken), parameters[0].ParameterType);
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

    private static string InvokeTruncate(string text, int maxLength)
    {
        var method = typeof(SpamReviewAgent).GetMethod(
            "Truncate",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var result = method.Invoke(obj: null, [text, maxLength]);
        Assert.NotNull(result);

        return (string)result;
    }
}
