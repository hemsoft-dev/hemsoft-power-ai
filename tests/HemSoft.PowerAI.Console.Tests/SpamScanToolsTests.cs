// <copyright file="SpamScanToolsTests.cs" company="HemSoft">
// Copyright © 2025 HemSoft
// </copyright>

namespace HemSoft.PowerAI.Console.Tests;

using System.Reflection;
using System.Text.Json;

using HemSoft.PowerAI.Console.Agents;
using HemSoft.PowerAI.Console.Configuration;
using HemSoft.PowerAI.Console.Services;

/// <summary>
/// Unit tests for <see cref="SpamScanTools"/>.
/// </summary>
[Collection("EnvironmentVariableTests")]
public class SpamScanToolsTests : IDisposable
{
    private readonly string testDirectory;
    private readonly SpamFilterSettings settings;
    private readonly SpamStorageService storageService;
    private readonly HumanReviewService humanReviewService;
    private readonly MockGraphClientProvider mockGraphProvider;
    private readonly SpamScanTools sut;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SpamScanToolsTests"/> class.
    /// </summary>
    public SpamScanToolsTests()
    {
        this.testDirectory = Path.Combine(Path.GetTempPath(), "SpamScanToolsTests_" + Guid.NewGuid().ToString("N")[..8]);
        _ = Directory.CreateDirectory(this.testDirectory);

        this.settings = new SpamFilterSettings
        {
            HumanReviewFilePath = Path.Combine(this.testDirectory, "human_review.json"),
            SpamDomainsFilePath = "spam_domains.json",
            SpamCandidatesFilePath = "spam_candidates.json",
        };

        this.storageService = new SpamStorageService(this.settings, this.testDirectory);
        this.humanReviewService = new HumanReviewService(this.settings);
        this.mockGraphProvider = new MockGraphClientProvider();
        this.sut = new SpamScanTools(this.storageService, this.humanReviewService, this.mockGraphProvider);
    }

    /// <summary>
    /// Tests that GetKnownSpamDomains returns empty array when no domains exist.
    /// </summary>
    [Fact]
    public void GetKnownSpamDomainsReturnsEmptyArrayWhenNoDomains()
    {
        // Act
        var result = this.sut.GetKnownSpamDomains();
        var domains = JsonSerializer.Deserialize<string[]>(result);

        // Assert
        Assert.NotNull(domains);
        Assert.Empty(domains);
    }

    /// <summary>
    /// Tests that GetKnownSpamDomains returns domains as JSON array.
    /// </summary>
    [Fact]
    public void GetKnownSpamDomainsReturnsDomainAsJsonArray()
    {
        // Arrange
        _ = this.storageService.AddSpamDomain("spam.com", "Test");
        _ = this.storageService.AddSpamDomain("junk.com", "Test");

        // Act
        var result = this.sut.GetKnownSpamDomains();
        var domains = JsonSerializer.Deserialize<string[]>(result);

        // Assert
        Assert.NotNull(domains);
        Assert.Equal(2, domains.Length);
        Assert.Contains("SPAM.COM", domains, StringComparer.Ordinal);
        Assert.Contains("JUNK.COM", domains, StringComparer.Ordinal);
    }

    /// <summary>
    /// Tests that GetPendingReviewDomains returns empty array when no domains pending.
    /// </summary>
    [Fact]
    public void GetPendingReviewDomainsReturnsEmptyArrayWhenNoDomains()
    {
        // Act
        var result = this.sut.GetPendingReviewDomains();
        var domains = JsonSerializer.Deserialize<string[]>(result);

        // Assert
        Assert.NotNull(domains);
        Assert.Empty(domains);
    }

    /// <summary>
    /// Tests that GetPendingReviewDomains returns domains as JSON array.
    /// </summary>
    [Fact]
    public void GetPendingReviewDomainsReturnsDomainAsJsonArray()
    {
        // Arrange
        _ = this.humanReviewService.AddOrUpdateDomain("pending1.com", "msg1", "a@pending1.com", "Subject 1", "Reason 1");
        _ = this.humanReviewService.AddOrUpdateDomain("pending2.com", "msg2", "a@pending2.com", "Subject 2", "Reason 2");

        // Act
        var result = this.sut.GetPendingReviewDomains();
        var domains = JsonSerializer.Deserialize<string[]>(result);

        // Assert
        Assert.NotNull(domains);
        Assert.Equal(2, domains.Length);
        Assert.Contains("PENDING1.COM", domains, StringComparer.Ordinal);
        Assert.Contains("PENDING2.COM", domains, StringComparer.Ordinal);
    }

    /// <summary>
    /// Tests that GetPendingReviewCount returns zero when no domains pending.
    /// </summary>
    [Fact]
    public void GetPendingReviewCountReturnsZeroWhenNoDomains()
    {
        // Act
        var count = this.sut.GetPendingReviewCount();

        // Assert
        Assert.Equal(0, count);
    }

    /// <summary>
    /// Tests that GetPendingReviewCount returns correct count.
    /// </summary>
    [Fact]
    public void GetPendingReviewCountReturnsCorrectCount()
    {
        // Arrange
        _ = this.humanReviewService.AddOrUpdateDomain("pending1.com", "msg1", "a@pending1.com", "Subject 1", "Reason 1");
        _ = this.humanReviewService.AddOrUpdateDomain("pending2.com", "msg2", "a@pending2.com", "Subject 2", "Reason 2");

        // Act
        var count = this.sut.GetPendingReviewCount();

        // Assert
        Assert.Equal(2, count);
    }

    /// <summary>
    /// Tests that FlagDomainForReview returns new domain message for new domain.
    /// </summary>
    [Fact]
    public void FlagDomainForReviewReturnsNewDomainMessageForNewDomain()
    {
        // Arrange
        const string domain = "new.spam.com";
        const string messageId = "msg123";
        const string senderEmail = "spammer@new.spam.com";
        const string subject = "Win money!";
        const string reason = "Suspicious content";

        // Act
        var result = this.sut.FlagDomainForReview(domain, messageId, senderEmail, subject, reason);

        // Assert
        Assert.Contains("Flagged new domain", result, StringComparison.Ordinal);
        Assert.Contains(domain, result, StringComparison.Ordinal);
        Assert.Equal(1, this.humanReviewService.GetPendingCount());
    }

    /// <summary>
    /// Tests that FlagDomainForReview returns updated message for existing domain.
    /// </summary>
    [Fact]
    public void FlagDomainForReviewReturnsUpdatedMessageForExistingDomain()
    {
        // Arrange
        const string domain = "existing.spam.com";
        _ = this.humanReviewService.AddOrUpdateDomain(domain, "msg1", "a@existing.spam.com", "Subject 1", "Reason 1");

        // Act
        var result = this.sut.FlagDomainForReview(domain, "msg2", "b@existing.spam.com", "Subject 2", "Reason 2");

        // Assert
        Assert.Contains("Updated existing flag", result, StringComparison.Ordinal);
        Assert.Contains(domain, result, StringComparison.Ordinal);
        Assert.Equal(1, this.humanReviewService.GetPendingCount());
    }

    /// <summary>
    /// Tests that ReportScanResult returns confirmation message.
    /// </summary>
    [Fact]
    public void ReportScanResultReturnsConfirmationMessage()
    {
        // Arrange
        const string domain = "test.com";
        const string subject = "Test Subject";
        const string status = "Flagged";
        const string reason = "Suspicious";

        // Act
        var result = this.sut.ReportScanResult(domain, subject, status, reason);

        // Assert
        Assert.Equal("Scan result recorded.", result);
    }

    /// <summary>
    /// Tests that ReportScanResult invokes callback when set.
    /// </summary>
    [Fact]
    public void ReportScanResultInvokesCallbackWhenSet()
    {
        // Arrange
        SpamScanAgent.ScanResult? receivedResult = null;
        this.sut.SetResultCallback(r => receivedResult = r);

        // Act
        _ = this.sut.ReportScanResult("test.com", "Test Subject", "Flagged", "Suspicious");

        // Assert
        Assert.NotNull(receivedResult);
        Assert.Equal("test.com", receivedResult.Domain);
        Assert.Equal("Test Subject", receivedResult.Subject);
        Assert.Equal("Flagged", receivedResult.Status);
        Assert.Equal("Suspicious", receivedResult.Reason);
    }

    /// <summary>
    /// Tests that ReportScanResult handles null callback gracefully.
    /// </summary>
    [Fact]
    public void ReportScanResultHandlesNullCallbackGracefully()
    {
        // Arrange
        this.sut.SetResultCallback(callback: null);

        // Act & Assert - should not throw
        var result = this.sut.ReportScanResult("test.com", "Test Subject", "Flagged", reason: null);
        Assert.Equal("Scan result recorded.", result);
    }

    /// <summary>
    /// Tests that ReportScanResult handles null values gracefully.
    /// </summary>
    [Fact]
    public void ReportScanResultHandlesNullValuesGracefully()
    {
        // Arrange
        SpamScanAgent.ScanResult? receivedResult = null;
        this.sut.SetResultCallback(r => receivedResult = r);

        // Act
        _ = this.sut.ReportScanResult(null!, null!, null!, reason: null);

        // Assert
        Assert.NotNull(receivedResult);
        Assert.Equal("unknown", receivedResult.Domain);
        Assert.Equal("(no subject)", receivedResult.Subject);
        Assert.Equal("Unknown", receivedResult.Status);
        Assert.Null(receivedResult.Reason);
    }

    /// <summary>
    /// Tests that SetResultCallback can change callback.
    /// </summary>
    [Fact]
    public void SetResultCallbackCanChangeCallback()
    {
        // Arrange
        SpamScanAgent.ScanResult? result1 = null;
        SpamScanAgent.ScanResult? result2 = null;

        // Act
        this.sut.SetResultCallback(r => result1 = r);
        _ = this.sut.ReportScanResult("domain1.com", "Subject 1", "Clean", reason: null);

        this.sut.SetResultCallback(r => result2 = r);
        _ = this.sut.ReportScanResult("domain2.com", "Subject 2", "Flagged", reason: null);

        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.Equal("domain1.com", result1.Domain);
        Assert.Equal("domain2.com", result2.Domain);
    }

    /// <summary>
    /// Tests that Dispose can be called multiple times without throwing.
    /// </summary>
    [Fact]
    public void DisposeCanBeCalledMultipleTimes()
    {
        // Arrange
        using var tools = new SpamScanTools(this.storageService, this.humanReviewService, this.mockGraphProvider);

        // Act
        var exception = Record.Exception(() =>
        {
            tools.Dispose();
            tools.Dispose();
        });

        // Assert
        Assert.Null(exception);
    }

    /// <summary>
    /// Tests that GetInboxEmailsAsync returns error when Graph client not configured.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task GetInboxEmailsAsyncReturnsErrorWhenClientNotConfigured()
    {
        // Arrange - mockGraphProvider returns null client by default

        // Act
        var result = await this.sut.GetInboxEmailsAsync(10);

        // Assert
        Assert.Contains("GRAPH_CLIENT_ID", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tests that ReadEmailAsync returns error when Graph client not configured.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task ReadEmailAsyncReturnsErrorWhenClientNotConfigured()
    {
        // Arrange - mockGraphProvider returns null client by default

        // Act
        var result = await this.sut.ReadEmailAsync("msg123");

        // Assert
        Assert.Contains("GRAPH_CLIENT_ID", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tests that ExtractDomain extracts domain from email address.
    /// </summary>
    [Fact]
    public void ExtractDomainExtractsDomainFromEmail()
    {
        // Arrange
        var method = typeof(SpamScanTools).GetMethod(
            "ExtractDomain",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        // Act
        var result = method.Invoke(null, ["user@example.com"]);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("EXAMPLE.COM", (string)result);
    }

    /// <summary>
    /// Tests that ExtractDomain handles email without @ symbol.
    /// </summary>
    [Fact]
    public void ExtractDomainHandlesEmailWithoutAtSymbol()
    {
        // Arrange
        var method = typeof(SpamScanTools).GetMethod(
            "ExtractDomain",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        // Act
        var result = method.Invoke(null, ["nodomain"]);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("NODOMAIN", (string)result);
    }

    /// <summary>
    /// Tests that ExtractDomain handles email with multiple @ symbols.
    /// </summary>
    [Fact]
    public void ExtractDomainHandlesEmailWithMultipleAtSymbols()
    {
        // Arrange
        var method = typeof(SpamScanTools).GetMethod(
            "ExtractDomain",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        // Act
        var result = method.Invoke(null, ["user@weird@example.com"]);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("EXAMPLE.COM", (string)result);
    }

    /// <summary>
    /// Tests that TruncateBody returns empty string for null body.
    /// </summary>
    [Fact]
    public void TruncateBodyReturnsEmptyStringForNullBody()
    {
        // Arrange
        var method = typeof(SpamScanTools).GetMethod(
            "TruncateBody",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        // Act
        var result = method.Invoke(null, [null, 500]);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(string.Empty, (string)result);
    }

    /// <summary>
    /// Tests that TruncateBody strips HTML tags.
    /// </summary>
    [Fact]
    public void TruncateBodyStripsHtmlTags()
    {
        // Arrange
        var method = typeof(SpamScanTools).GetMethod(
            "TruncateBody",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        // Act
        var result = method.Invoke(null, ["<p>Hello <b>World</b></p>", 500]);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Hello World", (string)result);
    }

    /// <summary>
    /// Tests that TruncateBody normalizes whitespace.
    /// </summary>
    [Fact]
    public void TruncateBodyNormalizesWhitespace()
    {
        // Arrange
        var method = typeof(SpamScanTools).GetMethod(
            "TruncateBody",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        // Act
        var result = method.Invoke(null, ["Hello    World\n\nTest", 500]);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Hello World Test", (string)result);
    }

    /// <summary>
    /// Tests that TruncateBody truncates and adds ellipsis.
    /// </summary>
    [Fact]
    public void TruncateBodyTruncatesAndAddsEllipsis()
    {
        // Arrange
        var method = typeof(SpamScanTools).GetMethod(
            "TruncateBody",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        var longText = new string('A', 600);

        // Act
        var result = method.Invoke(null, [longText, 500]);

        // Assert
        Assert.NotNull(result);
        var resultStr = (string)result;
        Assert.EndsWith("...", resultStr, StringComparison.Ordinal);
        Assert.Equal(503, resultStr.Length); // 500 chars + "..."
    }

    /// <summary>
    /// Tests that TruncateBody returns exact length text unchanged.
    /// </summary>
    [Fact]
    public void TruncateBodyReturnsExactLengthTextUnchanged()
    {
        // Arrange
        var method = typeof(SpamScanTools).GetMethod(
            "TruncateBody",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        var text = new string('B', 100);

        // Act
        var result = method.Invoke(null, [text, 100]);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(text, (string)result);
    }

    /// <summary>
    /// Tests that TruncateBody handles empty string.
    /// </summary>
    [Fact]
    public void TruncateBodyHandlesEmptyString()
    {
        // Arrange
        var method = typeof(SpamScanTools).GetMethod(
            "TruncateBody",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        // Act
        var result = method.Invoke(null, [string.Empty, 500]);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(string.Empty, (string)result);
    }

    /// <summary>
    /// Tests that TruncateBody handles complex HTML.
    /// </summary>
    [Fact]
    public void TruncateBodyHandlesComplexHtml()
    {
        // Arrange
        var method = typeof(SpamScanTools).GetMethod(
            "TruncateBody",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        const string html = "<html><body><div class='test'><p>Paragraph</p><span>Text</span></div></body></html>";

        // Act
        var result = method.Invoke(null, [html, 500]);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Paragraph Text", (string)result);
    }

    /// <summary>
    /// Tests that ExtractDomain handles empty string.
    /// </summary>
    [Fact]
    public void ExtractDomainHandlesEmptyString()
    {
        // Arrange
        var method = typeof(SpamScanTools).GetMethod(
            "ExtractDomain",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        // Act
        var result = method.Invoke(null, [string.Empty]);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(string.Empty, (string)result);
    }

    /// <summary>
    /// Tests that ExtractDomain handles email ending with @ symbol.
    /// </summary>
    [Fact]
    public void ExtractDomainHandlesEmailEndingWithAtSymbol()
    {
        // Arrange
        var method = typeof(SpamScanTools).GetMethod(
            "ExtractDomain",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        // Act
        var result = method.Invoke(null, ["user@"]);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("USER@", (string)result);
    }

    /// <summary>
    /// Tests that ExtractDomain converts domain to uppercase.
    /// </summary>
    [Fact]
    public void ExtractDomainConvertsDomainToUppercase()
    {
        // Arrange
        var method = typeof(SpamScanTools).GetMethod(
            "ExtractDomain",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        // Act
        var result = method.Invoke(null, ["user@Example.COM"]);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("EXAMPLE.COM", (string)result);
    }

    /// <summary>
    /// Tests that FlagDomainForReview uses uppercase domain.
    /// </summary>
    [Fact]
    public void FlagDomainForReviewUsesUppercaseDomain()
    {
        // Arrange
        const string domain = "lowercase.com";

        // Act
        var result = this.sut.FlagDomainForReview(domain, "msg123", "user@lowercase.com", "Test", "Test reason");

        // Assert
        Assert.Contains("lowercase.com", result, StringComparison.Ordinal);

        // Verify it was stored
        var pendingDomains = this.sut.GetPendingReviewDomains();
        Assert.Contains("LOWERCASE.COM", pendingDomains, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tests that multiple FlagDomainForReview calls for same domain don't create duplicates.
    /// </summary>
    [Fact]
    public void MultipleFlagDomainForReviewCallsForSameDomainDontCreateDuplicates()
    {
        // Arrange
        const string domain = "repeated.com";

        // Act
        _ = this.sut.FlagDomainForReview(domain, "msg1", "a@repeated.com", "Subject 1", "Reason 1");
        _ = this.sut.FlagDomainForReview(domain, "msg2", "b@repeated.com", "Subject 2", "Reason 2");
        _ = this.sut.FlagDomainForReview(domain, "msg3", "c@repeated.com", "Subject 3", "Reason 3");

        // Assert - should still only be one domain in the review queue
        Assert.Equal(1, this.humanReviewService.GetPendingCount());
    }

    /// <summary>
    /// Tests that ReportScanResult handles empty strings.
    /// </summary>
    [Fact]
    public void ReportScanResultHandlesEmptyStrings()
    {
        // Arrange
        SpamScanAgent.ScanResult? receivedResult = null;
        this.sut.SetResultCallback(r => receivedResult = r);

        // Act
        _ = this.sut.ReportScanResult(string.Empty, string.Empty, string.Empty, string.Empty);

        // Assert
        Assert.NotNull(receivedResult);
        Assert.Equal(string.Empty, receivedResult.Domain);
        Assert.Equal(string.Empty, receivedResult.Subject);
        Assert.Equal(string.Empty, receivedResult.Status);
        Assert.Equal(string.Empty, receivedResult.Reason);
    }

    /// <summary>
    /// Tests that processedMessageIds is properly used.
    /// </summary>
    [Fact]
    public void ProcessedMessageIdsIsProperlyInitialized()
    {
        // Arrange
        var field = typeof(SpamScanTools).GetField(
            "processedMessageIds",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);

        // Act
        var value = field.GetValue(this.sut);

        // Assert
        Assert.NotNull(value);
        var hashSet = value as HashSet<string>;
        Assert.NotNull(hashSet);
        Assert.Empty(hashSet);
    }

    /// <summary>
    /// Tests that GetKnownSpamDomains returns valid JSON for multiple domains.
    /// </summary>
    [Fact]
    public void GetKnownSpamDomainsReturnsValidJsonForMultipleDomains()
    {
        // Arrange
        _ = this.storageService.AddSpamDomain("domain1.com", "Test 1");
        _ = this.storageService.AddSpamDomain("domain2.com", "Test 2");
        _ = this.storageService.AddSpamDomain("domain3.com", "Test 3");

        // Act
        var result = this.sut.GetKnownSpamDomains();
        var domains = JsonSerializer.Deserialize<string[]>(result);

        // Assert
        Assert.NotNull(domains);
        Assert.Equal(3, domains.Length);
    }

    /// <summary>
    /// Tests that GetPendingReviewDomains returns valid JSON for multiple domains.
    /// </summary>
    [Fact]
    public void GetPendingReviewDomainsReturnsValidJsonForMultipleDomains()
    {
        // Arrange
        _ = this.humanReviewService.AddOrUpdateDomain("domain1.com", "msg1", "a@domain1.com", "Subject 1", "Reason 1");
        _ = this.humanReviewService.AddOrUpdateDomain("domain2.com", "msg2", "a@domain2.com", "Subject 2", "Reason 2");
        _ = this.humanReviewService.AddOrUpdateDomain("domain3.com", "msg3", "a@domain3.com", "Subject 3", "Reason 3");

        // Act
        var result = this.sut.GetPendingReviewDomains();
        var domains = JsonSerializer.Deserialize<string[]>(result);

        // Assert
        Assert.NotNull(domains);
        Assert.Equal(3, domains.Length);
    }

    /// <summary>
    /// Tests that FlagDomainForReview handles unicode domain names.
    /// </summary>
    [Fact]
    public void FlagDomainForReviewHandlesUnicodeDomainNames()
    {
        // Arrange
        const string domain = "日本語.jp";

        // Act
        var result = this.sut.FlagDomainForReview(domain, "msg123", "user@日本語.jp", "Japanese Subject", "Test reason");

        // Assert
        Assert.Contains("Flagged new domain", result, StringComparison.Ordinal);
        Assert.Equal(1, this.humanReviewService.GetPendingCount());
    }

    /// <summary>
    /// Tests that ReportScanResult handles very long strings.
    /// </summary>
    [Fact]
    public void ReportScanResultHandlesVeryLongStrings()
    {
        // Arrange
        SpamScanAgent.ScanResult? receivedResult = null;
        this.sut.SetResultCallback(r => receivedResult = r);
        var longString = new string('x', 10000);

        // Act
        _ = this.sut.ReportScanResult(longString, longString, longString, longString);

        // Assert
        Assert.NotNull(receivedResult);
        Assert.Equal(10000, receivedResult.Domain.Length);
        Assert.Equal(10000, receivedResult.Subject.Length);
        Assert.Equal(10000, receivedResult.Status.Length);
        Assert.Equal(10000, receivedResult.Reason!.Length);
    }

    /// <summary>
    /// Tests that ExtractDomain handles subdomain correctly.
    /// </summary>
    [Fact]
    public void ExtractDomainHandlesSubdomain()
    {
        // Arrange
        var method = typeof(SpamScanTools).GetMethod(
            "ExtractDomain",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        // Act
        var result = method.Invoke(null, ["user@sub.domain.example.com"]);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("SUB.DOMAIN.EXAMPLE.COM", (string)result);
    }

    /// <summary>
    /// Tests that TruncateBody handles body with only HTML tags.
    /// </summary>
    [Fact]
    public void TruncateBodyHandlesBodyWithOnlyHtmlTags()
    {
        // Arrange
        var method = typeof(SpamScanTools).GetMethod(
            "TruncateBody",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        // Act
        var result = method.Invoke(null, ["<div><span></span></div>", 500]);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(string.Empty, (string)result);
    }

    /// <summary>
    /// Tests that TruncateBody handles mixed content with tabs and newlines.
    /// </summary>
    [Fact]
    public void TruncateBodyHandlesMixedContentWithTabsAndNewlines()
    {
        // Arrange
        var method = typeof(SpamScanTools).GetMethod(
            "TruncateBody",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        // Act
        var result = method.Invoke(null, ["Hello\t\tWorld\n\n\nTest", 500]);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Hello World Test", (string)result);
    }

    /// <summary>
    /// Tests that FlagDomainForReview with special characters in reason works correctly.
    /// </summary>
    [Fact]
    public void FlagDomainForReviewHandlesSpecialCharactersInReason()
    {
        // Arrange
        const string domain = "test.com";
        const string reason = "Contains <script>alert('xss')</script> and \"quotes\" & ampersands";

        // Act
        var result = this.sut.FlagDomainForReview(domain, "msg123", "user@test.com", "Test", reason);

        // Assert
        Assert.Contains("Flagged new domain", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tests that GetPendingReviewCount updates correctly after adds and removes.
    /// </summary>
    [Fact]
    public void GetPendingReviewCountUpdatesCorrectlyAfterAddsAndRemoves()
    {
        // Arrange & Act - Add domains
        _ = this.humanReviewService.AddOrUpdateDomain("domain1.com", "msg1", "a@domain1.com", "Subject 1", "Reason 1");
        _ = this.humanReviewService.AddOrUpdateDomain("domain2.com", "msg2", "a@domain2.com", "Subject 2", "Reason 2");

        // Assert after adds
        Assert.Equal(2, this.sut.GetPendingReviewCount());

        // Act - Remove one
        _ = this.humanReviewService.RemoveDomain("domain1.com");

        // Assert after remove
        Assert.Equal(1, this.sut.GetPendingReviewCount());

        // Act - Clear all
        this.humanReviewService.ClearAll();

        // Assert after clear
        Assert.Equal(0, this.sut.GetPendingReviewCount());
    }

    /// <summary>
    /// Tests that SetResultCallback can be set to null and back.
    /// </summary>
    [Fact]
    public void SetResultCallbackCanBeSetToNullAndBack()
    {
        // Arrange
        SpamScanAgent.ScanResult? result1 = null;
        SpamScanAgent.ScanResult? result2 = null;

        // Act - Set callback
        this.sut.SetResultCallback(r => result1 = r);
        _ = this.sut.ReportScanResult("domain1.com", "Subject 1", "Flagged", reason: null);

        // Act - Set to null
        this.sut.SetResultCallback(callback: null);
        _ = this.sut.ReportScanResult("domain2.com", "Subject 2", "Clean", reason: null);

        // Act - Set new callback
        this.sut.SetResultCallback(r => result2 = r);
        _ = this.sut.ReportScanResult("domain3.com", "Subject 3", "Skipped", reason: null);

        // Assert
        Assert.NotNull(result1);
        Assert.Equal("domain1.com", result1.Domain);
        Assert.Null(string.Equals(result2?.Domain, "domain2.com", StringComparison.Ordinal) ? result2 : null); // result2 should not have domain2
        Assert.NotNull(result2);
        Assert.Equal("domain3.com", result2.Domain);
    }

    /// <summary>
    /// Tests that TruncateBody handles self-closing HTML tags.
    /// </summary>
    [Fact]
    public void TruncateBodyHandlesSelfClosingHtmlTags()
    {
        // Arrange
        var method = typeof(SpamScanTools).GetMethod(
            "TruncateBody",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        // Act
        var result = method.Invoke(null, ["Hello<br/>World<hr/>Test", 500]);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Hello World Test", (string)result);
    }

    /// <summary>
    /// Tests that TruncateBody handles nested HTML tags.
    /// </summary>
    [Fact]
    public void TruncateBodyHandlesNestedHtmlTags()
    {
        // Arrange
        var method = typeof(SpamScanTools).GetMethod(
            "TruncateBody",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        const string html = "<div><div><div><p>Deeply</p></div><span>Nested</span></div></div>";

        // Act
        var result = method.Invoke(null, [html, 500]);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Deeply Nested", (string)result);
    }

    /// <summary>
    /// Tests that ExtractDomain handles IP address format.
    /// </summary>
    [Fact]
    public void ExtractDomainHandlesIpAddress()
    {
        // Arrange
        var method = typeof(SpamScanTools).GetMethod(
            "ExtractDomain",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        // Act
        var result = method.Invoke(null, ["user@192.168.1.1"]);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("192.168.1.1", (string)result);
    }

    /// <summary>
    /// Tests that ExtractDomain handles plus-addressed email.
    /// </summary>
    [Fact]
    public void ExtractDomainHandlesPlusAddressedEmail()
    {
        // Arrange
        var method = typeof(SpamScanTools).GetMethod(
            "ExtractDomain",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        // Act
        var result = method.Invoke(null, ["user+tag@example.com"]);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("EXAMPLE.COM", (string)result);
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
            this.sut.Dispose();

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
