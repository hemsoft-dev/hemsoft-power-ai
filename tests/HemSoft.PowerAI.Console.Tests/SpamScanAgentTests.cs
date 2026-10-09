// <copyright file="SpamScanAgentTests.cs" company="HemSoft">
// Copyright © 2025 HemSoft
// </copyright>

namespace HemSoft.PowerAI.Console.Tests;

using System.Reflection;

using HemSoft.PowerAI.Console.Agents;
using HemSoft.PowerAI.Console.Configuration;

/// <summary>
/// Unit tests for <see cref="SpamScanAgent"/>.
/// </summary>
public class SpamScanAgentTests : IDisposable
{
    private readonly string testDirectory;
    private readonly SpamFilterSettings settings;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SpamScanAgentTests"/> class.
    /// </summary>
    public SpamScanAgentTests()
    {
        this.testDirectory = Path.Combine(Path.GetTempPath(), "SpamScanAgentTests_" + Guid.NewGuid().ToString("N")[..8]);
        _ = Directory.CreateDirectory(this.testDirectory);

        this.settings = new SpamFilterSettings
        {
            HumanReviewFilePath = Path.Combine(this.testDirectory, "human_review.json"),
            SpamDomainsFilePath = Path.Combine(this.testDirectory, "spam_domains.json"),
            SpamCandidatesFilePath = Path.Combine(this.testDirectory, "spam_candidates.json"),
            BatchSize = 10,
            DelayBetweenBatchesSeconds = 1,
        };
    }

    /// <summary>
    /// Tests that constructor creates an agent without throwing.
    /// </summary>
    [Fact]
    public void ConstructorCreatesAgentWithoutThrowing()
    {
        // Act & Assert
        using var agent = new SpamScanAgent(this.settings);
        Assert.NotNull(agent);
    }

    /// <summary>
    /// Tests that Dispose can be called multiple times without throwing.
    /// </summary>
    [Fact]
    public void DisposeCanBeCalledMultipleTimes()
    {
        // Arrange
        using var agent = new SpamScanAgent(this.settings);

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
    /// Tests that ParseBatchResult detects empty inbox from "no emails" text.
    /// </summary>
    [Fact]
    public void ParseBatchResultDetectsEmptyInboxFromNoEmails()
    {
        // Arrange
        const string response = "There are no emails in the inbox to process.";

        // Act
        var result = InvokeParseBatchResult(response);

        // Assert
        Assert.True(result.InboxWasEmpty);
    }

    /// <summary>
    /// Tests that ParseBatchResult detects empty inbox from empty array.
    /// </summary>
    [Fact]
    public void ParseBatchResultDetectsEmptyInboxFromEmptyArray()
    {
        // Arrange
        const string response = "The inbox returned []. No messages to process.";

        // Act
        var result = InvokeParseBatchResult(response);

        // Assert
        Assert.True(result.InboxWasEmpty);
    }

    /// <summary>
    /// Tests that ParseBatchResult detects empty inbox from "inbox is empty" text.
    /// </summary>
    [Fact]
    public void ParseBatchResultDetectsEmptyInboxFromInboxIsEmpty()
    {
        // Arrange
        const string response = "The inbox is empty, nothing to scan.";

        // Act
        var result = InvokeParseBatchResult(response);

        // Assert
        Assert.True(result.InboxWasEmpty);
    }

    /// <summary>
    /// Tests that ParseBatchResult detects empty inbox from "0 emails in" text.
    /// </summary>
    [Fact]
    public void ParseBatchResultDetectsEmptyInboxFromZeroEmails()
    {
        // Arrange
        const string response = "Found 0 emails in the inbox.";

        // Act
        var result = InvokeParseBatchResult(response);

        // Assert
        Assert.True(result.InboxWasEmpty);
    }

    /// <summary>
    /// Tests that ParseBatchResult parses BATCH_STATS correctly.
    /// </summary>
    [Fact]
    public void ParseBatchResultParsesBatchStatsCorrectly()
    {
        // Arrange
        const string response = "Processing complete.\nBATCH_STATS: processed=10, skipped_known=3, skipped_pending=2, flagged=5";

        // Act
        var result = InvokeParseBatchResult(response);

        // Assert
        Assert.False(result.InboxWasEmpty);
        Assert.Equal(10, result.Processed);
        Assert.Equal(3, result.SkippedKnown);
        Assert.Equal(2, result.SkippedPending);
        Assert.Equal(5, result.Flagged);
    }

    /// <summary>
    /// Tests that ParseBatchResult handles BATCH_STATS case-insensitively.
    /// </summary>
    [Fact]
    public void ParseBatchResultHandlesBatchStatsCaseInsensitively()
    {
        // Arrange
        const string response = "batch_stats: PROCESSED=5, SKIPPED_KNOWN=1, SKIPPED_PENDING=0, FLAGGED=4";

        // Act
        var result = InvokeParseBatchResult(response);

        // Assert
        Assert.Equal(5, result.Processed);
        Assert.Equal(1, result.SkippedKnown);
        Assert.Equal(0, result.SkippedPending);
        Assert.Equal(4, result.Flagged);
    }

    /// <summary>
    /// Tests that ParseBatchResult handles text without BATCH_STATS.
    /// </summary>
    [Fact]
    public void ParseBatchResultHandlesTextWithoutBatchStats()
    {
        // Arrange
        const string response = "Completed processing emails successfully.";

        // Act
        var result = InvokeParseBatchResult(response);

        // Assert
        Assert.False(result.InboxWasEmpty);
        Assert.Equal(0, result.Processed);
        Assert.Equal(0, result.SkippedKnown);
        Assert.Equal(0, result.SkippedPending);
        Assert.Equal(0, result.Flagged);
    }

    /// <summary>
    /// Tests that ParseBatchResult handles BATCH_STATS with extra spaces.
    /// </summary>
    [Fact]
    public void ParseBatchResultHandlesBatchStatsWithExtraSpaces()
    {
        // Arrange
        const string response = "BATCH_STATS:   processed = 7 ,  skipped_known = 2 ,  skipped_pending = 1 ,  flagged = 4";

        // Act
        var result = InvokeParseBatchResult(response);

        // Assert
        Assert.Equal(7, result.Processed);
        Assert.Equal(2, result.SkippedKnown);
        Assert.Equal(1, result.SkippedPending);
        Assert.Equal(4, result.Flagged);
    }

    /// <summary>
    /// Tests that Truncate returns original string when shorter than max length.
    /// </summary>
    [Fact]
    public void TruncateReturnsOriginalWhenShorterThanMaxLength()
    {
        // Arrange
        const string text = "Short text";
        const int maxLength = 20;

        // Act
        var result = InvokeTruncate(text, maxLength);

        // Assert
        Assert.Equal("Short text", result);
    }

    /// <summary>
    /// Tests that Truncate truncates string when longer than max length.
    /// </summary>
    [Fact]
    public void TruncateTruncatesWhenLongerThanMaxLength()
    {
        // Arrange
        const string text = "This is a very long string that exceeds the maximum";
        const int maxLength = 15;

        // Act
        var result = InvokeTruncate(text, maxLength);

        // Assert
        Assert.Equal(15, result.Length);
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
    /// Tests that ScanResult initializes with required properties.
    /// </summary>
    [Fact]
    public void ScanResultInitializesWithRequiredProperties()
    {
        // Act
        var scanResult = new SpamScanAgent.ScanResult
        {
            Domain = "test.com",
            Subject = "Test Subject",
            Status = "Flagged",
        };

        // Assert
        Assert.Equal("test.com", scanResult.Domain);
        Assert.Equal("Test Subject", scanResult.Subject);
        Assert.Equal("Flagged", scanResult.Status);
        Assert.Null(scanResult.Reason);
    }

    /// <summary>
    /// Tests that ScanResult Reason property can be set.
    /// </summary>
    [Fact]
    public void ScanResultReasonCanBeSet()
    {
        // Act
        var scanResult = new SpamScanAgent.ScanResult
        {
            Domain = "test.com",
            Subject = "Test Subject",
            Status = "Flagged",
            Reason = "Suspicious content",
        };

        // Assert
        Assert.Equal("Suspicious content", scanResult.Reason);
    }

    /// <summary>
    /// Tests that ScanResult properties can be modified.
    /// </summary>
    [Fact]
    public void ScanResultPropertiesCanBeModified()
    {
        // Arrange
        var scanResult = new SpamScanAgent.ScanResult
        {
            Domain = "original.com",
            Subject = "Original Subject",
            Status = "Clean",
        };

        // Act
        scanResult.Domain = "modified.com";
        scanResult.Subject = "Modified Subject";
        scanResult.Status = "Flagged";
        scanResult.Reason = "New Reason";

        // Assert
        Assert.Equal("modified.com", scanResult.Domain);
        Assert.Equal("Modified Subject", scanResult.Subject);
        Assert.Equal("Flagged", scanResult.Status);
        Assert.Equal("New Reason", scanResult.Reason);
    }

    /// <summary>
    /// Tests ScanBatchResult properties via reflection.
    /// </summary>
    [Fact]
    public void ScanBatchResultPropertiesCanBeSet()
    {
        // Arrange
        var scanBatchResultType = typeof(SpamScanAgent).GetNestedType("ScanBatchResult", BindingFlags.NonPublic);
        Assert.NotNull(scanBatchResultType);

        var instance = Activator.CreateInstance(scanBatchResultType);
        Assert.NotNull(instance);

        // Act - set all properties
        scanBatchResultType.GetProperty("Processed")?.SetValue(instance, 10);
        scanBatchResultType.GetProperty("SkippedKnown")?.SetValue(instance, 5);
        scanBatchResultType.GetProperty("SkippedPending")?.SetValue(instance, 3);
        scanBatchResultType.GetProperty("Flagged")?.SetValue(instance, 2);
        scanBatchResultType.GetProperty("InboxWasEmpty")?.SetValue(instance, value: true);

        // Assert
        Assert.Equal(10, scanBatchResultType.GetProperty("Processed")?.GetValue(instance));
        Assert.Equal(5, scanBatchResultType.GetProperty("SkippedKnown")?.GetValue(instance));
        Assert.Equal(3, scanBatchResultType.GetProperty("SkippedPending")?.GetValue(instance));
        Assert.Equal(2, scanBatchResultType.GetProperty("Flagged")?.GetValue(instance));
        Assert.True((bool)(scanBatchResultType.GetProperty("InboxWasEmpty")?.GetValue(instance) ?? false));
    }

    /// <summary>
    /// Tests RunStats properties via reflection.
    /// </summary>
    [Fact]
    public void RunStatsPropertiesCanBeSet()
    {
        // Arrange
        var runStatsType = typeof(SpamScanAgent).GetNestedType("RunStats", BindingFlags.NonPublic);
        Assert.NotNull(runStatsType);

        var instance = Activator.CreateInstance(runStatsType);
        Assert.NotNull(instance);

        // Act - set all properties
        runStatsType.GetProperty("Iteration")?.SetValue(instance, 5);
        runStatsType.GetProperty("TotalProcessed")?.SetValue(instance, 100);
        runStatsType.GetProperty("TotalSkippedKnown")?.SetValue(instance, 25);
        runStatsType.GetProperty("TotalSkippedPending")?.SetValue(instance, 10);
        runStatsType.GetProperty("TotalFlagged")?.SetValue(instance, 15);

        // Assert
        Assert.Equal(5, runStatsType.GetProperty("Iteration")?.GetValue(instance));
        Assert.Equal(100, runStatsType.GetProperty("TotalProcessed")?.GetValue(instance));
        Assert.Equal(25, runStatsType.GetProperty("TotalSkippedKnown")?.GetValue(instance));
        Assert.Equal(10, runStatsType.GetProperty("TotalSkippedPending")?.GetValue(instance));
        Assert.Equal(15, runStatsType.GetProperty("TotalFlagged")?.GetValue(instance));
    }

    /// <summary>
    /// Tests RunStats initializes with zero values.
    /// </summary>
    [Fact]
    public void RunStatsInitializesWithZeroValues()
    {
        // Arrange
        var runStatsType = typeof(SpamScanAgent).GetNestedType("RunStats", BindingFlags.NonPublic);
        Assert.NotNull(runStatsType);

        // Act
        var instance = Activator.CreateInstance(runStatsType);
        Assert.NotNull(instance);

        // Assert
        Assert.Equal(0, runStatsType.GetProperty("Iteration")?.GetValue(instance));
        Assert.Equal(0, runStatsType.GetProperty("TotalProcessed")?.GetValue(instance));
        Assert.Equal(0, runStatsType.GetProperty("TotalSkippedKnown")?.GetValue(instance));
        Assert.Equal(0, runStatsType.GetProperty("TotalSkippedPending")?.GetValue(instance));
        Assert.Equal(0, runStatsType.GetProperty("TotalFlagged")?.GetValue(instance));
    }

    /// <summary>
    /// Tests that ParseBatchResult detects empty inbox from "no more emails" text.
    /// </summary>
    [Fact]
    public void ParseBatchResultDetectsEmptyInboxFromNoMoreEmails()
    {
        // Arrange
        const string response = "There are no more emails to process.";

        // Act
        var result = InvokeParseBatchResult(response);

        // Assert
        Assert.True(result.InboxWasEmpty);
    }

    /// <summary>
    /// Tests that ParseBatchResult detects empty inbox from "inbox empty" text.
    /// </summary>
    [Fact]
    public void ParseBatchResultDetectsEmptyInboxFromInboxEmpty()
    {
        // Arrange
        const string response = "The inbox empty state.";

        // Act
        var result = InvokeParseBatchResult(response);

        // Assert
        Assert.True(result.InboxWasEmpty);
    }

    /// <summary>
    /// Tests that ParseBatchResult detects empty inbox from "empty array" text.
    /// </summary>
    [Fact]
    public void ParseBatchResultDetectsEmptyInboxFromEmptyArrayText()
    {
        // Arrange
        const string response = "Received an empty array of messages.";

        // Act
        var result = InvokeParseBatchResult(response);

        // Assert
        Assert.True(result.InboxWasEmpty);
    }

    /// <summary>
    /// Tests that ParseBatchResult handles mixed case text.
    /// </summary>
    [Fact]
    public void ParseBatchResultHandlesMixedCaseText()
    {
        // Arrange
        const string response = "NO EMAILS found in inbox.";

        // Act
        var result = InvokeParseBatchResult(response);

        // Assert
        Assert.True(result.InboxWasEmpty);
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
    /// Tests that Truncate handles single character string.
    /// </summary>
    [Fact]
    public void TruncateHandlesSingleCharacter()
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
    /// Tests that Truncate truncates to single ellipsis for long text with max 1.
    /// </summary>
    [Fact]
    public void TruncateTruncatesToEllipsisWhenMaxLengthIsOne()
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
    /// Tests that ScanBatchResult initializes with default values.
    /// </summary>
    [Fact]
    public void ScanBatchResultInitializesWithDefaultValues()
    {
        // Arrange
        var scanBatchResultType = typeof(SpamScanAgent).GetNestedType("ScanBatchResult", BindingFlags.NonPublic);
        Assert.NotNull(scanBatchResultType);

        // Act
        var instance = Activator.CreateInstance(scanBatchResultType);
        Assert.NotNull(instance);

        // Assert
        Assert.Equal(0, scanBatchResultType.GetProperty("Processed")?.GetValue(instance));
        Assert.Equal(0, scanBatchResultType.GetProperty("SkippedKnown")?.GetValue(instance));
        Assert.Equal(0, scanBatchResultType.GetProperty("SkippedPending")?.GetValue(instance));
        Assert.Equal(0, scanBatchResultType.GetProperty("Flagged")?.GetValue(instance));
        Assert.False((bool)(scanBatchResultType.GetProperty("InboxWasEmpty")?.GetValue(instance) ?? true));
    }

    /// <summary>
    /// Tests that RunStats can be incremented.
    /// </summary>
    [Fact]
    public void RunStatsCanBeIncremented()
    {
        // Arrange
        var runStatsType = typeof(SpamScanAgent).GetNestedType("RunStats", BindingFlags.NonPublic);
        Assert.NotNull(runStatsType);

        var instance = Activator.CreateInstance(runStatsType);
        Assert.NotNull(instance);

        // Act - simulate increments
        var iterationProp = runStatsType.GetProperty("Iteration");
        var processedProp = runStatsType.GetProperty("TotalProcessed");
        var flaggedProp = runStatsType.GetProperty("TotalFlagged");

        iterationProp?.SetValue(instance, (int)(iterationProp.GetValue(instance) ?? 0) + 1);
        iterationProp?.SetValue(instance, (int)(iterationProp.GetValue(instance) ?? 0) + 1);

        processedProp?.SetValue(instance, (int)(processedProp.GetValue(instance) ?? 0) + 10);
        processedProp?.SetValue(instance, (int)(processedProp.GetValue(instance) ?? 0) + 5);

        flaggedProp?.SetValue(instance, (int)(flaggedProp.GetValue(instance) ?? 0) + 3);

        // Assert
        Assert.Equal(2, iterationProp?.GetValue(instance));
        Assert.Equal(15, processedProp?.GetValue(instance));
        Assert.Equal(3, flaggedProp?.GetValue(instance));
    }

    /// <summary>
    /// Tests that RunStats handles large values.
    /// </summary>
    [Fact]
    public void RunStatsHandlesLargeValues()
    {
        // Arrange
        var runStatsType = typeof(SpamScanAgent).GetNestedType("RunStats", BindingFlags.NonPublic);
        Assert.NotNull(runStatsType);

        var instance = Activator.CreateInstance(runStatsType);
        Assert.NotNull(instance);

        // Act
        runStatsType.GetProperty("Iteration")?.SetValue(instance, 1_000);
        runStatsType.GetProperty("TotalProcessed")?.SetValue(instance, 1_000_000);
        runStatsType.GetProperty("TotalSkippedKnown")?.SetValue(instance, 500_000);
        runStatsType.GetProperty("TotalSkippedPending")?.SetValue(instance, 250_000);
        runStatsType.GetProperty("TotalFlagged")?.SetValue(instance, 100_000);

        // Assert
        Assert.Equal(1_000, runStatsType.GetProperty("Iteration")?.GetValue(instance));
        Assert.Equal(1_000_000, runStatsType.GetProperty("TotalProcessed")?.GetValue(instance));
        Assert.Equal(500_000, runStatsType.GetProperty("TotalSkippedKnown")?.GetValue(instance));
        Assert.Equal(250_000, runStatsType.GetProperty("TotalSkippedPending")?.GetValue(instance));
        Assert.Equal(100_000, runStatsType.GetProperty("TotalFlagged")?.GetValue(instance));
    }

    /// <summary>
    /// Tests that ScanBatchResult handles large values.
    /// </summary>
    [Fact]
    public void ScanBatchResultHandlesLargeValues()
    {
        // Arrange
        var scanBatchResultType = typeof(SpamScanAgent).GetNestedType("ScanBatchResult", BindingFlags.NonPublic);
        Assert.NotNull(scanBatchResultType);

        var instance = Activator.CreateInstance(scanBatchResultType);
        Assert.NotNull(instance);

        // Act
        scanBatchResultType.GetProperty("Processed")?.SetValue(instance, 50000);
        scanBatchResultType.GetProperty("SkippedKnown")?.SetValue(instance, 25000);
        scanBatchResultType.GetProperty("SkippedPending")?.SetValue(instance, 10000);
        scanBatchResultType.GetProperty("Flagged")?.SetValue(instance, 5000);

        // Assert
        Assert.Equal(50000, scanBatchResultType.GetProperty("Processed")?.GetValue(instance));
        Assert.Equal(25000, scanBatchResultType.GetProperty("SkippedKnown")?.GetValue(instance));
        Assert.Equal(10000, scanBatchResultType.GetProperty("SkippedPending")?.GetValue(instance));
        Assert.Equal(5000, scanBatchResultType.GetProperty("Flagged")?.GetValue(instance));
    }

    /// <summary>
    /// Tests that ParseBatchResult handles BATCH_STATS with zero values.
    /// </summary>
    [Fact]
    public void ParseBatchResultHandlesBatchStatsWithZeroValues()
    {
        // Arrange
        const string response = "BATCH_STATS: processed=0, skipped_known=0, skipped_pending=0, flagged=0";

        // Act
        var result = InvokeParseBatchResult(response);

        // Assert
        Assert.False(result.InboxWasEmpty);
        Assert.Equal(0, result.Processed);
        Assert.Equal(0, result.SkippedKnown);
        Assert.Equal(0, result.SkippedPending);
        Assert.Equal(0, result.Flagged);
    }

    /// <summary>
    /// Tests that ParseBatchResult handles BATCH_STATS with large values.
    /// </summary>
    [Fact]
    public void ParseBatchResultHandlesBatchStatsWithLargeValues()
    {
        // Arrange
        const string response = "BATCH_STATS: processed=99999, skipped_known=88888, skipped_pending=77777, flagged=66666";

        // Act
        var result = InvokeParseBatchResult(response);

        // Assert
        Assert.Equal(99999, result.Processed);
        Assert.Equal(88888, result.SkippedKnown);
        Assert.Equal(77777, result.SkippedPending);
        Assert.Equal(66666, result.Flagged);
    }

    /// <summary>
    /// Tests that ScanResult handles special characters in properties.
    /// </summary>
    [Fact]
    public void ScanResultHandlesSpecialCharacters()
    {
        // Act
        var scanResult = new SpamScanAgent.ScanResult
        {
            Domain = "test.com",
            Subject = "Subject with <special> & \"characters\"",
            Status = "Flagged",
            Reason = "Contains: <script>alert('xss')</script>",
        };

        // Assert
        Assert.Equal("Subject with <special> & \"characters\"", scanResult.Subject);
        Assert.Equal("Contains: <script>alert('xss')</script>", scanResult.Reason);
    }

    /// <summary>
    /// Tests that ScanResult handles unicode characters.
    /// </summary>
    [Fact]
    public void ScanResultHandlesUnicodeCharacters()
    {
        // Act
        var scanResult = new SpamScanAgent.ScanResult
        {
            Domain = "测试.com",
            Subject = "日本語テスト",
            Status = "Флаgged",
            Reason = "Emoji test: 🎉🔥💯",
        };

        // Assert
        Assert.Equal("测试.com", scanResult.Domain);
        Assert.Equal("日本語テスト", scanResult.Subject);
        Assert.Equal("Emoji test: 🎉🔥💯", scanResult.Reason);
    }

    /// <summary>
    /// Tests that ScanResult handles empty strings.
    /// </summary>
    [Fact]
    public void ScanResultHandlesEmptyStrings()
    {
        // Act
        var scanResult = new SpamScanAgent.ScanResult
        {
            Domain = string.Empty,
            Subject = string.Empty,
            Status = string.Empty,
            Reason = string.Empty,
        };

        // Assert
        Assert.Equal(string.Empty, scanResult.Domain);
        Assert.Equal(string.Empty, scanResult.Subject);
        Assert.Equal(string.Empty, scanResult.Status);
        Assert.Equal(string.Empty, scanResult.Reason);
    }

    /// <summary>
    /// Tests that ScanResult handles very long strings.
    /// </summary>
    [Fact]
    public void ScanResultHandlesVeryLongStrings()
    {
        // Arrange
        var longString = new string('x', 10000);

        // Act
        var scanResult = new SpamScanAgent.ScanResult
        {
            Domain = longString,
            Subject = longString,
            Status = longString,
            Reason = longString,
        };

        // Assert
        Assert.Equal(10000, scanResult.Domain.Length);
        Assert.Equal(10000, scanResult.Subject.Length);
        Assert.Equal(10000, scanResult.Status.Length);
        Assert.Equal(10000, scanResult.Reason.Length);
    }

    /// <summary>
    /// Tests agent constructor handles null settings gracefully.
    /// </summary>
    [Fact]
    public void ConstructorThrowsForNullSettings() =>
        Assert.Throws<NullReferenceException>(() =>
        {
            using var agent = new SpamScanAgent(null!);
        });

    /// <summary>
    /// Tests that CreateChatClient returns error when base URL is missing.
    /// </summary>
    [Fact]
    public void CreateChatClientReturnsErrorWhenBaseUrlMissing()
    {
        // Arrange
        var originalBaseUrl = Environment.GetEnvironmentVariable("OPENROUTER_BASE_URL");
        var originalApiKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
        Environment.SetEnvironmentVariable("OPENROUTER_BASE_URL", value: null);
        Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", "test-key");

        try
        {
            // Act
            var method = typeof(SpamScanAgent).GetMethod(
                "CreateChatClient",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);

            var result = method.Invoke(obj: null, parameters: null);
            Assert.NotNull(result);

            // Use reflection to access tuple elements
            var resultType = result.GetType();
            var client = resultType.GetField("Item1")?.GetValue(result);
            var error = resultType.GetField("Item2")?.GetValue(result) as string;

            // Assert
            Assert.Null(client);
            Assert.NotNull(error);
            Assert.Contains("OPENROUTER_BASE_URL", error, StringComparison.Ordinal);
        }
        finally
        {
            // Restore original values
            Environment.SetEnvironmentVariable("OPENROUTER_BASE_URL", originalBaseUrl);
            Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", originalApiKey);
        }
    }

    /// <summary>
    /// Tests that CreateChatClient returns error when API key is missing.
    /// </summary>
    [Fact]
    public void CreateChatClientReturnsErrorWhenApiKeyMissing()
    {
        // Arrange
        var originalBaseUrl = Environment.GetEnvironmentVariable("OPENROUTER_BASE_URL");
        var originalApiKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
        Environment.SetEnvironmentVariable("OPENROUTER_BASE_URL", "https://openrouter.ai/api/v1");
        Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", value: null);

        try
        {
            // Act
            var method = typeof(SpamScanAgent).GetMethod(
                "CreateChatClient",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);

            var result = method.Invoke(obj: null, parameters: null);
            Assert.NotNull(result);

            // Use reflection to access tuple elements
            var resultType = result.GetType();
            var client = resultType.GetField("Item1")?.GetValue(result);
            var error = resultType.GetField("Item2")?.GetValue(result) as string;

            // Assert
            Assert.Null(client);
            Assert.NotNull(error);
            Assert.Contains("OPENROUTER_API_KEY", error, StringComparison.Ordinal);
        }
        finally
        {
            // Restore original values
            Environment.SetEnvironmentVariable("OPENROUTER_BASE_URL", originalBaseUrl);
            Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", originalApiKey);
        }
    }

    /// <summary>
    /// Tests that CreateChatClient creates client when both env vars are set.
    /// </summary>
    [Fact]
    public void CreateChatClientCreatesClientWhenEnvVarsSet()
    {
        // Arrange
        var originalBaseUrl = Environment.GetEnvironmentVariable("OPENROUTER_BASE_URL");
        var originalApiKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
        Environment.SetEnvironmentVariable("OPENROUTER_BASE_URL", "https://openrouter.ai/api/v1");
        Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", "test-api-key");

        try
        {
            // Act
            var method = typeof(SpamScanAgent).GetMethod(
                "CreateChatClient",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);

            var result = method.Invoke(obj: null, parameters: null);
            Assert.NotNull(result);

            // Use reflection to access tuple elements
            var resultType = result.GetType();
            var client = resultType.GetField("Item1")?.GetValue(result);
            var error = resultType.GetField("Item2")?.GetValue(result) as string;

            // Assert
            Assert.NotNull(client);
            Assert.Null(error);
        }
        finally
        {
            // Restore original values
            Environment.SetEnvironmentVariable("OPENROUTER_BASE_URL", originalBaseUrl);
            Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", originalApiKey);
        }
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

    private static ScanBatchResultWrapper InvokeParseBatchResult(string responseText)
    {
        var method = typeof(SpamScanAgent).GetMethod(
            "ParseBatchResult",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var result = method.Invoke(obj: null, [responseText]);
        Assert.NotNull(result);

        return new ScanBatchResultWrapper(result);
    }

    private static string InvokeTruncate(string text, int maxLength)
    {
        var method = typeof(SpamScanAgent).GetMethod(
            "Truncate",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var result = method.Invoke(obj: null, [text, maxLength]);
        Assert.NotNull(result);

        return (string)result;
    }

    /// <summary>
    /// Wrapper for accessing ScanBatchResult properties via reflection.
    /// </summary>
    /// <param name="instance">The ScanBatchResult instance.</param>
    private sealed class ScanBatchResultWrapper(object instance)
    {
        private readonly Type type = instance.GetType();

        public bool InboxWasEmpty => this.GetProperty<bool>(nameof(this.InboxWasEmpty));

        public int Processed => this.GetProperty<int>(nameof(this.Processed));

        public int SkippedKnown => this.GetProperty<int>(nameof(this.SkippedKnown));

        public int SkippedPending => this.GetProperty<int>(nameof(this.SkippedPending));

        public int Flagged => this.GetProperty<int>(nameof(this.Flagged));

        private T GetProperty<T>(string name)
        {
            var prop = this.type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(prop);
            var value = prop.GetValue(instance);
            Assert.NotNull(value);
            return (T)value;
        }
    }
}
