// <copyright file="SpamFilterTools.cs" company="HemSoft">
// Copyright © 2025 HemSoft
// </copyright>

namespace HemSoft.PowerAI.Console.Tools;

using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using HemSoft.PowerAI.Console.Models;
using HemSoft.PowerAI.Console.Services;

using Microsoft.Graph.Models.ODataErrors;

using Spectre.Console;

/// <summary>
/// Callback invoked when an email is evaluated.
/// </summary>
/// <param name="evaluation">The email evaluation result.</param>
internal delegate void EmailEvaluatedCallback(EmailEvaluation evaluation);

/// <summary>
/// Provides spam filtering tools for the AI agent.
/// </summary>
/// <param name="storageService">The spam storage service for persisting spam domain data.</param>
/// <param name="graphClientProvider">The Graph client provider for API access.</param>
/// <param name="timeProvider">The time provider to use. Defaults to system time.</param>
internal sealed partial class SpamFilterTools(
    SpamStorageService storageService,
    IGraphClientProvider graphClientProvider,
    TimeProvider? timeProvider = null) : IDisposable
{
    private const string FolderInbox = "inbox";
    private const string FolderJunk = "junkemail";

    private static readonly ActivitySource GraphApiActivitySource = new("HemSoft.PowerAI.GraphApi", "1.0.0");

    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;
    private readonly HashSet<string> processedMessageIds = new(StringComparer.Ordinal);
    private EmailEvaluatedCallback? onEmailEvaluated;
    private int skipCount;
    private bool disposed;

    [GeneratedRegex(@"<[^>]+>", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex HtmlTagRegex { get; }

    [GeneratedRegex(@"\s+", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex WhitespaceRegex { get; }

    /// <summary>
    /// Sets the callback to be invoked when an email is evaluated.
    /// </summary>
    /// <param name="callback">The callback to invoke.</param>
    public void SetEvaluationCallback(EmailEvaluatedCallback? callback) => this.onEmailEvaluated = callback;

    /// <summary>
    /// Gets the list of known spam domains.
    /// </summary>
    /// <returns>JSON array of known spam domains.</returns>
    [Description("Gets the list of all known spam domains that should be immediately moved to junk.")]
    public string GetKnownSpamDomains()
    {
        var domains = storageService.GetSpamDomains();
        AnsiConsole.MarkupLine(CultureInfo.InvariantCulture, $"[dim]  → Loaded {domains.Count} known spam domains[/]");
        return JsonSerializer.Serialize(domains.Select(d => d.Domain));
    }

    /// <summary>
    /// Checks if a specific domain is in the known spam list.
    /// </summary>
    /// <param name="domain">The domain to check.</param>
    /// <returns>True if known spam, false otherwise.</returns>
    [Description("Checks if a specific email domain is in the known spam list.")]
    public bool IsKnownSpamDomain(string domain)
    {
        var isKnown = storageService.IsKnownSpamDomain(domain);
        if (isKnown)
        {
            AnsiConsole.MarkupLine($"[red]  → Known spam: {Markup.Escape(domain)}[/]");
        }

        return isKnown;
    }

    /// <summary>
    /// Reports an email evaluation for display purposes.
    /// </summary>
    /// <param name="messageId">The message ID.</param>
    /// <param name="senderEmail">The sender's email address.</param>
    /// <param name="subject">The email subject.</param>
    /// <param name="verdict">The verdict (Legitimate, Known Spam, Candidate, Junked).</param>
    /// <param name="reason">Brief reason for the verdict.</param>
    /// <returns>Confirmation message.</returns>
    [Description(
        "Reports the evaluation result for an email. REQUIRED parameters: messageId (the 'id' from GetInboxEmailsAsync), " +
        "senderEmail, subject, verdict ('Legitimate'/'Junked'/'Candidate'), reason.")]
    public string ReportEmailEvaluation(string messageId, string senderEmail, string subject, string verdict, string? reason = null)
    {
        this.onEmailEvaluated?.Invoke(new EmailEvaluation
        {
            MessageId = messageId ?? string.Empty,
            Sender = senderEmail ?? string.Empty,
            Subject = subject ?? string.Empty,
            Verdict = verdict ?? "Unknown",
            Reason = reason,
        });

        return "Evaluation recorded.";
    }

    /// <summary>
    /// Fetches a batch of emails from the inbox that haven't been processed yet.
    /// </summary>
    /// <param name="batchSize">Number of emails to fetch.</param>
    /// <returns>JSON array of email summaries with id, subject, sender, and receivedDateTime.</returns>
    [Description(
        "Fetches a batch of unprocessed emails from the inbox. Returns JSON array with id, subject, senderEmail, " +
        "senderDomain, and receivedDateTime for each email. Returns empty array when all emails have been processed.")]
    public async Task<string> GetInboxEmailsAsync(int batchSize = 10)
    {
        using var activity = GraphApiActivitySource.StartActivity("GetInboxEmails");
        _ = activity?.SetTag("batch.size", batchSize);

        var client = graphClientProvider.Client;
        if (client is null)
        {
            _ = activity?.SetStatus(ActivityStatusCode.Error, "Missing client ID");
            return $"Error: Set {SharedGraphClient.ClientIdEnvVar} environment variable.";
        }

        try
        {
            var messages = await client.Me.MailFolders[FolderInbox].Messages.GetAsync(config =>
            {
                config.Headers.Add("Prefer", "IdType=\"ImmutableId\"");
                config.QueryParameters.Top = batchSize;
                config.QueryParameters.Skip = this.skipCount;
                config.QueryParameters.Select = ["id", "subject", "from", "receivedDateTime", "isRead"];
                config.QueryParameters.Orderby = ["receivedDateTime desc"];
            }).ConfigureAwait(false);

            if (messages?.Value is not { Count: > 0 })
            {
                _ = activity?.SetTag("emails.fetched", 0);
                return "[]";
            }

            // Filter out any we might have seen (defensive) and track new ones
            var unprocessedEmails = messages.Value
                .Where(m => m.Id is not null && !this.processedMessageIds.Contains(m.Id))
                .ToList();

            // Advance skip for next batch
            this.skipCount += messages.Value.Count;

            _ = activity?.SetTag("emails.fetched", unprocessedEmails.Count);
            _ = activity?.SetTag("skip.count", this.skipCount);

            if (unprocessedEmails.Count == 0)
            {
                AnsiConsole.MarkupLine("[dim]  → No unprocessed emails found[/]");
                return "[]";
            }

            AnsiConsole.MarkupLine($"[dim]  → Fetched {unprocessedEmails.Count} emails to process[/]");

            // Mark these as processed so we don't fetch them again
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
                    isRead = m.IsRead,
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
    [Description(
        "Reads the full content of an email by its ID. " +
        "Returns subject, sender, body preview, and other details for spam analysis.")]
    public async Task<string> ReadEmailAsync(string messageId)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        AnsiConsole.MarkupLine("[dim]  → Reading email content...[/]");

        using var activity = GraphApiActivitySource.StartActivity("ReadEmail");
        _ = activity?.SetTag("message.id", messageId);

        var client = graphClientProvider.Client;
        if (client is null)
        {
            _ = activity?.SetStatus(ActivityStatusCode.Error, "Missing client ID");
            return $"Error: Set {SharedGraphClient.ClientIdEnvVar} environment variable.";
        }

        try
        {
            // Use MailFolders[inbox].Messages to match how we fetched the IDs in GetInboxEmailsAsync
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
    /// Moves an email to the junk folder.
    /// </summary>
    /// <param name="messageId">The message ID to move.</param>
    /// <returns>Success or error message.</returns>
    [Description(
        "Moves an email to the junk folder immediately. " +
        "You MUST call this for emails from known spam domains BEFORE calling ReportEmailEvaluation.")]
    public async Task<string> MoveToJunkAsync(string messageId)
    {
        using var activity = GraphApiActivitySource.StartActivity("MoveToJunk");
        _ = activity?.SetTag("message.id", messageId);

        var client = graphClientProvider.Client;
        if (client is null)
        {
            _ = activity?.SetStatus(ActivityStatusCode.Error, "Missing client ID");
            return $"Error: Set {SharedGraphClient.ClientIdEnvVar} environment variable.";
        }

        if (string.IsNullOrEmpty(messageId))
        {
            _ = activity?.SetStatus(ActivityStatusCode.Error, "Missing message ID");
            return "Error: messageId is required.";
        }

        try
        {
            // Use MailFolders[inbox].Messages to match how we fetched the IDs in GetInboxEmailsAsync
            _ = await client.Me.MailFolders[FolderInbox].Messages[messageId].Move.PostAsync(
                new Microsoft.Graph.Me.MailFolders.Item.Messages.Item.Move.MovePostRequestBody
                {
                    DestinationId = FolderJunk,
                }).ConfigureAwait(false);

            // Track as processed so we don't see it again
            _ = this.processedMessageIds.Add(messageId);

            AnsiConsole.MarkupLine("[red]  → Moved to junk[/]");
            _ = activity?.SetTag("move.success", value: true);
            return "Successfully moved message to junk folder.";
        }
        catch (ODataError ex)
        {
            _ = activity?.SetStatus(ActivityStatusCode.Error, ex.Error?.Message ?? ex.Message);
            return $"Error moving to junk: {ex.Error?.Message ?? ex.Message}";
        }
    }

    /// <summary>
    /// Records a spam candidate for later human review.
    /// </summary>
    /// <param name="messageId">The message ID.</param>
    /// <param name="senderEmail">The sender's email address.</param>
    /// <param name="subject">The email subject.</param>
    /// <param name="spamReason">The AI's reasoning for flagging this as spam.</param>
    /// <param name="confidenceScore">Confidence score from 0.0 to 1.0.</param>
    /// <returns>Success or error message.</returns>
    [Description(
        "Records an email as a spam candidate for later human review. " +
        "Use this when AI analysis suggests the email is spam but the domain is not in the known list.")]
    public string RecordSpamCandidate(
        string messageId,
        string senderEmail,
        string subject,
        string spamReason,
        double confidenceScore)
    {
        var candidate = new SpamCandidate
        {
            MessageId = messageId,
            SenderEmail = senderEmail,
            SenderDomain = ExtractDomain(senderEmail),
            Subject = subject,
            SpamReason = spamReason,
            ConfidenceScore = Math.Clamp(confidenceScore, 0.0, 1.0),
            ReceivedAt = this.timeProvider.GetUtcNow(),
            IdentifiedAt = this.timeProvider.GetUtcNow(),
        };

        var added = storageService.AddSpamCandidate(candidate);
        if (added)
        {
            AnsiConsole.MarkupLine($"[yellow]  → Flagged as spam candidate: {Markup.Escape(candidate.SenderDomain)}[/]");
        }

        return added
            ? $"Recorded spam candidate from {candidate.SenderDomain}"
            : $"Candidate already recorded for message {messageId}";
    }

    /// <summary>
    /// Adds a domain to the known spam list.
    /// </summary>
    /// <param name="domain">The domain to add.</param>
    /// <param name="reason">Reason for marking as spam.</param>
    /// <returns>Success or error message.</returns>
    [Description("Adds a domain to the known spam list. Future emails from this domain will be moved to junk immediately.")]
    public string AddToSpamDomainList(string domain, string reason)
    {
        var added = storageService.AddSpamDomain(domain, reason);
        return added
            ? $"Added {domain} to spam domain list"
            : $"Domain {domain} already in spam list";
    }

    /// <summary>
    /// Gets all pending spam candidates grouped by domain for human review.
    /// </summary>
    /// <returns>JSON object with domains as keys and candidate arrays as values.</returns>
    [Description("Gets all pending spam candidates grouped by sender domain for human review.")]
    public string GetPendingSpamCandidatesByDomain()
    {
        var grouped = storageService.GetCandidatesGroupedByDomain();
        var result = grouped.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Select(c => new
            {
                c.MessageId,
                c.SenderEmail,
                c.Subject,
                c.SpamReason,
                c.ConfidenceScore,
            }),
            StringComparer.OrdinalIgnoreCase);

        return JsonSerializer.Serialize(result);
    }

    /// <summary>
    /// Processes approved domains by adding them to the spam list and moving their emails to junk.
    /// </summary>
    /// <param name="domain">The domain that was approved as spam.</param>
    /// <returns>Summary of actions taken.</returns>
    [Description("Processes a domain after human approval - adds to spam list and moves all pending emails from that domain to junk.")]
    public async Task<string> ProcessApprovedSpamDomainAsync(string domain)
    {
        using var activity = GraphApiActivitySource.StartActivity("ProcessApprovedSpamDomain");
        _ = activity?.SetTag("domain", domain);

        // Add to spam list
        _ = storageService.AddSpamDomain(domain, "Approved by user");

        // Get all candidates from this domain
        var candidates = storageService.GetSpamCandidates()
            .Where(c => c.SenderDomain.Equals(domain, StringComparison.OrdinalIgnoreCase))
            .ToList();

        _ = activity?.SetTag("candidates.count", candidates.Count);

        var movedCount = 0;
        var errors = new List<string>();

        foreach (var candidate in candidates)
        {
            var result = await this.MoveToJunkAsync(candidate.MessageId).ConfigureAwait(false);
            if (result.StartsWith("Successfully", StringComparison.Ordinal))
            {
                _ = storageService.RemoveSpamCandidate(candidate.MessageId);
                movedCount++;
            }
            else
            {
                errors.Add($"{candidate.SenderEmail}: {result}");
            }
        }

        _ = activity?.SetTag("emails.moved", movedCount);
        _ = activity?.SetTag("errors.count", errors.Count);

        var sb = new StringBuilder();
        _ = sb.Append(CultureInfo.InvariantCulture, $"Processed domain {domain}: ");
        _ = sb.Append(CultureInfo.InvariantCulture, $"Added to spam list, moved {movedCount} emails to junk.");

        if (errors.Count > 0)
        {
            _ = sb.Append(CultureInfo.InvariantCulture, $" Errors: {errors.Count}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Clears all processed spam candidates.
    /// </summary>
    /// <returns>Confirmation message.</returns>
    [Description("Clears all spam candidates that have been processed.")]
    public string ClearProcessedCandidates()
    {
        storageService.ClearSpamCandidates();
        return "Cleared all spam candidates.";
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        // SharedGraphClient owns the client - don't dispose it here
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

        // Strip HTML tags for a cleaner preview
        var text = HtmlTagRegex.Replace(body, " ");
        text = WhitespaceRegex.Replace(text, " ").Trim();

        return text.Length <= maxLength ? text : text[..maxLength] + "...";
    }
}
