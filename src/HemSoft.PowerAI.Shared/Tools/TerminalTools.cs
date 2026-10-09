// <copyright file="TerminalTools.cs" company="HemSoft">
// Copyright © 2025 HemSoft
// </copyright>

namespace HemSoft.PowerAI.Common.Tools;

using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

/// <summary>
/// Provides unified terminal access for the AI agent.
/// Combines command execution, file operations, and process management in a single tool.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Process execution cannot be reliably unit tested")]
public static class TerminalTools
{
    private const int MaxOutputLength = 8192;
    private const int DefaultTimeoutSeconds = 30;
    private const int MaxTimeoutSeconds = 300;

    private const string TerminalDescription =
        "Execute terminal commands. PowerShell on Windows, bash on Unix. Returns stdout, stderr, exit code.";

    private const string BatchDescription =
        "Execute multiple semicolon-separated commands. Set continueOnError=true to run all.";

    private const string WindowsShell = "pwsh";
    private const string WindowsShellArgs = "-NoProfile -NonInteractive -Command";
    private const string UnixShell = "/bin/bash";
    private const string UnixShellArgs = "-c";
    private const string WindowsQuoteEscape = "`\"";
    private const string UnixQuoteEscape = "\\\"";

    /// <summary>
    /// Executes a terminal command with full shell access.
    /// Supports PowerShell on Windows and bash on Unix-like systems.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <returns>Command output including stdout, stderr, and exit code.</returns>
    [Description(TerminalDescription)]
    public static string Terminal(string command) =>
        TerminalCore(command, workingDirectory: null, timeoutSeconds: DefaultTimeoutSeconds);

    /// <summary>
    /// Executes a terminal command with full shell access.
    /// Supports PowerShell on Windows and bash on Unix-like systems.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="workingDirectory">Working directory for command execution.</param>
    /// <returns>Command output including stdout, stderr, and exit code.</returns>
    [Description(TerminalDescription)]
    public static string Terminal(string command, string workingDirectory) =>
        TerminalCore(command, workingDirectory, timeoutSeconds: DefaultTimeoutSeconds);

    /// <summary>
    /// Executes a terminal command with full shell access.
    /// Supports PowerShell on Windows and bash on Unix-like systems.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="workingDirectory">Working directory for command execution.</param>
    /// <param name="timeoutSeconds">Timeout in seconds (max: 300).</param>
    /// <returns>Command output including stdout, stderr, and exit code.</returns>
    [Description(TerminalDescription)]
    public static string Terminal(string command, string workingDirectory, int timeoutSeconds) =>
        TerminalCore(command, workingDirectory, timeoutSeconds);

    /// <summary>
    /// Executes multiple commands in sequence, stopping on first failure.
    /// </summary>
    /// <param name="commands">Semicolon-separated list of commands.</param>
    /// <returns>Combined output from all commands.</returns>
    [Description(BatchDescription)]
    public static string ExecuteBatch(string commands) =>
        ExecuteBatchCore(commands, workingDirectory: null, continueOnError: false);

    /// <summary>
    /// Executes multiple commands in sequence, stopping on first failure.
    /// </summary>
    /// <param name="commands">Semicolon-separated list of commands.</param>
    /// <param name="workingDirectory">Working directory for command execution.</param>
    /// <returns>Combined output from all commands.</returns>
    [Description(BatchDescription)]
    public static string ExecuteBatch(string commands, string workingDirectory) =>
        ExecuteBatchCore(commands, workingDirectory, continueOnError: false);

    /// <summary>
    /// Executes multiple commands in sequence.
    /// </summary>
    /// <param name="commands">Semicolon-separated list of commands.</param>
    /// <param name="workingDirectory">Working directory for command execution.</param>
    /// <param name="continueOnError">If true, continues execution even if a command fails.</param>
    /// <returns>Combined output from all commands.</returns>
    [Description(BatchDescription)]
    public static string ExecuteBatch(string commands, string workingDirectory, bool continueOnError) =>
        ExecuteBatchCore(commands, workingDirectory, continueOnError);

    private static string TerminalCore(string command, string? workingDirectory, int timeoutSeconds)
    {
        Console.WriteLine($"[Tool] Terminal: {command}");

        if (string.IsNullOrWhiteSpace(command))
        {
            return "Error: Command cannot be empty";
        }

        // Clamp timeout to reasonable bounds
        var timeout = Math.Clamp(timeoutSeconds, 1, MaxTimeoutSeconds) * 1000;

        var (shell, shellArgs) = GetShellInfo();
        var fullArgs = $"{shellArgs} \"{EscapeCommand(command)}\"";

        var startInfo = new ProcessStartInfo
        {
            FileName = shell,
            Arguments = fullArgs,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = ResolveWorkingDirectory(workingDirectory),
        };

        return RunProcess(startInfo, timeout);
    }

    private static string ExecuteBatchCore(string commands, string? workingDirectory, bool continueOnError)
    {
        Console.WriteLine($"[Tool] ExecuteBatch: {commands}");

        if (string.IsNullOrWhiteSpace(commands))
        {
            return "Error: Commands cannot be empty";
        }

        var commandList = commands.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return ExecuteCommandList(commandList, workingDirectory, continueOnError);
    }

    private static string ExecuteCommandList(string[] commandList, string? workingDirectory, bool continueOnError)
    {
        var results = new StringBuilder();
        var allSucceeded = true;
        var shouldStop = false;
        var resolvedWorkingDir = ResolveWorkingDirectory(workingDirectory);

        foreach (var cmd in commandList)
        {
            if (shouldStop)
            {
                continue;
            }

            _ = results.Append(">>> ").AppendLine(cmd);
            var result = TerminalCore(cmd, resolvedWorkingDir, DefaultTimeoutSeconds);
            _ = results.AppendLine(result);

            var failed = result.Contains("Exit: ", StringComparison.Ordinal) &&
                         !result.Contains("Exit: 0", StringComparison.Ordinal);

            if (failed)
            {
                allSucceeded = false;
                if (!continueOnError)
                {
                    _ = results.AppendLine("Batch stopped due to failure. Use continueOnError=true.");
                    shouldStop = true;
                }
            }
        }

        _ = results.Append("Batch complete. Success: ").Append(allSucceeded).AppendLine();
        return results.ToString();
    }

    private static (string Shell, string Args) GetShellInfo() =>
        OperatingSystem.IsWindows()
            ? (WindowsShell, WindowsShellArgs)
            : (UnixShell, UnixShellArgs);

    private static string EscapeCommand(string command) =>
        OperatingSystem.IsWindows()
            ? command.Replace("\"", WindowsQuoteEscape, StringComparison.Ordinal)
            : command.Replace("\"", UnixQuoteEscape, StringComparison.Ordinal);

    private static string ResolveWorkingDirectory(string? workingDirectory) =>
        string.IsNullOrEmpty(workingDirectory) || !Directory.Exists(workingDirectory)
            ? Environment.CurrentDirectory
            : workingDirectory;

    private static string RunProcess(ProcessStartInfo startInfo, int timeoutMs)
    {
        var output = new StringBuilder();
        var error = new StringBuilder();

        try
        {
            using var process = new Process { StartInfo = startInfo };

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is not null && output.Length < MaxOutputLength)
                {
                    output.AppendLine(e.Data);
                }
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is not null && error.Length < MaxOutputLength)
                {
                    error.AppendLine(e.Data);
                }
            };

            if (!process.Start())
            {
                return "Error: Failed to start process";
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var completed = process.WaitForExit(timeoutMs);

            if (!completed)
            {
                process.Kill(entireProcessTree: true);
                var timeoutSecs = (timeoutMs / 1000).ToString(CultureInfo.InvariantCulture);
                return $"Error: Command timed out after {timeoutSecs}s\nPartial output:\n{output}";
            }

            // Complete the redirected stream callbacks before collecting their buffers.
            process.WaitForExit();
            return FormatResult(output.ToString(), error.ToString(), process.ExitCode);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return $"Error: Failed to execute command - {ex.Message}";
        }
    }

    private static string FormatResult(string stdout, string stderr, int exitCode)
    {
        var result = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(stdout))
        {
            _ = result.AppendLine(stdout.TrimEnd());
        }

        if (!string.IsNullOrWhiteSpace(stderr))
        {
            _ = result.Append("[stderr] ").AppendLine(stderr.TrimEnd());
        }

        _ = result.Append("Exit: ").Append(exitCode).AppendLine();

        return result.ToString();
    }
}
