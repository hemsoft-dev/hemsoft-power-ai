// <copyright file="SpamScanAgent.cs" company="HemSoft">
// Copyright © 2025 HemSoft
// </copyright>

namespace HemSoft.PowerAI.Console.Agents;

using System.ClientModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;

using HemSoft.PowerAI.Console.Configuration;
using HemSoft.PowerAI.Console.Extensions;
using HemSoft.PowerAI.Console.Services;

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

using OpenAI;

using Spectre.Console;

/// <summary>
/// Autonomous agent that scans the inbox for spam and identifies domains for human review.
/// Does not delete emails - only identifies suspicious domains and adds them to HumanReview.json.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Agent requires OpenRouter API and Graph API authentication")]
internal sealed partial class SpamScanAgent : IDisposable
{
    private const string ModelId = "x-ai/grok-4.1-fast:free";
    private const string OpenRouterBaseUrlEnvVar = "OPENROUTER_BASE_URL";
    private const string ApiKeyEnvVar = "OPENROUTER_API_KEY";

    private const string AgentInstructions = """
        You are an autonomous spam scanner. Your job is to identify suspicious email domains for later human review.
        You do NOT delete or move any emails - you only identify and flag domains.

        ## Your workflow for each batch of emails:

        1. First, get the list of known spam domains using GetKnownSpamDomains.
        2. Check which domains are already pending review using GetPendingReviewDomains.
        3. Fetch a batch of emails from the inbox using GetInboxEmailsAsync.
        4. For each email:
           a. Check if the sender's domain is already in the known spam list (skip it - already handled)
           b. Check if the sender's domain is already pending review (skip it - already flagged)
           c. If neither, read the full email using ReadEmailAsync and analyze for spam indicators:
              - Suspicious links, urgent money requests, lottery/prize notifications
              - Pharmaceutical ads, adult content, phishing attempts
              - Unknown senders with generic greetings, too-good-to-be-true offers
           d. If suspicious (confidence > 0.7), call FlagDomainForReview with details
           e. Call ReportScanResult for EVERY email processed (for progress tracking)

        5. After processing the batch, report stats using EXACTLY this format on its own line:
           BATCH_STATS: processed=N, skipped_known=M, skipped_pending=K, flagged=L

        ## Important rules:
        - NEVER call MoveToJunk or any deletion functions - this is scan-only mode
        - Be conservative - only flag domains if you're reasonably confident they're spam
        - Skip domains that are already in the spam list or pending review
        - Always call ReportScanResult for every email
        - ALWAYS include the BATCH_STATS line at the end
        """;

    private readonly SpamFilterSettings settings;
    private readonly SpamScanTools tools;
    private readonly List<ScanResult> currentBatchResults = [];
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SpamScanAgent"/> class.
    /// </summary>
    /// <param name="settings">The spam filter settings.</param>
    public SpamScanAgent(SpamFilterSettings settings)
    {
        this.settings = settings;
        var storageService = new SpamStorageService(settings);
        var humanReviewService = new HumanReviewService(settings);
        var graphClientProvider = new DefaultGraphClientProvider();
        this.tools = new SpamScanTools(storageService, humanReviewService, graphClientProvider);
    }

    [GeneratedRegex(
        @"BATCH_STATS:\s*processed\s*=\s*(?<processed>\d+)\s*,\s*skipped_known\s*=\s*(?<skipped>\d+)\s*,\s*skipped_pending\s*=\s*(?<pending>\d+)\s*,\s*flagged\s*=\s*(?<flagged>\d+)",
        RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex BatchStatsRegex { get; }

    /// <summary>
    /// Runs the autonomous spam scanning workflow.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the async operation.</returns>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var (chatClient, validationError) = CreateChatClient();
        if (chatClient is null)
        {
            AnsiConsole.Write(new Panel(validationError!)
                .Header("[red]Configuration Error[/]")
                .Border(BoxBorder.Rounded));
            return;
        }

        using (chatClient)
        {
            var agent = this.CreateAgent(chatClient);
            DisplayHeader(this.settings);

            var stats = new RunStats();
            var consecutiveEmptyBatches = 0;

            while (!cancellationToken.IsCancellationRequested)
            {
                stats.Iteration++;
                AnsiConsole.MarkupLine($"\n[blue]═══ Scan Batch {stats.Iteration.ToInvariant()} ═══[/]");

                var batchResult = await this.ProcessScanBatchAsync(agent, this.settings.BatchSize, cancellationToken).ConfigureAwait(false);

                if (batchResult.InboxWasEmpty)
                {
                    consecutiveEmptyBatches++;
                    if (consecutiveEmptyBatches >= 2)
                    {
                        break;
                    }

                    AnsiConsole.MarkupLine("[dim]No more emails to scan.[/]");
                    await Task.Delay(this.settings.DelayBetweenBatchesSeconds * 1000, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                consecutiveEmptyBatches = 0;
                stats.TotalProcessed += batchResult.Processed;
                stats.TotalSkippedKnown += batchResult.SkippedKnown;
                stats.TotalSkippedPending += batchResult.SkippedPending;
                stats.TotalFlagged += batchResult.Flagged;

                AnsiConsole.MarkupLine(
                    $"[dim]Running totals: {stats.TotalProcessed.ToInvariant()} processed, " +
                    $"{stats.TotalFlagged.ToInvariant()} flagged[/]");

                await Task.Delay(this.settings.DelayBetweenBatchesSeconds * 1000, cancellationToken).ConfigureAwait(false);
            }

            DisplayFinalSummary(stats, this.tools.GetPendingReviewCount());
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.tools.Dispose();
        this.disposed = true;
    }

    private static void DisplayFinalSummary(RunStats stats, int pendingReviewCount)
    {
        AnsiConsole.MarkupLine("\n[green]═══ Scan Complete ═══[/]");
        AnsiConsole.MarkupLine($"[green]Total emails scanned: {stats.TotalProcessed.ToInvariant()}[/]");
        AnsiConsole.MarkupLine($"[green]Skipped (known spam): {stats.TotalSkippedKnown.ToInvariant()}[/]");
        AnsiConsole.MarkupLine($"[green]Skipped (pending review): {stats.TotalSkippedPending.ToInvariant()}[/]");
        AnsiConsole.MarkupLine($"[yellow]New domains flagged: {stats.TotalFlagged.ToInvariant()}[/]");
        AnsiConsole.MarkupLine($"\n[cyan]Total domains pending review: {pendingReviewCount.ToInvariant()}[/]");

        if (pendingReviewCount > 0)
        {
            AnsiConsole.MarkupLine("\n[yellow]Run /spam-review to process flagged domains.[/]");
        }
    }

    private static void DisplayHeader(SpamFilterSettings settings)
    {
        AnsiConsole.Write(new FigletText("Spam Scan").Color(Color.Blue));
        AnsiConsole.MarkupLine($"[dim]Model: {ModelId}[/]");
        AnsiConsole.MarkupLine(
            $"[dim]Batch Size: {settings.BatchSize.ToInvariant()} | " +
            $"Delay: {settings.DelayBetweenBatchesSeconds.ToInvariant()}s[/]\n");

        AnsiConsole.MarkupLine("[dim]Autonomous scan mode - identifying suspicious domains for review.[/]");
        AnsiConsole.MarkupLine("[dim]No emails will be moved or deleted.[/]");
        AnsiConsole.MarkupLine("[dim]Press Ctrl+C to stop.[/]\n");
    }

    private static (IChatClient? Client, string? Error) CreateChatClient()
    {
        var baseUrlValue = Environment.GetEnvironmentVariable(OpenRouterBaseUrlEnvVar);
        if (string.IsNullOrEmpty(baseUrlValue))
        {
            return (null, $"Missing {OpenRouterBaseUrlEnvVar} environment variable.\n\n" +
                          $"Set it with:\n$env:{OpenRouterBaseUrlEnvVar} = \"https://openrouter.ai/api/v1\"");
        }

        var apiKey = Environment.GetEnvironmentVariable(ApiKeyEnvVar);
        if (string.IsNullOrEmpty(apiKey))
        {
            return (null, $"Missing {ApiKeyEnvVar} environment variable.\n\n" +
                          $"Set it with:\n$env:{ApiKeyEnvVar} = \"your-api-key\"");
        }

        var openAiClient = new OpenAIClient(
            new ApiKeyCredential(apiKey),
            new OpenAIClientOptions { Endpoint = new Uri(baseUrlValue) });

        var chatClient = CompositeDisposableChatClient.CreateWithFunctionInvocation(openAiClient, ModelId);

        return (chatClient, null);
    }

    private static ScanBatchResult ParseBatchResult(string responseText)
    {
        var result = new ScanBatchResult();

        var upperResponse = responseText.ToUpperInvariant();
        if (upperResponse.Contains("NO EMAILS", StringComparison.Ordinal) ||
            upperResponse.Contains("INBOX IS EMPTY", StringComparison.Ordinal) ||
            upperResponse.Contains("INBOX EMPTY", StringComparison.Ordinal) ||
            upperResponse.Contains("NO MORE EMAILS", StringComparison.Ordinal) ||
            upperResponse.Contains("EMPTY ARRAY", StringComparison.Ordinal) ||
            upperResponse.Contains("[]", StringComparison.Ordinal) ||
            upperResponse.Contains("0 EMAILS IN", StringComparison.Ordinal))
        {
            result.InboxWasEmpty = true;
            return result;
        }

        var statsMatch = BatchStatsRegex.Match(responseText);

        if (statsMatch.Success)
        {
            result.Processed = int.Parse(statsMatch.Groups["processed"].Value, CultureInfo.InvariantCulture);
            result.SkippedKnown = int.Parse(statsMatch.Groups["skipped"].Value, CultureInfo.InvariantCulture);
            result.SkippedPending = int.Parse(statsMatch.Groups["pending"].Value, CultureInfo.InvariantCulture);
            result.Flagged = int.Parse(statsMatch.Groups["flagged"].Value, CultureInfo.InvariantCulture);
        }

        return result;
    }

    private static void DisplayScanResultsTable(List<ScanResult> results)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("[blue]#[/]").Width(3))
            .AddColumn(new TableColumn("[blue]Domain[/]").Width(25))
            .AddColumn(new TableColumn("[blue]Subject[/]").Width(35))
            .AddColumn(new TableColumn("[blue]Status[/]").Width(15))
            .AddColumn(new TableColumn("[blue]Reason[/]").Width(20));

        var rowNum = 1;
        foreach (var scan in results)
        {
            var statusColor = scan.Status.ToUpperInvariant() switch
            {
                "FLAGGED" => "yellow",
                "SKIPPED" => "dim",
                "CLEAN" => "green",
                _ => "white",
            };

            var domain = Truncate(scan.Domain, 23);
            var subject = Truncate(scan.Subject, 33);
            var reason = Truncate(scan.Reason ?? "-", 18);

            _ = table.AddRow(
                $"[dim]{rowNum.ToInvariant()}[/]",
                Markup.Escape(domain),
                Markup.Escape(subject),
                $"[{statusColor}]{Markup.Escape(scan.Status)}[/]",
                Markup.Escape(reason));
            rowNum++;
        }

        AnsiConsole.Write(table);
    }

    private static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..(maxLength - 1)] + "…";

    private async Task<ScanBatchResult> ProcessScanBatchAsync(AIAgent agent, int batchSize, CancellationToken cancellationToken)
    {
        var result = new ScanBatchResult();
        this.currentBatchResults.Clear();

        try
        {
            var prompt = $"Scan a batch of {batchSize.ToInvariant()} emails from the inbox. " +
                         "Follow your workflow to identify suspicious domains. " +
                         "Skip domains already in spam list or pending review. " +
                         "Flag new suspicious domains for human review. " +
                         "Call ReportScanResult for EVERY email you process.";

            string? responseText = null;

            this.tools.SetResultCallback(this.currentBatchResults.Add);

            try
            {
                AnsiConsole.MarkupLine("[blue]Scanning inbox...[/]");
                var response = await agent.RunAsync(prompt, cancellationToken: cancellationToken).ConfigureAwait(false);
                responseText = response.Text;
            }
            finally
            {
                this.tools.SetResultCallback(callback: null);
            }

            if (this.currentBatchResults.Count > 0)
            {
                DisplayScanResultsTable(this.currentBatchResults);
            }

            if (!string.IsNullOrEmpty(responseText))
            {
                result = ParseBatchResult(responseText);
            }
        }
        catch (HttpRequestException ex)
        {
            AnsiConsole.Write(new Panel($"[red]{Markup.Escape(ex.Message)}[/]")
                .Header("[red]Network Error[/]")
                .Border(BoxBorder.Rounded));
        }

        return result;
    }

    private ChatClientAgent CreateAgent(IChatClient chatClient) =>
        new(
            chatClient,
            instructions: AgentInstructions,
            name: "SpamScanAgent",
            description: "An autonomous agent that scans emails and identifies suspicious domains for review",
            tools:
            [
                AIFunctionFactory.Create(this.tools.GetKnownSpamDomains),
                AIFunctionFactory.Create(this.tools.GetPendingReviewDomains),
                AIFunctionFactory.Create(this.tools.GetInboxEmailsAsync),
                AIFunctionFactory.Create(this.tools.ReadEmailAsync),
                AIFunctionFactory.Create(this.tools.FlagDomainForReview),
                AIFunctionFactory.Create(this.tools.ReportScanResult),
            ]);

    /// <summary>
    /// Represents a single email scan result for display.
    /// </summary>
    internal sealed class ScanResult
    {
        /// <summary>
        /// Gets or sets the sender's domain.
        /// </summary>
        public required string Domain { get; set; }

        /// <summary>
        /// Gets or sets the email subject.
        /// </summary>
        public required string Subject { get; set; }

        /// <summary>
        /// Gets or sets the scan status (Flagged, Skipped, Clean).
        /// </summary>
        public required string Status { get; set; }

        /// <summary>
        /// Gets or sets the reason for the status.
        /// </summary>
        public string? Reason { get; set; }
    }

    private sealed class ScanBatchResult
    {
        public int Processed { get; set; }

        public int SkippedKnown { get; set; }

        public int SkippedPending { get; set; }

        public int Flagged { get; set; }

        public bool InboxWasEmpty { get; set; }
    }

    private sealed class RunStats
    {
        public int Iteration { get; set; }

        public int TotalProcessed { get; set; }

        public int TotalSkippedKnown { get; set; }

        public int TotalSkippedPending { get; set; }

        public int TotalFlagged { get; set; }
    }
}
