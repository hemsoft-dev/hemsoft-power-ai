// <copyright file="SpamScanTools.cs" company="HemSoft">
// Copyright © 2025 HemSoft
// </copyright>

namespace HemSoft.PowerAI.Console.Agents;

using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

using HemSoft.PowerAI.Console.Services;

using Microsoft.Graph.Models.ODataErrors;

/// <summary>
/// Callback for reporting scan results.
/// </summary>
/// <param name="result">The scan result.</param>
internal delegate void ScanResultCallback(SpamScanAgent.ScanResult result);

/// <summary>
/// Provides tools for the SpamScanAgent - read-only operations plus flagging for review.
/// </summary>
/// <param name="storageService">The spam storage service.</param>
/// <param name="humanReviewService">The human review service.</param>
/// <param name="graphClientProvider">The Graph client provider for API access.</param>
internal sealed partial class SpamScanTools(
    SpamStorageService storageService,
    HumanReviewService humanReviewService,
    IGraphClientProvider graphClientProvider) : IDisposable
{
    private const string FolderInbox = "inbox";

    private static readonly ActivitySource GraphApiActivitySource = new("HemSoft.PowerAI.GraphApi", "1.0.0");

    private readonly HashSet<string> processedMessageIds = new(StringComparer.Ordinal);
    private ScanResultCallback? onScanResult;
    private bool disposed;

    [GeneratedRegex(@"<[^>]+>", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex HtmlTagRegex { get; }

    [GeneratedRegex(@"\s+", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex WhitespaceRegex { get; }

    /// <summary>
    /// Sets the callback to be invoked when a scan result is reported.
    /// </summary>
    /// <param name="callback">The callback to invoke.</param>
    public void SetResultCallback(ScanResultCallback? callback) => this.onScanResult = callback;

    /// <summary>
    /// Gets the count of domains pending review.
    /// </summary>
    /// <returns>The count of pending domains.</returns>
    public int GetPendingReviewCount() => humanReviewService.GetPendingCount();

    /// <summary>
    /// Gets the list of known spam domains.
    /// </summary>
    /// <returns>JSON array of known spam domains.</returns>
    [Description("Gets the list of all known spam domains. Skip emails from these domains - they're already handled.")]
    public string GetKnownSpamDomains()
    {
        var domains = storageService.GetSpamDomains();
        return JsonSerializer.Serialize(domains.Select(d => d.Domain));
    }

    /// <summary>
    /// Gets the list of domains already pending human review.
    /// </summary>
    /// <returns>JSON array of domains pending review.</returns>
    [Description("Gets domains already flagged and pending human review. Skip emails from these domains.")]
    public string GetPendingReviewDomains()
    {
        var domains = humanReviewService.GetPendingDomains();
        return JsonSerializer.Serialize(domains.Select(d => d.Domain));
    }

    /// <summary>
    /// Fetches a batch of emails from the inbox that haven't been processed yet.
    /// </summary>
    /// <param name="batchSize">Number of emails to fetch.</param>
    /// <returns>JSON array of email summaries.</returns>
    [Description("Fetches a batch of unprocessed emails from the inbox. Returns JSON array with id, subject, senderEmail, senderDomain.")]
    public async Task<string> GetInboxEmailsAsync(int batchSize = 10)
    {
        using var activity = GraphApiActivitySource.StartActivity("GetInboxEmails");
        _ = activity?.SetTag("batch.size", batchSize);

        var client = graphClientProvider.Client;
        if (client is null)
        {
            _ = activity?.SetStatus(ActivityStatusCode.Error, "Missing client ID");
            return "Error: Set GRAPH_CLIENT_ID environment variable.";
        }

        try
        {
            var fetchSize = Math.Min(batchSize * 3, 50);
            var messages = await client.Me.MailFolders[FolderInbox].Messages.GetAsync(config =>
            {
                config.QueryParameters.Top = fetchSize;
                config.QueryParameters.Select = ["id", "subject", "from", "receivedDateTime"];
                config.QueryParameters.Orderby = ["receivedDateTime desc"];
            }).ConfigureAwait(false);

            if (messages?.Value is not { Count: > 0 })
            {
                _ = activity?.SetTag("emails.fetched", 0);
                return "[]";
            }

            var unprocessedEmails = messages.Value
                .Where(m => m.Id is not null && !this.processedMessageIds.Contains(m.Id))
                .Take(batchSize)
                .ToList();

            _ = activity?.SetTag("emails.fetched", unprocessedEmails.Count);

            if (unprocessedEmails.Count == 0)
            {
                return "[]";
            }

            foreach (var email in unprocessedEmails)
            {
                _ = this.processedMessageIds.Add(email.Id!);
            }

            var result = unprocessedEmails.Select(m =>
            {
                var senderEmail = m.From?.EmailAddress?.Address ?? "unknown";
                var senderDomain = ExtractDomain(senderEmail);
                return new
                {
                    id = m.Id,
                    subject = m.Subject,
                    senderEmail,
                    senderDomain,
                    receivedDateTime = m.ReceivedDateTime?.ToString("o", CultureInfo.InvariantCulture),
                };
            });

            return JsonSerializer.Serialize(result);
        }
        catch (ODataError ex)
        {
            _ = activity?.SetStatus(ActivityStatusCode.Error, ex.Error?.Message ?? ex.Message);
            return $"Error fetching emails: {ex.Error?.Message ?? ex.Message}";
        }
    }

    /// <summary>
    /// Reads the full content of an email by its ID.
    /// </summary>
    /// <param name="messageId">The message ID.</param>
    /// <returns>Full email details including body.</returns>
    [Description("Reads the full content of an email by its ID for spam analysis.")]
    public async Task<string> ReadEmailAsync(string messageId)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);

        using var activity = GraphApiActivitySource.StartActivity("ReadEmail");
        _ = activity?.SetTag("message.id", messageId);

        var client = graphClientProvider.Client;
        if (client is null)
        {
            _ = activity?.SetStatus(ActivityStatusCode.Error, "Missing client ID");
            return "Error: Set GRAPH_CLIENT_ID environment variable.";
        }

        try
        {
            var message = await client.Me.MailFolders[FolderInbox].Messages[messageId].GetAsync(config =>
                config.QueryParameters.Select = ["id", "subject", "from", "receivedDateTime", "bodyPreview", "body"]).ConfigureAwait(false);

            if (message is null)
            {
                _ = activity?.SetStatus(ActivityStatusCode.Error, "Message not found");
                return "Message not found";
            }

            var senderEmail = message.From?.EmailAddress?.Address ?? "unknown";
            _ = activity?.SetTag("sender.domain", ExtractDomain(senderEmail));

            var result = new
            {
                id = message.Id,
                subject = message.Subject,
                senderEmail,
                senderDomain = ExtractDomain(senderEmail),
                receivedDateTime = message.ReceivedDateTime?.ToString("o", CultureInfo.InvariantCulture),
                bodyPreview = message.BodyPreview,
                bodySnippet = TruncateBody(message.Body?.Content, 500),
            };

            return JsonSerializer.Serialize(result);
        }
        catch (ODataError ex)
        {
            _ = activity?.SetStatus(ActivityStatusCode.Error, ex.Error?.Message ?? ex.Message);
            return $"Error reading email: {ex.Error?.Message ?? ex.Message}";
        }
    }

    /// <summary>
    /// Flags a domain for human review with sample email information.
    /// </summary>
    /// <param name="domain">The domain to flag.</param>
    /// <param name="messageId">The message ID of the sample email.</param>
    /// <param name="senderEmail">The sender's email address.</param>
    /// <param name="subject">The email subject.</param>
    /// <param name="reason">The reason this domain was flagged.</param>
    /// <returns>Confirmation message.</returns>
    [Description("Flags a domain for human review. Call this when you find a suspicious email from a new domain.")]
    public string FlagDomainForReview(string domain, string messageId, string senderEmail, string subject, string reason)
    {
        var isNew = humanReviewService.AddOrUpdateDomain(domain, messageId, senderEmail, subject, reason);
        return isNew
            ? $"Flagged new domain {domain} for review"
            : $"Updated existing flag for domain {domain}";
    }

    /// <summary>
    /// Reports a scan result for display purposes.
    /// </summary>
    /// <param name="domain">The sender's domain.</param>
    /// <param name="subject">The email subject.</param>
    /// <param name="status">The scan status (Flagged, Skipped, Clean).</param>
    /// <param name="reason">Brief reason for the status.</param>
    /// <returns>Confirmation message.</returns>
    [Description(
        "Reports the scan result for an email. Call this for EVERY email processed. " +
        "Status should be 'Flagged', 'Skipped', or 'Clean'.")]
    public string ReportScanResult(string domain, string subject, string status, string? reason = null)
    {
        this.onScanResult?.Invoke(new SpamScanAgent.ScanResult
        {
            Domain = domain ?? "unknown",
            Subject = subject ?? "(no subject)",
            Status = status ?? "Unknown",
            Reason = reason,
        });

        return "Scan result recorded.";
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        // GraphClient is managed by the shared factory, not disposed here
        this.disposed = true;
    }

    private static string ExtractDomain(string email)
    {
        var atIndex = email.LastIndexOf('@');
        return atIndex >= 0 && atIndex < email.Length - 1
            ? email[(atIndex + 1)..].ToUpperInvariant()
            : email.ToUpperInvariant();
    }

    private static string TruncateBody(string? body, int maxLength)
    {
        if (string.IsNullOrEmpty(body))
        {
            return string.Empty;
        }

        var text = HtmlTagRegex.Replace(body, " ");
        text = WhitespaceRegex.Replace(text, " ").Trim();

        return text.Length <= maxLength ? text : text[..maxLength] + "...";
    }
}
