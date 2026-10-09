// <copyright file="ProgressSubscription.cs" company="HemSoft">
// Copyright © 2025 HemSoft
// </copyright>

namespace HemSoft.PowerAI.Console.Services;

/// <summary>
/// Completes optional progress reporting without replacing a research result.
/// </summary>
internal static class ProgressSubscription
{
    /// <summary>
    /// Cancels reporting and observes cleanup failures within a bounded wait.
    /// </summary>
    /// <param name="subscription">The background subscription.</param>
    /// <param name="cancellation">The subscription cancellation source.</param>
    /// <param name="log">The diagnostic logger.</param>
    /// <param name="timeout">The maximum cleanup delay.</param>
    /// <returns>The cleanup completion task.</returns>
    internal static async Task CompleteAsync(
        Task subscription, CancellationTokenSource cancellation, Action<string> log, TimeSpan timeout)
    {
        var cleanup = Task.WhenAll(cancellation.CancelAsync(), subscription);
        var observed = cleanup.ContinueWith(
            completed =>
            {
                if (completed.Exception is not null)
                {
                    log("Progress reporting cleanup failed: " + completed.Exception.GetBaseException().Message);
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        try
        {
            await observed.WaitAsync(timeout, CancellationToken.None).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            log("Progress reporting cleanup exceeded its time limit; the task result is preserved.");
        }
    }
}
