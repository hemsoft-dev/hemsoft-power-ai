// <copyright file="SharedTerminalToolsTests.cs" company="HemSoft">
// Copyright © 2025 HemSoft
// </copyright>

namespace HemSoft.PowerAI.Console.Tests;

using HemSoft.PowerAI.Common.Tools;

/// <summary>
/// Verifies shared terminal output and platform-specific working-directory commands.
/// </summary>
public sealed class SharedTerminalToolsTests
{
    /// <summary>
    /// Even a short-lived command must return its complete redirected output.
    /// </summary>
    [Fact]
    public void ShortCommandPreservesOutput()
    {
        var result = TerminalTools.Terminal("echo 'hello world'");
        Assert.Contains("hello world", result, StringComparison.Ordinal);
        Assert.Contains("Exit: 0", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Runs the native shell's directory command in the requested directory.
    /// </summary>
    [Fact]
    public void WorkingDirectoryUsesNativeShell()
    {
        var result = TerminalTools.Terminal(OperatingSystem.IsWindows() ? "Get-Location" : "pwd", Path.GetTempPath());
        Assert.Contains(Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar), result, StringComparison.Ordinal);
        Assert.Contains("Exit: 0", result, StringComparison.Ordinal);
    }
}
