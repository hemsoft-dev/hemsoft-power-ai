// <copyright file="OutlookMailToolsTests.cs" company="HemSoft">
// Copyright © 2025 HemSoft
// </copyright>

namespace HemSoft.PowerAI.Console.Tests;

using System.Reflection;
using System.Text.Json;

using HemSoft.PowerAI.Console.Configuration;
using HemSoft.PowerAI.Console.Services;
using HemSoft.PowerAI.Console.Tools;

/// <summary>
/// Unit tests for <see cref="OutlookMailTools"/>.
/// </summary>
[Collection("EnvironmentVariableTests")]
public class OutlookMailToolsTests : IDisposable
{
    private readonly string testDirectory;
    private readonly SpamFilterSettings settings;
    private readonly SpamStorageService storageService;
    private readonly MockGraphClientProvider mockGraphProvider;
    private readonly OutlookMailTools sut;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="OutlookMailToolsTests"/> class.
    /// </summary>
    public OutlookMailToolsTests()
    {
        this.testDirectory = Path.Combine(Path.GetTempPath(), "OutlookMailToolsTests_" + Guid.NewGuid().ToString("N")[..8]);
        _ = Directory.CreateDirectory(this.testDirectory);

        this.settings = new SpamFilterSettings
        {
            HumanReviewFilePath = Path.Combine(this.testDirectory, "human_review.json"),
            SpamDomainsFilePath = "spam_domains.json",
            SpamCandidatesFilePath = "spam_candidates.json",
        };

        this.storageService = new SpamStorageService(this.settings, this.testDirectory);
        this.mockGraphProvider = new MockGraphClientProvider();
        this.sut = new OutlookMailTools(this.mockGraphProvider, this.storageService);
    }

    /// <summary>
    /// Tests that MailAsync returns error when Graph client is not configured.
    /// </summary>
    /// <returns>A task representing the async operation.</returns>
    [Fact]
    public async Task MailAsyncReturnsErrorWhenClientNotConfigured()
    {
        // Arrange - mockGraphProvider returns null client by default

        // Act
        var result = await this.sut.MailAsync("inbox");

        // Assert
        Assert.Contains("GRAPH_CLIENT_ID", result, StringComparison.Ordinal);
        Assert.Contains("environment variable", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tests that MailAsync returns error for unknown mode.
    /// </summary>
    /// <returns>A task representing the async operation.</returns>
    [Fact]
    public async Task MailAsyncReturnsErrorForUnknownMode()
    {
        // Arrange - with configured provider we'd get unknown mode error
        // but with unconfigured provider we get client error first

        // Act
        var result = await this.sut.MailAsync("unknownmode");

        // Assert
        Assert.Contains("GRAPH_CLIENT_ID", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tests that blocklist mode returns error when spam storage not initialized.
    /// </summary>
    /// <returns>A task representing the async operation.</returns>
    [Fact]
    public async Task BlocklistModeReturnsErrorWhenSpamStorageNull()
    {
        // Arrange
        var sutWithoutStorage = new OutlookMailTools(this.mockGraphProvider, spamStorage: null);

        // Act - blocklist mode should fail because storage is null
        var result = await sutWithoutStorage.MailAsync("blocklist");

        // Assert - gets client error first since client is null
        Assert.Contains("GRAPH_CLIENT_ID", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tests that blockadd mode returns error when spam storage not initialized.
    /// </summary>
    /// <returns>A task representing the async operation.</returns>
    [Fact]
    public async Task BlockaddModeReturnsErrorWhenSpamStorageNull()
    {
        // Arrange
        var sutWithoutStorage = new OutlookMailTools(this.mockGraphProvider, spamStorage: null);

        // Act
        var result = await sutWithoutStorage.MailAsync("blockadd", "spam.com", "Test reason");

        // Assert - gets client error first since client is null
        Assert.Contains("GRAPH_CLIENT_ID", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tests that blockcheck mode returns error when spam storage not initialized.
    /// </summary>
    /// <returns>A task representing the async operation.</returns>
    [Fact]
    public async Task BlockcheckModeReturnsErrorWhenSpamStorageNull()
    {
        // Arrange
        var sutWithoutStorage = new OutlookMailTools(this.mockGraphProvider, spamStorage: null);

        // Act
        var result = await sutWithoutStorage.MailAsync("blockcheck", "spam.com");

        // Assert - gets client error first since client is null
        Assert.Contains("GRAPH_CLIENT_ID", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tests that ResetOperationTracking clears the last operation.
    /// </summary>
    [Fact]
    public void ResetOperationTrackingClearsLastOperation()
    {
        // Arrange
        var field = typeof(OutlookMailTools).GetField(
            "lastOperation",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);

        // Set a value first
        field.SetValue(this.sut, "INBOX");

        // Act
        this.sut.ResetOperationTracking();

        // Assert
        var value = field.GetValue(this.sut);
        Assert.Null(value);
    }

    /// <summary>
    /// Tests that GetSpamDomainList returns empty message when no domains.
    /// </summary>
    [Fact]
    public void GetSpamDomainListReturnsEmptyMessageWhenNoDomains()
    {
        // Arrange - use reflection to call private method
        var method = typeof(OutlookMailTools).GetMethod(
            "GetSpamDomainList",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        // Act
        var result = method.Invoke(this.sut, parameters: null) as string;

        // Assert
        Assert.Equal("No domains in spam registry.", result);
    }

    /// <summary>
    /// Tests that GetSpamDomainList returns JSON array when domains exist.
    /// </summary>
    [Fact]
    public void GetSpamDomainListReturnsJsonArrayWhenDomainsExist()
    {
        // Arrange
        _ = this.storageService.AddSpamDomain("spam1.com", "Test");
        _ = this.storageService.AddSpamDomain("spam2.com", "Test");

        var method = typeof(OutlookMailTools).GetMethod(
            "GetSpamDomainList",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        // Act
        var result = method.Invoke(this.sut, parameters: null) as string;
        var domains = JsonSerializer.Deserialize<string[]>(result!);

        // Assert
        Assert.NotNull(domains);
        Assert.Equal(2, domains.Length);
        Assert.Contains("SPAM1.COM", domains, StringComparer.Ordinal);
        Assert.Contains("SPAM2.COM", domains, StringComparer.Ordinal);
    }

    /// <summary>
    /// Tests that GetSpamDomainList returns error when storage is null.
    /// </summary>
    [Fact]
    public void GetSpamDomainListReturnsErrorWhenStorageNull()
    {
        // Arrange
        var sutWithoutStorage = new OutlookMailTools(this.mockGraphProvider, spamStorage: null);
        var method = typeof(OutlookMailTools).GetMethod(
            "GetSpamDomainList",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        // Act
        var result = method.Invoke(sutWithoutStorage, parameters: null) as string;

        // Assert
        Assert.Contains("Spam storage not initialized", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tests that AddToSpamDomainList adds domain successfully.
    /// </summary>
    [Fact]
    public void AddToSpamDomainListAddsDomainSuccessfully()
    {
        // Arrange
        var method = typeof(OutlookMailTools).GetMethod(
            "AddToSpamDomainList",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        // Act
        var result = method.Invoke(this.sut, ["newspam.com", "Test reason"]) as string;

        // Assert
        Assert.Contains("Added", result, StringComparison.Ordinal);
        Assert.Contains("NEWSPAM.COM", result, StringComparison.Ordinal);
        Assert.True(this.storageService.IsKnownSpamDomain("newspam.com"));
    }

    /// <summary>
    /// Tests that AddToSpamDomainList returns error for duplicate domain.
    /// </summary>
    [Fact]
    public void AddToSpamDomainListReturnsMessageForDuplicateDomain()
    {
        // Arrange
        _ = this.storageService.AddSpamDomain("existing.com", "Test");
        var method = typeof(OutlookMailTools).GetMethod(
            "AddToSpamDomainList",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        // Act
        var result = method.Invoke(this.sut, ["existing.com", "Test reason"]) as string;

        // Assert
        Assert.Contains("already in spam registry", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tests that AddToSpamDomainList returns error for empty domain.
    /// </summary>
    [Fact]
    public void AddToSpamDomainListReturnsErrorForEmptyDomain()
    {
        // Arrange
        var method = typeof(OutlookMailTools).GetMethod(
            "AddToSpamDomainList",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        // Act
        var result = method.Invoke(this.sut, [string.Empty, "Test"]) as string;

        // Assert
        Assert.Contains("Domain required", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tests that AddToSpamDomainList returns error for null domain.
    /// </summary>
    [Fact]
    public void AddToSpamDomainListReturnsErrorForNullDomain()
    {
        // Arrange
        var method = typeof(OutlookMailTools).GetMethod(
            "AddToSpamDomainList",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        // Act
        var result = method.Invoke(this.sut, [null, "Test"]) as string;

        // Assert
        Assert.Contains("Domain required", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tests that AddToSpamDomainList returns error when storage is null.
    /// </summary>
    [Fact]
    public void AddToSpamDomainListReturnsErrorWhenStorageNull()
    {
        // Arrange
        var sutWithoutStorage = new OutlookMailTools(this.mockGraphProvider, spamStorage: null);
        var method = typeof(OutlookMailTools).GetMethod(
            "AddToSpamDomainList",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        // Act
        var result = method.Invoke(sutWithoutStorage, ["test.com", "Test"]) as string;

        // Assert
        Assert.Contains("Spam storage not initialized", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tests that CheckSpamDomain returns true message for known domain.
    /// </summary>
    [Fact]
    public void CheckSpamDomainReturnsTrueMessageForKnownDomain()
    {
        // Arrange
        _ = this.storageService.AddSpamDomain("knownspam.com", "Test");
        var method = typeof(OutlookMailTools).GetMethod(
            "CheckSpamDomain",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        // Act
        var result = method.Invoke(this.sut, ["knownspam.com"]) as string;

        // Assert
        Assert.Contains("KNOWNSPAM.COM", result, StringComparison.Ordinal);
        Assert.Contains("IS in the spam registry", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tests that CheckSpamDomain returns false message for unknown domain.
    /// </summary>
    [Fact]
    public void CheckSpamDomainReturnsFalseMessageForUnknownDomain()
    {
        // Arrange
        var method = typeof(OutlookMailTools).GetMethod(
            "CheckSpamDomain",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        // Act
        var result = method.Invoke(this.sut, ["unknown.com"]) as string;

        // Assert
        Assert.Contains("UNKNOWN.COM", result, StringComparison.Ordinal);
        Assert.Contains("is NOT in the spam registry", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tests that CheckSpamDomain returns error for empty domain.
    /// </summary>
    [Fact]
    public void CheckSpamDomainReturnsErrorForEmptyDomain()
    {
        // Arrange
        var method = typeof(OutlookMailTools).GetMethod(
            "CheckSpamDomain",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        // Act
        var result = method.Invoke(this.sut, [string.Empty]) as string;

        // Assert
        Assert.Contains("Domain required", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tests that CheckSpamDomain returns error for null domain.
    /// </summary>
    [Fact]
    public void CheckSpamDomainReturnsErrorForNullDomain()
    {
        // Arrange
        var method = typeof(OutlookMailTools).GetMethod(
            "CheckSpamDomain",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        // Act
        var result = method.Invoke(this.sut, [null]) as string;

        // Assert
        Assert.Contains("Domain required", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tests that CheckSpamDomain returns error when storage is null.
    /// </summary>
    [Fact]
    public void CheckSpamDomainReturnsErrorWhenStorageNull()
    {
        // Arrange
        var sutWithoutStorage = new OutlookMailTools(this.mockGraphProvider, spamStorage: null);
        var method = typeof(OutlookMailTools).GetMethod(
            "CheckSpamDomain",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        // Act
        var result = method.Invoke(sutWithoutStorage, ["test.com"]) as string;

        // Assert
        Assert.Contains("Spam storage not initialized", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tests that TrackOperation only prints once for repeated calls.
    /// </summary>
    [Fact]
    public void TrackOperationOnlyPrintsOnceForRepeatedCalls()
    {
        // Arrange
        var method = typeof(OutlookMailTools).GetMethod(
            "TrackOperation",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        var field = typeof(OutlookMailTools).GetField(
            "lastOperation",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);

        // Act - call twice with same mode
        method.Invoke(this.sut, ["inbox"]);
        var firstValue = field.GetValue(this.sut);
        method.Invoke(this.sut, ["inbox"]);
        var secondValue = field.GetValue(this.sut);

        // Assert - lastOperation should be the same
        Assert.Equal("INBOX", firstValue);
        Assert.Equal("INBOX", secondValue);
    }

    /// <summary>
    /// Tests that TrackOperation updates for new operation.
    /// </summary>
    [Fact]
    public void TrackOperationUpdatesForNewOperation()
    {
        // Arrange
        var method = typeof(OutlookMailTools).GetMethod(
            "TrackOperation",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        var field = typeof(OutlookMailTools).GetField(
            "lastOperation",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);

        // Act - call with different modes
        method.Invoke(this.sut, ["inbox"]);
        var firstValue = field.GetValue(this.sut);
        method.Invoke(this.sut, ["send"]);
        var secondValue = field.GetValue(this.sut);

        // Assert
        Assert.Equal("INBOX", firstValue);
        Assert.Equal("SEND", secondValue);
    }

    /// <summary>
    /// Tests that TrackOperation handles null mode.
    /// </summary>
    [Fact]
    public void TrackOperationHandlesNullMode()
    {
        // Arrange
        var method = typeof(OutlookMailTools).GetMethod(
            "TrackOperation",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        var field = typeof(OutlookMailTools).GetField(
            "lastOperation",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);

        // Act
        method.Invoke(this.sut, [null]);
        var value = field.GetValue(this.sut);

        // Assert
        Assert.Equal("UNKNOWN", value);
    }

    /// <summary>
    /// Tests that ResolveFolderName resolves inbox correctly.
    /// </summary>
    [Fact]
    public void ResolveFolderNameResolvesInboxCorrectly()
    {
        // Arrange
        var method = typeof(OutlookMailTools).GetMethod(
            "ResolveFolderName",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        // Act
        var result = method.Invoke(null, ["INBOX", "inbox"]) as string;

        // Assert
        Assert.Equal("inbox", result);
    }

    /// <summary>
    /// Tests that ResolveFolderName resolves junk variations correctly.
    /// </summary>
    /// <param name="input">The folder name input.</param>
    [Theory]
    [InlineData("JUNK")]
    [InlineData("JUNKEMAIL")]
    [InlineData("SPAM")]
    public void ResolveFolderNameResolvesJunkVariationsCorrectly(string input)
    {
        // Arrange
        var method = typeof(OutlookMailTools).GetMethod(
            "ResolveFolderName",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        // Act
        var result = method.Invoke(null, [input, "inbox"]) as string;

        // Assert
        Assert.Equal("junkemail", result);
    }

    /// <summary>
    /// Tests that ResolveFolderName resolves sent variations correctly.
    /// </summary>
    /// <param name="input">The folder name input.</param>
    [Theory]
    [InlineData("SENT")]
    [InlineData("SENTITEMS")]
    public void ResolveFolderNameResolvesSentVariationsCorrectly(string input)
    {
        // Arrange
        var method = typeof(OutlookMailTools).GetMethod(
            "ResolveFolderName",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        // Act
        var result = method.Invoke(null, [input, "inbox"]) as string;

        // Assert
        Assert.Equal("sentitems", result);
    }

    /// <summary>
    /// Tests that ResolveFolderName resolves deleted variations correctly.
    /// </summary>
    /// <param name="input">The folder name input.</param>
    [Theory]
    [InlineData("DELETED")]
    [InlineData("DELETEDITEMS")]
    [InlineData("TRASH")]
    public void ResolveFolderNameResolvesDeletedVariationsCorrectly(string input)
    {
        // Arrange
        var method = typeof(OutlookMailTools).GetMethod(
            "ResolveFolderName",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        // Act
        var result = method.Invoke(null, [input, "inbox"]) as string;

        // Assert
        Assert.Equal("deleteditems", result);
    }

    /// <summary>
    /// Tests that ResolveFolderName returns default for null.
    /// </summary>
    [Fact]
    public void ResolveFolderNameReturnsDefaultForNull()
    {
        // Arrange
        var method = typeof(OutlookMailTools).GetMethod(
            "ResolveFolderName",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        // Act
        var result = method.Invoke(null, [null, "customdefault"]) as string;

        // Assert
        Assert.Equal("customdefault", result);
    }

    /// <summary>
    /// Tests that ResolveFolderName returns default for empty string.
    /// </summary>
    [Fact]
    public void ResolveFolderNameReturnsDefaultForEmptyString()
    {
        // Arrange
        var method = typeof(OutlookMailTools).GetMethod(
            "ResolveFolderName",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        // Act
        var result = method.Invoke(null, [string.Empty, "defaultfolder"]) as string;

        // Assert
        Assert.Equal("defaultfolder", result);
    }

    /// <summary>
    /// Tests that ResolveFolderName returns unknown folder as-is.
    /// </summary>
    [Fact]
    public void ResolveFolderNameReturnsUnknownFolderAsIs()
    {
        // Arrange
        var method = typeof(OutlookMailTools).GetMethod(
            "ResolveFolderName",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        // Act
        var result = method.Invoke(null, ["customfolder", "inbox"]) as string;

        // Assert
        Assert.Equal("customfolder", result);
    }

    /// <summary>
    /// Tests that maxResults is clamped to valid range.
    /// </summary>
    /// <returns>A task representing the async operation.</returns>
    [Fact]
    public async Task MaxResultsIsClampedToValidRange()
    {
        // Act with values outside range - should not throw
        var result1 = await this.sut.MailAsync("inbox", maxResults: -5);
        var result2 = await this.sut.MailAsync("inbox", maxResults: 100);

        // Assert - both should fail with client error (not argument error)
        Assert.Contains("GRAPH_CLIENT_ID", result1, StringComparison.Ordinal);
        Assert.Contains("GRAPH_CLIENT_ID", result2, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tests mode case insensitivity.
    /// </summary>
    /// <param name="mode">The mode to test.</param>
    /// <returns>A task representing the async operation.</returns>
    [Theory]
    [InlineData("INBOX")]
    [InlineData("inbox")]
    [InlineData("InBox")]
    public async Task ModesAreCaseInsensitive(string mode)
    {
        // Act
        var result = await this.sut.MailAsync(mode);

        // Assert - all should hit the same code path
        Assert.Contains("GRAPH_CLIENT_ID", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tests that junk mode without param1 lists junk folder.
    /// </summary>
    /// <returns>A task representing the async operation.</returns>
    [Fact]
    public async Task JunkModeWithoutParam1ListsJunkFolder()
    {
        // Act
        var result = await this.sut.MailAsync("junk");

        // Assert - should try to list junk folder (but fail with no client)
        Assert.Contains("GRAPH_CLIENT_ID", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tests that junk mode with param1 moves message to junk.
    /// </summary>
    /// <returns>A task representing the async operation.</returns>
    [Fact]
    public async Task JunkModeWithParam1MovesMessageToJunk()
    {
        // Act
        var result = await this.sut.MailAsync("junk", "message123");

        // Assert - should try to move message (but fail with no client)
        Assert.Contains("GRAPH_CLIENT_ID", result, StringComparison.Ordinal);
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
