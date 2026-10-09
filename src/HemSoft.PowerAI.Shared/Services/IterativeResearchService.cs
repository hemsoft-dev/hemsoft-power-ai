// <copyright file="IterativeResearchService.cs" company="HemSoft">
// Copyright © 2025 HemSoft
// </copyright>

namespace HemSoft.PowerAI.Common.Services;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using HemSoft.PowerAI.Common.Agents;
using HemSoft.PowerAI.Common.Models;

using Microsoft.Agents.AI;

/// <summary>
/// Orchestrates iterative research by coordinating ResearchAgent and EvaluatorAgent.
/// Uses task decomposition: breaks queries into sub-tasks, researches each sequentially,
/// then synthesizes results. Continues until quality threshold is met or max iterations reached.
/// </summary>
/// <param name="maxIterationsPerSubTask">Maximum refinement iterations per sub-task.</param>
/// <param name="qualityThreshold">Minimum quality score to accept (1-10).</param>
/// <param name="onProgress">Optional callback for progress updates.</param>
/// <param name="timeProvider">Time provider for timestamps.</param>
[ExcludeFromCodeCoverage(Justification = "Service requires OpenRouter API")]
public sealed partial class IterativeResearchService(
    int maxIterationsPerSubTask,
    int qualityThreshold,
    Action<string>? onProgress,
    TimeProvider timeProvider)
{
    private const string SectionSeparator =
        "═══════════════════════════════════════════════════════════════════";

    private const string SubSectionSeparator =
        "───────────────────────────────────────────────────────────────────";

    private static readonly CompositeFormat PlanningPromptFormat = CompositeFormat.Parse("""
        ## Research Query to Decompose
        {0}

        ---
        Please analyze this query and decompose it into focused sub-tasks.
        Consider what distinct aspects need investigation and their logical order.
        Respond with your JSON research plan.
        """);

    private static readonly CompositeFormat SubTaskEvaluationPromptFormat = CompositeFormat.Parse("""
        ## Original Research Question
        {0}

        ## Current Sub-Task
        Query: {1}
        Expected Outcome: {2}

        ## Research Findings for This Sub-Task
        {3}

        ---
        Please evaluate these findings against the sub-task requirements.
        Respond with your JSON assessment.
        """);

    private static readonly CompositeFormat SynthesisPromptFormat = CompositeFormat.Parse("""
        ## Original Question
        {0}

        ## Research Findings from {1} Sub-Tasks
        {2}

        ---
        ## Your Task: Create a COMPREHENSIVE Report

        Synthesize ALL findings into an EXHAUSTIVE, publication-quality report. This is NOT
        a summary - it should be a thorough reference document that leaves no question unanswered.

        ### Requirements:
        1. **Length**: The report MUST be DETAILED and LONG (minimum 5000 words for complex topics).
           Include ALL relevant information from the sub-tasks. Do NOT abbreviate or summarize
           excessively. Aim for MAXIMUM depth and coverage. The final report should be LONGER
           than the combined input findings, not shorter.

        2. **Structure**: Use proper markdown with multiple levels of headings:
           - Executive Summary (brief overview - 200-300 words)
           - Table of Contents
           - Detailed sections for each major topic area (each 500+ words)
           - Code examples where relevant (with full context, not abbreviated)
           - Tables for comparisons or structured data
           - Links to sources and references
           - Appendices for supplementary information

        3. **Content Quality**:
           - Include specific version numbers, dates, and facts
           - Provide step-by-step procedures where applicable
           - Include architecture diagrams described in text if relevant
           - Add practical examples and use cases with complete code
           - Note any limitations, caveats, or open questions
           - Include troubleshooting sections where relevant

        4. **CRITICAL - No Loss**: You MUST preserve ALL unique details from the input findings.
           Every fact, every code snippet, every reference from the sub-tasks must appear in
           the final report. Organize and deduplicate, but NEVER omit information.

        5. **Actionable**: The report should enable someone to take immediate action on the topic.

        IMPORTANT: Output your complete report. Do not truncate or abbreviate. Continue until
        you have covered ALL information from the input findings.

        First provide your JSON assessment, then provide the COMPLETE synthesized report in markdown.
        """);

    private static readonly CompositeFormat RefinementPromptFormat = CompositeFormat.Parse("""
        ## REFINEMENT REQUIRED - Previous Research Was Insufficient

        ### Original Sub-Task Query
        {0}

        ### Previous Research Findings (INSUFFICIENT)
        {1}

        ### Evaluator Feedback - WHAT WAS WRONG
        **Score:** {2}/10 (needs {3}+ to pass)
        **Reasoning:** {4}

        ### Specific Gaps to Address
        {5}

        ### Refined Query for This Iteration
        {6}

        ---
        ## YOUR TASK
        You must address the specific gaps and issues identified above.
        Focus on providing the MISSING information that the evaluator noted.
        Be MORE thorough and detailed than the previous attempt.
        Include specific examples, version numbers, code samples, and authoritative sources.

        DO NOT simply repeat the previous findings. ADD to them with the missing details.
        """);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="IterativeResearchService"/> class
    /// with default settings.
    /// </summary>
    public IterativeResearchService()
        : this(maxIterationsPerSubTask: 5, qualityThreshold: 5, onProgress: null, timeProvider: TimeProvider.System)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="IterativeResearchService"/> class
    /// with a progress callback.
    /// </summary>
    /// <param name="progressCallback">Callback for progress updates.</param>
    public IterativeResearchService(Action<string> progressCallback)
        : this(maxIterationsPerSubTask: 5, qualityThreshold: 5, onProgress: progressCallback, timeProvider: TimeProvider.System)
    {
    }

    [GeneratedRegex(
        @"```(?:json)?\s*(?<json>[\s\S]*?)\s*```",
        RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture | RegexOptions.NonBacktracking,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex JsonBlockRegex { get; }

    /// <summary>
    /// Executes iterative research on the given query using task decomposition.
    /// </summary>
    /// <param name="query">The research query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The complete research state with all iterations and final synthesis.</returns>
    public async Task<ResearchIterationState> ResearchAsync(
        string query,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var state = new ResearchIterationState(query, timeProvider);
        var researchAgent = ResearchAgent.Create();

        this.LogResearchSessionStart(query);

        // Phase 1: Plan - decompose the query into sub-tasks
        var plan = await this.ExecutePlanningPhaseAsync(query, cancellationToken).ConfigureAwait(false);

        if (plan is null || plan.SubTasks.Length == 0)
        {
            this.ReportProgress("⚠ Decomposition failed, falling back to simple research");
            return await FallbackSimpleResearchAsync(state, query, researchAgent, cancellationToken)
                .ConfigureAwait(false);
        }

        this.LogSubTaskList(plan);

        // Phase 2: Execute each sub-task sequentially
        await this.ExecuteResearchPhaseAsync(state, plan, researchAgent, cancellationToken)
            .ConfigureAwait(false);

        // Phase 3: Synthesize all findings
        await this.ExecuteSynthesisPhaseAsync(state, plan, cancellationToken).ConfigureAwait(false);

        this.LogResearchSessionComplete(state, plan);

        return state;
    }

    private static string FormatDataSizeWithBytes(long bytes) =>
        string.Create(CultureInfo.InvariantCulture, $"{FormatDataSize(bytes)} ({bytes:N0} bytes)");

    private static string FormatDataSize(long bytes) =>
        bytes switch
        {
            < 1024 => string.Create(CultureInfo.InvariantCulture, $"{bytes} B"),
            < 1024 * 1024 => string.Create(CultureInfo.InvariantCulture, $"{bytes / 1024.0:F1} KB"),
            _ => string.Create(CultureInfo.InvariantCulture, $"{bytes / (1024.0 * 1024.0):F2} MB"),
        };

    private static async Task<string> RunResearchAsync(
        AIAgent agent,
        string query,
        CancellationToken cancellationToken)
    {
        var response = await agent.RunAsync(query, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return response.Text ?? "No findings returned.";
    }

    private static async Task<ResearchEvaluation> EvaluateSubTaskAsync(
        string originalQuery,
        ResearchSubTask subTask,
        string findings,
        AIAgent evaluatorAgent,
        CancellationToken cancellationToken)
    {
        var prompt = string.Format(
            CultureInfo.InvariantCulture,
            SubTaskEvaluationPromptFormat,
            originalQuery,
            subTask.Query,
            subTask.ExpectedOutcome,
            findings);

        var response = await evaluatorAgent.RunAsync(prompt, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return ParseEvaluation(response.Text ?? string.Empty);
    }

    private static async Task<ResearchIterationState> FallbackSimpleResearchAsync(
        ResearchIterationState state,
        string query,
        AIAgent researchAgent,
        CancellationToken cancellationToken)
    {
        var findings = await RunResearchAsync(researchAgent, query, cancellationToken)
            .ConfigureAwait(false);

        var evaluation = ResearchEvaluation.CreateDefault();
        state.AddIteration(query, findings, evaluation);
        state.FinalSynthesis = findings;
        state.IsComplete = true;

        return state;
    }

    /// <summary>
    /// Parses the evaluation JSON from the agent response.
    /// Uses multiple strategies to extract JSON reliably without regex-dependent truncation.
    /// </summary>
    /// <param name="response">The full response text from the evaluator agent.</param>
    /// <returns>The parsed evaluation, or a default if parsing fails.</returns>
    private static ResearchEvaluation ParseEvaluation(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return ResearchEvaluation.CreateDefault();
        }

        try
        {
            // Try multiple extraction strategies in order of reliability
            return TryParseFromFencedBlock(response)
                ?? TryParseFromBraceDepth(response)
                ?? TryParseAsFullJson(response)
                ?? ResearchEvaluation.CreateDefault();
        }
        catch (JsonException)
        {
            return ResearchEvaluation.CreateDefault();
        }
    }

    /// <summary>
    /// Tries to parse evaluation from a fenced code block (```json ... ```).
    /// </summary>
    /// <param name="response">The response text to parse.</param>
    /// <returns>The parsed evaluation, or null if parsing fails.</returns>
    private static ResearchEvaluation? TryParseFromFencedBlock(string response)
    {
        var jsonMatch = JsonBlockRegex.Match(response);
        if (!jsonMatch.Success)
        {
            return null;
        }

        var json = jsonMatch.Groups["json"].Value.Trim();
        return string.IsNullOrEmpty(json)
            ? null
            : JsonSerializer.Deserialize<ResearchEvaluation>(json, JsonOptions);
    }

    /// <summary>
    /// Tries to parse evaluation by finding JSON object boundaries using brace depth.
    /// </summary>
    /// <param name="response">The response text to parse.</param>
    /// <returns>The parsed evaluation, or null if parsing fails.</returns>
    private static ResearchEvaluation? TryParseFromBraceDepth(string response)
    {
        var jsonEndIndex = FindFirstJsonObjectEnd(response);
        if (jsonEndIndex <= 0)
        {
            return null;
        }

        var firstBrace = response.IndexOf('{', StringComparison.Ordinal);
        if (firstBrace < 0 || jsonEndIndex <= firstBrace)
        {
            return null;
        }

        var json = response[firstBrace..(jsonEndIndex + 1)];
        return JsonSerializer.Deserialize<ResearchEvaluation>(json, JsonOptions);
    }

    /// <summary>
    /// Tries to parse the entire response as JSON.
    /// </summary>
    /// <param name="response">The response text to parse.</param>
    /// <returns>The parsed evaluation, or null if parsing fails.</returns>
    private static ResearchEvaluation? TryParseAsFullJson(string response)
    {
        var trimmed = response.Trim();
        return trimmed.StartsWith('{') && trimmed.EndsWith('}')
            ? JsonSerializer.Deserialize<ResearchEvaluation>(response, JsonOptions)
            : null;
    }

    /// <summary>
    /// Extracts the markdown content that appears after the JSON assessment block.
    /// Uses robust parsing to preserve ALL markdown content without truncation.
    /// </summary>
    /// <param name="text">The full response text containing JSON followed by markdown.</param>
    /// <param name="fallback">Fallback content to return if extraction fails.</param>
    /// <returns>The markdown content after the JSON block, or the full text if no JSON found.</returns>
    private static string ExtractMarkdownAfterJson(string text, string fallback)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return fallback;
        }

        // Try extraction strategies in order of reliability
        return TryExtractAfterFencedBlock(text)
            ?? TryExtractAfterJsonObject(text)
            ?? GetMarkdownOrFullText(text);
    }

    /// <summary>
    /// Tries to extract markdown content after a fenced JSON block.
    /// </summary>
    /// <param name="text">The text to search.</param>
    /// <returns>The extracted markdown, or null if not found.</returns>
    private static string? TryExtractAfterFencedBlock(string text)
    {
        var jsonBlockMatch = JsonBlockRegex.Match(text);
        if (!jsonBlockMatch.Success)
        {
            return null;
        }

        var afterBlock = text[(jsonBlockMatch.Index + jsonBlockMatch.Length)..].Trim();
        return IsValidMarkdownContent(afterBlock) ? afterBlock : null;
    }

    /// <summary>
    /// Tries to extract markdown content after a JSON object found by brace depth.
    /// </summary>
    /// <param name="text">The text to search.</param>
    /// <returns>The extracted markdown, or null if not found.</returns>
    private static string? TryExtractAfterJsonObject(string text)
    {
        var jsonEndIndex = FindFirstJsonObjectEnd(text);
        if (jsonEndIndex <= 0 || jsonEndIndex >= text.Length - 1)
        {
            return null;
        }

        var afterJson = text[(jsonEndIndex + 1)..].Trim();
        return IsValidMarkdownContent(afterJson) ? afterJson : null;
    }

    /// <summary>
    /// Returns the original text. Always returns full content to avoid truncation.
    /// </summary>
    /// <param name="text">The text to return.</param>
    /// <returns>The original text.</returns>
    private static string GetMarkdownOrFullText(string text) => text;

    /// <summary>
    /// Checks if the text is valid markdown content worth extracting.
    /// </summary>
    /// <param name="text">The text to check.</param>
    /// <returns>True if the text is valid markdown content.</returns>
    private static bool IsValidMarkdownContent(string text) =>
        text.Length > 50 && ContainsMarkdownContent(text);

    /// <summary>
    /// Checks if the text contains typical markdown content indicators.
    /// Used to verify extraction got actual markdown, not truncated content.
    /// </summary>
    /// <param name="text">The text to check.</param>
    /// <returns>True if the text appears to contain markdown content.</returns>
    private static bool ContainsMarkdownContent(string text) =>
        text.Contains('#', StringComparison.Ordinal) ||
        text.Contains("**", StringComparison.Ordinal) ||
        text.Contains("```", StringComparison.Ordinal) ||
        text.Contains("- ", StringComparison.Ordinal) ||
        text.Contains("1. ", StringComparison.Ordinal);

    /// <summary>
    /// Truncates text to a maximum length for display purposes.
    /// </summary>
    /// <param name="text">The text to truncate.</param>
    /// <param name="maxLength">Maximum length before truncation.</param>
    /// <returns>The truncated text with ellipsis if needed.</returns>
    private static string TruncateForDisplay(string text, int maxLength) =>
        text.Length > maxLength ? text[..maxLength] + "..." : text;

    /// <summary>
    /// Finds the index of the closing brace of the first JSON object in the text.
    /// </summary>
    /// <param name="text">The text to search.</param>
    /// <returns>
    /// The index of the closing brace, or -1 if no complete JSON object is found.
    /// </returns>
    private static int FindFirstJsonObjectEnd(string text)
    {
        var firstBrace = text.IndexOf('{', StringComparison.Ordinal);
        if (firstBrace < 0)
        {
            return -1;
        }

        var depth = 0;
        var inString = false;
        var escapeNext = false;

        for (var i = firstBrace; i < text.Length; i++)
        {
            (inString, escapeNext, var isEnd) = ProcessJsonChar(text[i], inString, escapeNext, ref depth);

            if (isEnd)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Processes a single character while parsing JSON to track depth and string state.
    /// </summary>
    /// <param name="c">The character to process.</param>
    /// <param name="inString">Whether we're currently inside a JSON string.</param>
    /// <param name="escapeNext">Whether the next character should be escaped.</param>
    /// <param name="depth">The current brace nesting depth (modified by reference).</param>
    /// <returns>Updated state: InString, EscapeNext, and whether this is the end of the object.</returns>
    private static (bool InString, bool EscapeNext, bool IsObjectEnd) ProcessJsonChar(
        char c,
        bool inString,
        bool escapeNext,
        ref int depth)
    {
        if (escapeNext)
        {
            return (inString, false, false);
        }

        if (c == '\\' && inString)
        {
            return (inString, true, false);
        }

        if (c == '"')
        {
            return (!inString, false, false);
        }

        if (inString)
        {
            return (inString, false, false);
        }

        // Not in string, check for braces
        if (c == '{')
        {
            depth++;
        }
        else if (c == '}')
        {
            depth--;
            if (depth == 0)
            {
                return (false, false, true);
            }
        }

        return (inString, false, false);
    }

    private void LogResearchSessionStart(string query)
    {
        this.ReportProgress(SectionSeparator);
        this.ReportProgress("DEEP RESEARCH SESSION STARTED");
        this.ReportProgress(SectionSeparator);
        this.ReportProgress(
            "Models: Research={0}, Evaluator={1}",
            ResearchAgent.DefaultModelId,
            EvaluatorAgent.DefaultModelId);
        this.ReportProgress("Query: \"{0}\"", TruncateForDisplay(query, 100));
    }

    private Task<ResearchPlan?> ExecutePlanningPhaseAsync(
        string query,
        CancellationToken cancellationToken)
    {
        this.ReportProgress(SubSectionSeparator);
        this.ReportProgress("PHASE 1: PLANNING - Decomposing query into sub-tasks...");
        return this.DecomposeQueryAsync(query, cancellationToken);
    }

    private void LogSubTaskList(ResearchPlan plan)
    {
        this.ReportProgress("✓ Created {0} sub-tasks:", plan.SubTasks.Length);
        for (var i = 0; i < plan.SubTasks.Length; i++)
        {
            var st = plan.SubTasks[i];
            this.ReportProgress("  [{0}] {1}", i + 1, TruncateForDisplay(st.Query, 80));
        }
    }

    private async Task ExecuteResearchPhaseAsync(
        ResearchIterationState state,
        ResearchPlan plan,
        AIAgent researchAgent,
        CancellationToken cancellationToken)
    {
        this.ReportProgress(SubSectionSeparator);
        this.ReportProgress("PHASE 2: RESEARCH - Executing {0} sub-tasks...", plan.SubTasks.Length);
        var evaluatorAgent = EvaluatorAgent.Create();
        var subTaskNum = 0;

        while (!plan.AllSubTasksComplete && !cancellationToken.IsCancellationRequested)
        {
            var nextSubTask = plan.NextReadySubTask;
            if (nextSubTask is null)
            {
                this.ReportProgress("⚠ No more sub-tasks ready to execute");
                break;
            }

            subTaskNum++;
            await this.ExecuteSubTaskAsync(
                state,
                plan,
                nextSubTask,
                subTaskNum,
                researchAgent,
                evaluatorAgent,
                cancellationToken).ConfigureAwait(false);

            this.LogCumulativeFindingsSize(plan);
        }
    }

    private void LogCumulativeFindingsSize(ResearchPlan plan)
    {
        var allFindings = plan.AllFindings;
        var totalFindingsSize = Encoding.UTF8.GetByteCount(allFindings);
        this.ReportProgress(
            "  📊 Cumulative findings: {0}",
            FormatDataSizeWithBytes(totalFindingsSize));
    }

    private async Task ExecuteSynthesisPhaseAsync(
        ResearchIterationState state,
        ResearchPlan plan,
        CancellationToken cancellationToken)
    {
        this.ReportProgress(SubSectionSeparator);
        this.ReportProgress("PHASE 3: SYNTHESIS - Combining findings from {0} sub-tasks...", plan.CompletedCount);

        var preSynthesisFindings = plan.AllFindings;
        var preSynthesisSize = Encoding.UTF8.GetByteCount(preSynthesisFindings);
        this.ReportProgress("  Pre-synthesis findings: {0}", FormatDataSizeWithBytes(preSynthesisSize));

        state.FinalSynthesis = await this.SynthesizeFindingsWithLoggingAsync(plan, cancellationToken)
            .ConfigureAwait(false);
        state.IsComplete = true;
    }

    private void LogResearchSessionComplete(ResearchIterationState state, ResearchPlan plan)
    {
        var finalSize = Encoding.UTF8.GetByteCount(state.FinalSynthesis ?? string.Empty);
        this.ReportProgress(SectionSeparator);
        this.ReportProgress("RESEARCH COMPLETE");
        this.ReportProgress("  Sub-tasks completed: {0}/{1}", plan.CompletedCount, plan.SubTasks.Length);
        this.ReportProgress("  Total iterations: {0}", state.CurrentIteration);
        this.ReportProgress("  Final report size: {0}", FormatDataSizeWithBytes(finalSize));
        this.ReportProgress(SectionSeparator);
    }

    private async Task<string> SynthesizeFindingsWithLoggingAsync(
        ResearchPlan plan,
        CancellationToken cancellationToken)
    {
        if (plan.CompletedCount == 0)
        {
            this.ReportProgress("  ⚠ No sub-tasks completed, nothing to synthesize");
            return "No sub-tasks were completed.";
        }

        if (plan.CompletedCount == 1)
        {
            var singleFindings = plan.SubTasks.First(st => st.IsComplete).Findings ?? "No findings.";
            var singleSize = Encoding.UTF8.GetByteCount(singleFindings);
            this.ReportProgress("  Single sub-task, using findings directly ({0})", FormatDataSize(singleSize));
            return singleFindings;
        }

        var synthesizer = EvaluatorAgent.CreateSynthesizer();
        var allFindings = plan.AllFindings;
        var prompt = string.Format(
            CultureInfo.InvariantCulture,
            SynthesisPromptFormat,
            plan.OriginalQuery,
            plan.CompletedCount,
            allFindings);

        var promptSize = Encoding.UTF8.GetByteCount(prompt);
        this.ReportProgress("  Synthesis prompt size: {0}", FormatDataSizeWithBytes(promptSize));
        this.ReportProgress("  Calling synthesizer agent...");

        var response = await synthesizer.RunAsync(prompt, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var rawResponse = response.Text ?? string.Empty;
        var rawSize = Encoding.UTF8.GetByteCount(rawResponse);
        this.ReportProgress("  Raw synthesizer response: {0}", FormatDataSizeWithBytes(rawSize));

        // Extract the markdown content after the JSON block
        var extracted = ExtractMarkdownAfterJson(rawResponse, allFindings);
        var extractedSize = Encoding.UTF8.GetByteCount(extracted);
        this.ReportProgress("  Extracted markdown: {0}", FormatDataSizeWithBytes(extractedSize));

        // DIAGNOSTIC: Log if there's a significant size reduction
        this.LogContentLossWarningIfNeeded(rawResponse, rawSize, extracted, extractedSize);

        return extracted;
    }

    private void LogContentLossWarningIfNeeded(
        string rawResponse,
        long rawSize,
        string extracted,
        long extractedSize)
    {
        if (extractedSize >= rawSize * 0.5 || rawSize <= 1000)
        {
            return;
        }

        this.ReportProgress("  ⚠ WARNING: Significant content loss during extraction!");
        this.ReportProgress("    Raw first 200 chars: {0}", TruncateForDisplay(rawResponse, 200));
        this.ReportProgress("    Extracted first 200 chars: {0}", TruncateForDisplay(extracted, 200));
    }

    private async Task<ResearchPlan?> DecomposeQueryAsync(
        string query,
        CancellationToken cancellationToken)
    {
        this.ReportProgress("Planning: Decomposing query into sub-tasks...");

        var planner = EvaluatorAgent.CreatePlanner();
        var prompt = string.Format(CultureInfo.InvariantCulture, PlanningPromptFormat, query);

        var response = await planner.RunAsync(prompt, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var evaluation = ParseEvaluation(response.Text ?? string.Empty);

        return !evaluation.HasResearchPlan || evaluation.SubTasks is not { Length: > 0 }
            ? null
            : new ResearchPlan(
                OriginalQuery: query,
                SubTasks: evaluation.SubTasks.Value,
                Rationale: evaluation.Reasoning);
    }

    private async Task ExecuteSubTaskAsync(
        ResearchIterationState state,
        ResearchPlan plan,
        ResearchSubTask subTask,
        int subTaskNum,
        AIAgent researchAgent,
        AIAgent evaluatorAgent,
        CancellationToken cancellationToken)
    {
        var iterationContext = new SubTaskIterationContext(subTask.Query);
        var truncatedQuery = TruncateForDisplay(subTask.Query, 60);

        this.ReportProgress("  ┌─ Sub-task {0}/{1}: {2}", subTaskNum, plan.SubTasks.Length, truncatedQuery);

        while (iterationContext.Count < maxIterationsPerSubTask && !cancellationToken.IsCancellationRequested)
        {
            iterationContext.IncrementCount();
            this.ReportProgress("  │  Iteration {0}/{1}", iterationContext.Count, maxIterationsPerSubTask);

            var findings = await this.ExecuteResearchIterationAsync(
                researchAgent, subTask, iterationContext, cancellationToken).ConfigureAwait(false);

            var findingsSize = Encoding.UTF8.GetByteCount(findings);
            this.ReportProgress("  │  → Findings received: {0}", FormatDataSizeWithBytes(findingsSize));

            var evaluation = await EvaluateSubTaskAsync(
                plan.OriginalQuery, subTask, findings, evaluatorAgent, cancellationToken)
                .ConfigureAwait(false);

            state.AddIteration(iterationContext.CurrentQuery, findings, evaluation);
            this.LogIterationEvaluation(evaluation);

            if (this.TryCompleteSubTask(subTask, findings, evaluation))
            {
                this.LogSubTaskComplete(subTaskNum, evaluation, findingsSize);
                return;
            }

            if (!this.TryRefineQuery(iterationContext, evaluation, subTaskNum))
            {
                this.AcceptCurrentFindings(subTask, findings, evaluation, subTaskNum);
                this.ReportProgress(
                    "  └─ Sub-task {0} ACCEPTED (no refinement, score: {1}/10)",
                    subTaskNum,
                    evaluation.QualityScore);
                return;
            }

            this.ReportProgress("  │  → Refining query for next iteration...");
        }

        this.ReportProgress(
            "  └─ Sub-task {0}: Max iterations reached, accepting current findings",
            subTaskNum);
        subTask.IsComplete = true;
    }

    private void LogIterationEvaluation(ResearchEvaluation evaluation)
    {
        var isPassing = evaluation.IsSatisfactory && evaluation.QualityScore >= qualityThreshold;
        var passOrFail = isPassing ? "✓ PASS" : "✗ FAIL";
        this.ReportProgress(
            "  │  → Evaluation: Score {0}/10 (threshold: {1}) {2}",
            evaluation.QualityScore,
            qualityThreshold,
            passOrFail);

        if (!isPassing && evaluation.Gaps.Length > 0)
        {
            var gapsSummary = string.Join("; ", evaluation.Gaps.Take(2));
            this.ReportProgress("  │  → Gaps: {0}", gapsSummary);
        }
    }

    private void LogSubTaskComplete(int subTaskNum, ResearchEvaluation evaluation, long findingsSize) =>
        this.ReportProgress(
            "  └─ Sub-task {0} COMPLETE (score: {1}/10, size: {2})",
            subTaskNum,
            evaluation.QualityScore,
            FormatDataSize(findingsSize));

    private Task<string> ExecuteResearchIterationAsync(
        AIAgent researchAgent,
        ResearchSubTask subTask,
        SubTaskIterationContext context,
        CancellationToken cancellationToken)
    {
        if (context.Count > 1 && context.HasPreviousEvaluation)
        {
            var refinementPrompt = this.BuildRefinementPrompt(
                subTask.Query,
                context.PreviousFindings!,
                context.PreviousEvaluation!,
                context.CurrentQuery);
            return RunResearchAsync(researchAgent, refinementPrompt, cancellationToken);
        }

        return RunResearchAsync(researchAgent, context.CurrentQuery, cancellationToken);
    }

    private bool TryCompleteSubTask(ResearchSubTask subTask, string findings, ResearchEvaluation evaluation)
    {
        if (!evaluation.IsSatisfactory || evaluation.QualityScore < qualityThreshold)
        {
            return false;
        }

        subTask.Findings = findings;
        subTask.QualityScore = evaluation.QualityScore;
        subTask.IsComplete = true;
        return true;
    }

    private bool TryRefineQuery(SubTaskIterationContext context, ResearchEvaluation evaluation, int subTaskNum)
    {
        context.StoreEvaluation(context.CurrentQuery, evaluation);

        if (!string.IsNullOrWhiteSpace(evaluation.RefinedQuery))
        {
            var truncatedQuery = TruncateForDisplay(evaluation.RefinedQuery, 80);
            this.ReportProgress("Sub-task {0}: Refining research focus: \"{1}\"", subTaskNum, truncatedQuery);
            context.UpdateQuery(evaluation.RefinedQuery);
            return true;
        }

        if (evaluation.FollowUpQuestions.Length > 0)
        {
            var truncatedQuestion = TruncateForDisplay(evaluation.FollowUpQuestions[0], 80);
            this.ReportProgress("Sub-task {0}: Following up: \"{1}\"", subTaskNum, truncatedQuestion);
            context.UpdateQuery(evaluation.FollowUpQuestions[0]);
            return true;
        }

        return false;
    }

    private void AcceptCurrentFindings(
        ResearchSubTask subTask,
        string findings,
        ResearchEvaluation evaluation,
        int subTaskNum)
    {
        this.ReportProgress(
            "Sub-task {0}: No refinement available, accepting current findings (score {1})",
            subTaskNum,
            evaluation.QualityScore);
        subTask.Findings = findings;
        subTask.QualityScore = evaluation.QualityScore;
        subTask.IsComplete = true;
    }

    private string BuildRefinementPrompt(
        string originalQuery,
        string previousFindings,
        ResearchEvaluation evaluation,
        string refinedQuery)
    {
        var gapsText = evaluation.Gaps.Length > 0
            ? string.Join("\n- ", evaluation.Gaps)
            : "No specific gaps identified";

        var truncatedFindings = previousFindings.Length > 2000
            ? previousFindings[..2000] + "\n[...truncated for context...]"
            : previousFindings;

        return string.Format(
            CultureInfo.InvariantCulture,
            RefinementPromptFormat,
            originalQuery,
            truncatedFindings,
            evaluation.QualityScore,
            qualityThreshold,
            evaluation.Reasoning,
            "- " + gapsText,
            refinedQuery);
    }

    private void ReportProgress(string format, params object[] args) =>
        onProgress?.Invoke(string.Format(CultureInfo.InvariantCulture, format, args));

    /// <summary>
    /// Tracks iteration state for a sub-task to avoid excessive method parameters.
    /// </summary>
    /// <param name="initialQuery">The initial query for this sub-task.</param>
    private sealed class SubTaskIterationContext(string initialQuery)
    {
        public string CurrentQuery { get; private set; } = initialQuery;

        public int Count { get; private set; }

        public string? PreviousFindings { get; private set; }

        public ResearchEvaluation? PreviousEvaluation { get; private set; }

        public bool HasPreviousEvaluation => this.PreviousFindings is not null && this.PreviousEvaluation is not null;

        public void IncrementCount() => this.Count++;

        public void UpdateQuery(string newQuery) => this.CurrentQuery = newQuery;

        public void StoreEvaluation(string findings, ResearchEvaluation evaluation)
        {
            this.PreviousFindings = findings;
            this.PreviousEvaluation = evaluation;
        }
    }
}
