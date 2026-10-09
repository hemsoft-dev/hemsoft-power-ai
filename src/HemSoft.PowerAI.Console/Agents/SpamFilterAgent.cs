// <copyright file="SpamFilterAgent.cs" company="HemSoft">
// Copyright © 2025 HemSoft
// </copyright>

namespace HemSoft.PowerAI.Console.Agents;

using System.ClientModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

using HemSoft.PowerAI.Console.Configuration;
using HemSoft.PowerAI.Console.Extensions;
using HemSoft.PowerAI.Console.Models;
using HemSoft.PowerAI.Console.Services;
using HemSoft.PowerAI.Console.Tools;

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

using OpenAI;

using Spectre.Console;

/// <summary>
/// Agent that orchestrates the spam filtering workflow.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Agent requires external API calls")]
internal sealed partial class SpamFilterAgent : IDisposable
{
    private const string ModelId = "mistralai/devstral-2512:free";
    private const string OpenRouterBaseUrlEnvVar = "OPENROUTER_BASE_URL";
    private const string ApiKeyEnvVar = "OPENROUTER_API_KEY";

    private const string AgentInstructions = """
        You are a spam filtering assistant. Your job is to analyze emails and determine if they are spam.

        ## Your workflow for each batch of emails:

        1. First, get the list of known spam domains using GetKnownSpamDomains.
        2. Fetch a batch of emails from the inbox using GetInboxEmailsAsync.
        3. For each email, check the senderDomain from the results:

           a. If senderDomain is in the known SPAM list (from step 1):
              - Call MoveToJunkAsync(messageId) to move the email
              - Call ReportEmailEvaluation with verdict="Junked"
              - DO NOT read the email content

           b. If senderDomain is from a TRUSTED source (microsoft.com, amazon.com, paypal.com, google.com,
              apple.com, twitch.tv, github.com, linkedin.com, costco.com, etc.):
              - Call ReportEmailEvaluation with verdict="Legitimate" and reason="Trusted sender"
              - DO NOT read the email content - skip to next email

           c. ONLY for UNKNOWN domains (not spam AND not trusted):
              - Read the full email content using ReadEmailAsync
              - Analyze for spam indicators (suspicious links, urgent money requests, lottery notifications,
                pharmaceutical ads, adult content, phishing attempts, generic greetings)
              - If spam (confidence > 0.7), use RecordSpamCandidate and ReportEmailEvaluation (verdict="Candidate")
              - If legitimate, use ReportEmailEvaluation (verdict="Legitimate")

        4. IMPORTANT: Call ReportEmailEvaluation for EVERY email with:
           - messageId: the email's id from GetInboxEmailsAsync (REQUIRED - copy it exactly)
           - senderEmail: the sender's email address
           - subject: the email subject
           - verdict: one of "Legitimate", "Junked", or "Candidate"
           - reason: brief explanation

        5. After processing ALL emails, report stats using EXACTLY this format:
           BATCH_STATS: processed=N, junked=M, candidates=K, legitimate=L

        ## CRITICAL Performance Rules:
        - NEVER call ReadEmailAsync for known spam domains - just junk them immediately
        - NEVER call ReadEmailAsync for trusted/well-known domains - mark as legitimate immediately
        - ONLY call ReadEmailAsync for unknown/suspicious domains that need analysis
        - Process emails quickly - most emails should NOT require reading content
        """;

    private readonly SpamFilterSettings settings;
    private readonly SpamFilterTools tools;
    private readonly List<EmailEvaluation> currentBatchEvaluations = [];
    private int consecutiveEmptyBatches;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SpamFilterAgent"/> class.
    /// </summary>
    /// <param name="settings">The spam filter settings.</param>
    public SpamFilterAgent(SpamFilterSettings settings)
    {
        this.settings = settings;
        var storageService = new SpamStorageService(settings);
        var graphClientProvider = new DefaultGraphClientProvider();
        this.tools = new SpamFilterTools(storageService, graphClientProvider);
    }

    private enum BatchAction
    {
        Continue = 0,
        Stop = 1,
    }

    [GeneratedRegex(
        @"BATCH_STATS:\s*processed\s*=\s*(?<processed>\d+)\s*,\s*junked\s*=\s*(?<junked>\d+)\s*,\s*candidates\s*=\s*(?<candidates>\d+)",
        RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex BatchStatsRegex { get; }

    /// <summary>
    /// Runs the spam filtering workflow.
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
            this.consecutiveEmptyBatches = 0;
            var continueProcessing = true;

            while (continueProcessing && !cancellationToken.IsCancellationRequested)
            {
                stats.Iteration++;
                AnsiConsole.MarkupLine($"\n[blue]═══ Batch {stats.Iteration.ToInvariant()} ═══[/]");

                var batchResult = await this
                    .ProcessInboxBatchAsync(agent, this.settings.BatchSize, cancellationToken)
                    .ConfigureAwait(false);
                var action = await this.HandleBatchResultAsync(batchResult, stats, cancellationToken)
                    .ConfigureAwait(false);

                continueProcessing = action != BatchAction.Stop;
            }

            DisplayFinalSummary(stats);
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

    private static void DisplayFinalSummary(RunStats stats)
    {
        AnsiConsole.MarkupLine("\n[green]═══ Final Summary ═══[/]");
        AnsiConsole.MarkupLine($"[green]Total emails processed: {stats.TotalProcessed.ToInvariant()}[/]");
        AnsiConsole.MarkupLine($"[green]Total moved to junk: {stats.TotalMovedToJunk.ToInvariant()}[/]");
        AnsiConsole.MarkupLine($"[green]Total candidates flagged: {stats.TotalCandidates.ToInvariant()}[/]");
    }

    private static void DisplayEvaluationTable(List<EmailEvaluation> evaluations)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("[blue]#[/]").Width(3))
            .AddColumn(new TableColumn("[blue]Sender[/]").Width(28))
            .AddColumn(new TableColumn("[blue]Subject[/]").Width(33))
            .AddColumn(new TableColumn("[blue]Verdict[/]").Width(12))
            .AddColumn(new TableColumn("[blue]Reason[/]").Width(22));

        var rowNum = 1;
        foreach (var eval in evaluations)
        {
            var verdictColor = eval.Verdict.ToUpperInvariant() switch
            {
                "LEGITIMATE" => "green",
                "JUNKED" => "red",
                "CANDIDATE" => "yellow",
                "KNOWN SPAM" => "red",
                _ => "white",
            };

            var sender = Truncate(eval.Sender, 26);
            var subject = Truncate(eval.Subject, 31);
            var reason = Truncate(eval.Reason ?? "-", 20);

            _ = table.AddRow(
                $"[dim]{rowNum.ToInvariant()}[/]",
                Markup.Escape(sender),
                Markup.Escape(subject),
                $"[{verdictColor}]{Markup.Escape(eval.Verdict)}[/]",
                Markup.Escape(reason));
            rowNum++;
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine("[dim]Press any key to mark emails as spam (e.g., 1,3,5) or wait to continue...[/]");
    }

    private static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..(maxLength - 1)] + "…";

    private static string ExtractDomain(string email)
    {
        var atIndex = email.LastIndexOf('@');
        return atIndex >= 0 && atIndex < email.Length - 1
            ? email[(atIndex + 1)..].ToUpperInvariant()
            : email.ToUpperInvariant();
    }

    private static void AddEmailRowToTable(Table table, JsonElement email)
    {
        var subject = email.GetProperty("subject").GetString() ?? "(no subject)";
        var sender = email.GetProperty("senderEmail").GetString() ?? "unknown";
        var confidence = email.GetProperty("confidenceScore").GetDouble();
        var reason = email.GetProperty("spamReason").GetString() ?? "N/A";

        _ = table.AddRow(
            Markup.Escape(subject.Length > 40 ? subject[..37] + "..." : subject),
            Markup.Escape(sender),
            confidence.ToString("P0", CultureInfo.InvariantCulture),
            Markup.Escape(reason.Length > 30 ? reason[..27] + "..." : reason));
    }

    private static BatchResult ParseBatchResult(string responseText)
    {
        var result = new BatchResult();

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
            result.EmailsProcessed = int.Parse(statsMatch.Groups["processed"].Value, CultureInfo.InvariantCulture);
            result.MovedToJunk = int.Parse(statsMatch.Groups["junked"].Value, CultureInfo.InvariantCulture);
            result.CandidatesRecorded = int.Parse(statsMatch.Groups["candidates"].Value, CultureInfo.InvariantCulture);
            return result;
        }

        result.EmailsProcessed = ExtractCountFromPattern(
            upperResponse, @"(?<num1>\d+)\s*(?:EMAILS?\s*)?PROCESSED|PROCESSED\s*(?<num2>\d+)");

        result.MovedToJunk = ExtractCountFromPattern(
            upperResponse, @"(?<num1>\d+)\s*(?:EMAILS?\s*)?(?:MOVED\s*TO\s*JUNK|JUNKED)|(?:MOVED|JUNKED)\s*(?<num2>\d+)");

        result.CandidatesRecorded = ExtractCountFromPattern(
            upperResponse, @"(?<num1>\d+)\s*(?:SPAM\s*)?CANDIDATES?|FLAGGED\s*(?<num2>\d+)");

        return result;
    }

    private static int ExtractCountFromPattern(string text, string pattern)
    {
        var match = Regex.Match(
            text,
            pattern,
            RegexOptions.ExplicitCapture,
            TimeSpan.FromSeconds(1));
        if (match.Success)
        {
            var numStr = match.Groups["num1"].Success ? match.Groups["num1"].Value : match.Groups["num2"].Value;
            if (int.TryParse(numStr, CultureInfo.InvariantCulture, out var num))
            {
                return num;
            }
        }

        return 0;
    }

    private static void DisplayHeader(SpamFilterSettings settings)
    {
        AnsiConsole.Write(new FigletText("Spam Filter").Color(Color.Red));
        AnsiConsole.MarkupLine($"[dim]Model: {ModelId}[/]");
        AnsiConsole.MarkupLine(
            $"[dim]Batch Size: {settings.BatchSize.ToInvariant()} | " +
            $"Delay: {settings.DelayBetweenBatchesSeconds.ToInvariant()}s[/]\n");

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("[blue]Phase[/]")
            .AddColumn("[blue]Description[/]");

        _ = table.AddRow("1. Scan", "Process inbox emails in batches");
        _ = table.AddRow("2. Analyze", "AI checks known spam domains and analyzes content");
        _ = table.AddRow("3. Act", "Known spam → junk, unknown spam → candidates");
        _ = table.AddRow("4. Review", "Human approves domains to add to spam list");

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine("\n[dim]Press Ctrl+C to stop.[/]\n");
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

        var chatClient = CompositeDisposableChatClient.CreateWithFunctionInvocation(
            openAiClient,
            ModelId,
            enableLogging: true);

        return (chatClient, null);
    }

    private async Task<BatchResult> ProcessInboxBatchAsync(AIAgent agent, int batchSize, CancellationToken cancellationToken)
    {
        var result = new BatchResult();
        this.currentBatchEvaluations.Clear();

        try
        {
            var prompt = $"Process a batch of {batchSize.ToInvariant()} emails from the inbox. " +
                         "Follow your workflow to check each email against known spam domains, " +
                         "move known spam to junk, and analyze unknown senders for spam indicators. " +
                         "Call ReportEmailEvaluation for EVERY email you process. " +
                         "Report how many emails you processed, moved to junk, and flagged as candidates.";

            string? responseText = null;

            // Set up callback before starting
            this.tools.SetEvaluationCallback(this.currentBatchEvaluations.Add);

            try
            {
                AnsiConsole.MarkupLine("[blue]Processing inbox batch...[/]");

                // Add timeout - free models can be very slow
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromMinutes(3));

                var response = await agent.RunAsync(prompt, cancellationToken: timeoutCts.Token).ConfigureAwait(false);
                responseText = response.Text;
            }
            finally
            {
                this.tools.SetEvaluationCallback(callback: null);
            }

            // Display the evaluation table if we have any evaluations
            if (this.currentBatchEvaluations.Count > 0)
            {
                DisplayEvaluationTable(this.currentBatchEvaluations);
            }

            if (!string.IsNullOrEmpty(responseText))
            {
                result = ParseBatchResult(responseText);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            AnsiConsole.MarkupLine("[yellow]Batch timed out after 3 minutes. Continuing to next batch...[/]");
            result.EmailsProcessed = this.currentBatchEvaluations.Count;
        }
        catch (AggregateException ex) when (ex.InnerException is TaskCanceledException or OperationCanceledException)
        {
            AnsiConsole.MarkupLine("[yellow]API request timed out (model too slow). Continuing to next batch...[/]");
            result.EmailsProcessed = this.currentBatchEvaluations.Count;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            AnsiConsole.MarkupLine("[yellow]API network timeout. Continuing to next batch...[/]");
            result.EmailsProcessed = this.currentBatchEvaluations.Count;
        }
        catch (ClientResultException ex) when (ex.Status == 429)
        {
            AnsiConsole.MarkupLine("[yellow]Rate limited by API. Waiting 60 seconds before retry...[/]");
            await Task.Delay(TimeSpan.FromSeconds(60), cancellationToken).ConfigureAwait(false);
            result.EmailsProcessed = this.currentBatchEvaluations.Count;
        }
        catch (ClientResultException ex) when (ex.Status is 502 or 503 or 504)
        {
            AnsiConsole.MarkupLine($"[yellow]API temporarily unavailable ({ex.Status.ToInvariant()}). Waiting 30 seconds...[/]");
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);
            result.EmailsProcessed = this.currentBatchEvaluations.Count;
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
            name: "SpamFilterAgent",
            description: "An agent that analyzes emails and filters spam",
            tools:
            [
                AIFunctionFactory.Create(this.tools.GetKnownSpamDomains),
                AIFunctionFactory.Create(this.tools.IsKnownSpamDomain),
                AIFunctionFactory.Create(this.tools.GetInboxEmailsAsync),
                AIFunctionFactory.Create(this.tools.ReadEmailAsync),
                AIFunctionFactory.Create(this.tools.MoveToJunkAsync),
                AIFunctionFactory.Create(this.tools.RecordSpamCandidate),
                AIFunctionFactory.Create(this.tools.ReportEmailEvaluation),
                AIFunctionFactory.Create(this.tools.AddToSpamDomainList),
                AIFunctionFactory.Create(this.tools.GetPendingSpamCandidatesByDomain),
                AIFunctionFactory.Create(this.tools.ProcessApprovedSpamDomainAsync),
                AIFunctionFactory.Create(this.tools.ClearProcessedCandidates),
            ]);

    private async Task<BatchAction> HandleBatchResultAsync(BatchResult batchResult, RunStats stats, CancellationToken cancellationToken) =>
        (batchResult.EmailsProcessed, batchResult.InboxWasEmpty) switch
        {
            (0, true) => await this.HandleEmptyInboxAsync(cancellationToken).ConfigureAwait(false),
            (0, false) => await this.HandleParsingFailureAsync(cancellationToken).ConfigureAwait(false),
            _ => await this.HandleSuccessfulBatchAsync(batchResult, stats, cancellationToken).ConfigureAwait(false),
        };

    private async Task<BatchAction> HandleEmptyInboxAsync(CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("[green]No more emails to process in inbox.[/]");

        var hasMoreCandidates = await this.ProcessHumanReviewAsync(cancellationToken).ConfigureAwait(false);

        if (!hasMoreCandidates)
        {
            AnsiConsole.MarkupLine("[green]✓ All done! No spam candidates pending review.[/]");
            return BatchAction.Stop;
        }

        this.consecutiveEmptyBatches = 0;
        return BatchAction.Continue;
    }

    private async Task<BatchAction> HandleParsingFailureAsync(CancellationToken cancellationToken)
    {
        this.consecutiveEmptyBatches++;
        AnsiConsole.MarkupLine(
            "[yellow]Could not parse batch results " +
            $"(attempt {this.consecutiveEmptyBatches.ToInvariant()}/3). Continuing...[/]");

        if (this.consecutiveEmptyBatches >= 3)
        {
            AnsiConsole.MarkupLine("[yellow]Multiple parsing failures. Triggering human review...[/]");

            var hasMoreCandidates = await this.ProcessHumanReviewAsync(cancellationToken).ConfigureAwait(false);

            if (!hasMoreCandidates)
            {
                AnsiConsole.MarkupLine("[green]✓ All done! No spam candidates pending review.[/]");
                return BatchAction.Stop;
            }

            this.consecutiveEmptyBatches = 0;
        }

        await this.WaitBeforeNextBatchAsync(cancellationToken).ConfigureAwait(false);
        return BatchAction.Continue;
    }

    private async Task<BatchAction> HandleSuccessfulBatchAsync(BatchResult batchResult, RunStats stats, CancellationToken cancellationToken)
    {
        this.consecutiveEmptyBatches = 0;
        stats.TotalProcessed += batchResult.EmailsProcessed;
        stats.TotalMovedToJunk += batchResult.MovedToJunk;
        stats.TotalCandidates += batchResult.CandidatesRecorded;

        AnsiConsole.MarkupLine(
            $"[dim]Running totals: {stats.TotalProcessed.ToInvariant()} processed, " +
            $"{stats.TotalMovedToJunk.ToInvariant()} junked, {stats.TotalCandidates.ToInvariant()} candidates[/]");

        await this.WaitBeforeNextBatchAsync(cancellationToken).ConfigureAwait(false);
        return BatchAction.Continue;
    }

    private async Task WaitBeforeNextBatchAsync(CancellationToken cancellationToken)
    {
        var delayMs = this.settings.DelayBetweenBatchesSeconds * 1000;
        var elapsed = 0;
        const int checkIntervalMs = 100;

        while (elapsed < delayMs && !cancellationToken.IsCancellationRequested)
        {
            if (System.Console.KeyAvailable)
            {
                // User pressed a key - prompt for spam selections
                _ = System.Console.ReadKey(intercept: true); // Consume the key that triggered this
                await this.ProcessUserSpamSelectionsAsync().ConfigureAwait(false);
                break;
            }

            await Task.Delay(checkIntervalMs, cancellationToken).ConfigureAwait(false);
            elapsed += checkIntervalMs;
        }
    }

    private async Task ProcessUserSpamSelectionsAsync()
    {
        if (this.currentBatchEvaluations.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No emails in current batch to mark.[/]");
            return;
        }

        var input = await AnsiConsole.AskAsync<string>(
            "[yellow]Enter email numbers to mark as spam (e.g., 1,3,5) or press Enter to skip:[/]")
            .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(input))
        {
            return;
        }

        var selections = input
            .Split([',', ' '], StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s.Trim(), CultureInfo.InvariantCulture, out var n) ? n : 0)
            .Where(n => n > 0 && n <= this.currentBatchEvaluations.Count)
            .Distinct()
            .ToList();

        if (selections.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No valid selections.[/]");
            return;
        }

        var processed = 0;
        foreach (var num in selections)
        {
            var result = await this.ProcessSingleSpamSelectionAsync(num).ConfigureAwait(false);
            if (result)
            {
                processed++;
            }
        }

        AnsiConsole.MarkupLine($"[green]Processed {processed.ToInvariant()} email(s).[/]");
    }

    private async Task<bool> ProcessSingleSpamSelectionAsync(int num)
    {
        var eval = this.currentBatchEvaluations[num - 1];
        var domain = ExtractDomain(eval.Sender);

        // Check if already junked
        if (eval.Verdict.Equals("Junked", StringComparison.OrdinalIgnoreCase))
        {
            AnsiConsole.MarkupLine($"[yellow]#{num.ToInvariant()}[/] [dim]{domain}[/] - already moved to junk");
            _ = this.tools.AddToSpamDomainList(domain, "Manually marked by user");
            return true;
        }

        // Check if message ID is valid
        if (string.IsNullOrEmpty(eval.MessageId))
        {
            AnsiConsole.MarkupLine($"[red]#{num.ToInvariant()}[/] [dim]{domain}[/] - no message ID captured (LLM didn't provide it)");
            _ = this.tools.AddToSpamDomainList(domain, "Manually marked by user");
            return false;
        }

        // Add domain to spam list and move email to junk
        _ = this.tools.AddToSpamDomainList(domain, "Manually marked by user");
        var result = await this.tools.MoveToJunkAsync(eval.MessageId).ConfigureAwait(false);

        if (result.StartsWith("Successfully", StringComparison.Ordinal))
        {
            AnsiConsole.MarkupLine($"[green]#{num.ToInvariant()}[/] [dim]{domain}[/] → added to spam list, email moved to junk");
            return true;
        }

        // Still add domain even if move failed (email might already be gone)
        AnsiConsole.MarkupLine(
            $"[yellow]#{num.ToInvariant()}[/] [dim]{domain}[/] → " +
            $"added to spam list (move failed: {Truncate(result, 50)})");
        return true;
    }

    private async Task<bool> ProcessHumanReviewAsync(CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        var candidatesJson = this.tools.GetPendingSpamCandidatesByDomain();
        var candidates = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(candidatesJson);

        if (candidates is null || candidates.Count == 0)
        {
            return false;
        }

        AnsiConsole.MarkupLine("\n[yellow]═══ Human Review Required ═══[/]");
        AnsiConsole.MarkupLine($"[yellow]Found {candidates.Count.ToInvariant()} domains with spam candidates.[/]\n");

        foreach (var (domain, emailsElement) in candidates)
        {
            await this.ProcessDomainCandidatesAsync(domain, emailsElement).ConfigureAwait(false);
        }

        _ = this.tools.ClearProcessedCandidates();

        return false;
    }

    private async Task ProcessDomainCandidatesAsync(string domain, JsonElement emailsElement)
    {
        List<JsonElement> emails;
        using (var enumerator = emailsElement.EnumerateArray())
        {
            emails = [.. enumerator];
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("[blue]Subject[/]")
            .AddColumn("[blue]Sender[/]")
            .AddColumn("[blue]Confidence[/]")
            .AddColumn("[blue]Reason[/]");

        foreach (var email in emails)
        {
            AddEmailRowToTable(table, email);
        }

        AnsiConsole.Write(new Panel(table)
            .Header($"[yellow]Domain: {domain} ({emails.Count.ToInvariant()} emails)[/]")
            .Border(BoxBorder.Rounded));

        var confirmMessage = $"Add [yellow]{domain}[/] to spam list and move {emails.Count.ToInvariant()} email(s) to junk?";
        var approve = await AnsiConsole.ConfirmAsync(confirmMessage).ConfigureAwait(false);

        if (approve)
        {
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync($"Processing {domain}...", async _ =>
                {
                    var result = await this.tools.ProcessApprovedSpamDomainAsync(domain).ConfigureAwait(false);
                    AnsiConsole.MarkupLine($"[green]{Markup.Escape(result)}[/]");
                })
                .ConfigureAwait(false);
        }
        else
        {
            AnsiConsole.MarkupLine($"[dim]Skipped {domain} - emails remain in inbox.[/]");
        }
    }

    private sealed class BatchResult
    {
        public int EmailsProcessed { get; set; }

        public int MovedToJunk { get; set; }

        public int CandidatesRecorded { get; set; }

        public bool InboxWasEmpty { get; set; }
    }

    private sealed class RunStats
    {
        public int Iteration { get; set; }

        public int TotalProcessed { get; set; }

        public int TotalMovedToJunk { get; set; }

        public int TotalCandidates { get; set; }
    }
}
