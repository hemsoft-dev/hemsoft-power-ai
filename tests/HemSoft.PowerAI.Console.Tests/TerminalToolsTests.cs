// <copyright file="TerminalToolsTests.cs" company="HemSoft">
// Copyright © 2025 HemSoft
// </copyright>

namespace HemSoft.PowerAI.Console.Tests;

using HemSoft.PowerAI.Console.Tools;

/// <summary>
/// Unit tests for the <see cref="TerminalTools"/> class.
/// </summary>
public sealed class TerminalToolsTests
{
    /// <summary>
    /// Tests that Execute returns an error for empty commands.
    /// </summary>
    [Fact]
    public void ExecuteEmptyCommandReturnsError()
    {
        var result = TerminalTools.Terminal(string.Empty);

        Assert.Contains("Error: Command cannot be empty", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tests that Execute returns an error for whitespace-only commands.
    /// </summary>
    [Fact]
    public void ExecuteWhitespaceCommandReturnsError()
    {
        var result = TerminalTools.Terminal("   ");

        Assert.Contains("Error: Command cannot be empty", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tests that Execute can run a simple echo command.
    /// </summary>
    [Fact]
    public void ExecuteEchoCommandReturnsOutput()
    {
        var result = TerminalTools.Terminal("echo 'hello world'");

        Assert.Contains("hello world", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Exit: 0", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tests that Execute includes exit code in output.
    /// </summary>
    [Fact]
    public void ExecuteValidCommandIncludesExitCode()
    {
        var result = TerminalTools.Terminal("echo test");

        Assert.Contains("Exit:", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tests that Execute handles working directory parameter.
    /// </summary>
    [Fact]
    public void ExecuteWithWorkingDirectoryExecutesInDirectory()
    {
        var tempDir = Path.GetTempPath();
        var result = TerminalTools.Terminal(OperatingSystem.IsWindows() ? "Get-Location" : "pwd", tempDir);

        Assert.Contains("Exit: 0", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tests that Execute handles invalid working directory gracefully.
    /// </summary>
    [Fact]
    public void ExecuteInvalidWorkingDirectoryUsesCurrentDirectory()
    {
        var result = TerminalTools.Terminal("echo test", "C:\\NonExistentPath\\12345");

        Assert.Contains("Exit: 0", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tests that Execute respects timeout parameter.
    /// </summary>
    [Fact]
    public void ExecuteWithTimeoutRespectsTimeout()
    {
        // Should complete quickly
        var result = TerminalTools.Terminal("echo fast", timeoutSeconds: 5);

        Assert.Contains("Exit: 0", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tests that ExecuteBatch returns error for empty commands.
    /// </summary>
    [Fact]
    public void ExecuteBatchEmptyCommandsReturnsError()
    {
        var result = TerminalTools.ExecuteBatch(string.Empty);

        Assert.Contains("Error: Commands cannot be empty", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tests that ExecuteBatch executes multiple commands.
    /// </summary>
    [Fact]
    public void ExecuteBatchMultipleCommandsExecutesAll()
    {
        var result = TerminalTools.ExecuteBatch("echo first; echo second");

        Assert.Contains("first", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("second", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Batch complete. Success: True", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tests that ExecuteBatch stops on first failure by default.
    /// </summary>
    [Fact]
    public void ExecuteBatchFailureWithoutContinueStopsExecution()
    {
        var result = TerminalTools.ExecuteBatch("exit 1; echo should_not_run");

        Assert.Contains("Batch stopped due to failure", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tests that ExecuteBatch continues on error when continueOnError is true.
    /// </summary>
    [Fact]
    public void ExecuteBatchFailureWithContinueContinuesExecution()
    {
        var result = TerminalTools.ExecuteBatch("exit 1; echo continued", continueOnError: true);

        Assert.Contains("continued", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Batch complete. Success: False", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tests that ExecuteBatch handles working directory.
    /// </summary>
    [Fact]
    public void ExecuteBatchWithWorkingDirectoryExecutesInDirectory()
    {
        var tempDir = Path.GetTempPath();
        var result = TerminalTools.ExecuteBatch(OperatingSystem.IsWindows() ? "Get-Location" : "pwd", tempDir);

        Assert.Contains("Batch complete", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tests that Execute handles a command that writes to stderr.
    /// </summary>
    [Fact]
    public void ExecuteCommandWithStderrReturnsStderr()
    {
        var result = TerminalTools.Terminal("Write-Error 'test error' 2>&1");

        Assert.Contains("Exit:", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tests that Execute handles commands with special characters.
    /// </summary>
    [Fact]
    public void ExecuteCommandWithSpecialCharactersWorks()
    {
        var result = TerminalTools.Terminal("echo 'hello \"world\"'");

        Assert.Contains("Exit:", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tests that Execute clamps timeout to minimum value.
    /// </summary>
    [Fact]
    public void ExecuteWithZeroTimeoutUsesMinimum()
    {
        var result = TerminalTools.Terminal("echo test", timeoutSeconds: 0);

        Assert.Contains("Exit: 0", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tests that Execute clamps timeout to maximum value.
    /// </summary>
    [Fact]
    public void ExecuteWithLargeTimeoutUsesMaximum()
    {
        var result = TerminalTools.Terminal("echo test", timeoutSeconds: 1000);

        Assert.Contains("Exit: 0", result, StringComparison.Ordinal);
    }
}
