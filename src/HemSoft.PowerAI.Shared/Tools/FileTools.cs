// <copyright file="FileTools.cs" company="HemSoft">
// Copyright © 2025 HemSoft
// </copyright>

namespace HemSoft.PowerAI.Common.Tools;

using System.ComponentModel;
using System.Globalization;
using System.IO;

/// <summary>
/// Provides file system tools for the AI agent.
/// </summary>
public static class FileTools
{
    private const string QueryDescription =
        "Query file system. Modes: list, count, info, read. Path is file or directory.";

    private const string ModifyDescription =
        "Modify file system. Modes: 'write' (create/overwrite file with content), 'mkdir' (create folder), " +
        "'delete' (remove), 'move', 'copy'. For write: filePath is target, content is file text.";

    /// <summary>
    /// Queries the file system for information.
    /// </summary>
    /// <param name="mode">Operation mode: 'list' (files in dir), 'count' (file count), 'info' (file details), 'read' (file contents).</param>
    /// <param name="path">The file or directory path.</param>
    /// <returns>Query result or error message.</returns>
    [Description(QueryDescription)]
    public static string QueryFileSystem(string mode, string path)
    {
        path = SanitizePath(path);
        Console.WriteLine($"[Tool] QueryFileSystem: {mode} {path}");

        return mode?.ToUpperInvariant() switch
        {
            "LIST" => ListFiles(path),
            "COUNT" => CountFiles(path),
            "INFO" => GetFileInfo(path),
            "READ" => ReadFileContents(path),
            _ => $"Unknown mode '{mode}'. Use: list, count, info, read",
        };
    }

    /// <summary>
    /// Modifies the file system.
    /// </summary>
    /// <param name="mode">Operation mode: 'mkdir' (create folder), 'delete' (remove file/folder).</param>
    /// <param name="filePath">The target file path.</param>
    /// <returns>Result message or error.</returns>
    [Description(ModifyDescription)]
    public static string ModifyFileSystem(string mode, string filePath) =>
        ModifyFileSystemCore(mode, filePath, content: null);

    /// <summary>
    /// Modifies the file system with content.
    /// </summary>
    /// <param name="mode">Operation mode: 'move', 'copy', 'write' (create/overwrite file).</param>
    /// <param name="filePath">The target file path for write, or source path for other operations.</param>
    /// <param name="content">The file content (for write mode) or destination path (for move/copy).</param>
    /// <returns>Result message or error.</returns>
    [Description(ModifyDescription)]
    public static string ModifyFileSystem(string mode, string filePath, string content) =>
        ModifyFileSystemCore(mode, filePath, content);

    /// <summary>
    /// Formats a file operation error message.
    /// </summary>
    /// <param name="operation">The operation that failed.</param>
    /// <param name="path">The path involved.</param>
    /// <param name="ex">The exception that occurred.</param>
    /// <returns>Formatted error message.</returns>
    internal static string FormatError(string operation, string path, Exception ex) =>
        ex is UnauthorizedAccessException
            ? $"Access denied: {path}"
            : $"Error {operation} {path}: {ex.Message}";

    /// <summary>
    /// Sanitizes a file path by removing invalid characters and extra text that LLMs sometimes append.
    /// </summary>
    /// <param name="path">The file path to sanitize.</param>
    /// <returns>The sanitized path.</returns>
    private static string SanitizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        // Trim whitespace and remove common LLM mistakes: PowerShell/bash flags appended to paths
        // e.g., "F:\weather.md -Encoding utf8" -> "F:\weather.md"
        var flagPatterns = new[] { " -Encoding", " -Force", " -NoNewline", " >", " |", " &&", " ||" };
        var sanitized = path.Trim();

        foreach (var pattern in flagPatterns)
        {
            var idx = sanitized.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (idx > 0)
            {
                sanitized = sanitized[..idx];
            }
        }

        // Remove quotes that LLMs sometimes include
        return sanitized.Trim('"', '\'');
    }

    private static string ModifyFileSystemCore(string mode, string filePath, string? content)
    {
        filePath = SanitizePath(filePath);
        var contentPreview = GetContentPreview(content);
        Console.WriteLine($"[Tool] ModifyFileSystem: {mode} {filePath} -> {contentPreview}");

        return mode?.ToUpperInvariant() switch
        {
            "MKDIR" => CreateFolder(filePath),
            "DELETE" => Delete(filePath),
            "MOVE" => Move(filePath, content),
            "COPY" => Copy(filePath, content),
            "WRITE" => WriteFile(filePath, content),
            _ => $"Unknown mode '{mode}'. Use: mkdir, delete, move, copy, write",
        };
    }

    private static string GetContentPreview(string? content)
    {
        const int maxPreviewLength = 50;

        return content switch
        {
            null => "(null)",
            { Length: <= maxPreviewLength } => content,
            _ => $"{content[..maxPreviewLength]}...",
        };
    }

    private static string ListFiles(string path)
    {
        if (!Directory.Exists(path))
        {
            return $"Directory not found: {path}";
        }

        var files = Directory.EnumerateFiles(path, "*", SearchOption.TopDirectoryOnly)
            .Take(100)
            .Select(Path.GetFileName)
            .Where(f => f is not null);

        var fileList = string.Join(", ", files);
        return string.IsNullOrEmpty(fileList) ? "Empty directory" : fileList;
    }

    private static string CountFiles(string path) =>
        !Directory.Exists(path)
            ? $"Directory not found: {path}"
            : $"{Directory.EnumerateFiles(path, "*", SearchOption.TopDirectoryOnly).Count()} files";

    private static string GetFileInfo(string path)
    {
        if (!File.Exists(path))
        {
            return $"File not found: {path}";
        }

        var info = new FileInfo(path);
        var sizeFormatted = info.Length.ToString("N0", CultureInfo.InvariantCulture);
        var modifiedFormatted = info.LastWriteTime.ToString("g", CultureInfo.InvariantCulture);
        return $"{info.Name} | {sizeFormatted} bytes | Modified: {modifiedFormatted}";
    }

    private static string ReadFileContents(string path)
    {
        if (!File.Exists(path))
        {
            return $"File not found: {path}";
        }

        try
        {
            var content = File.ReadAllText(path);
            if (string.IsNullOrEmpty(content))
            {
                return "[Empty file]";
            }

            const int maxLength = 10000;
            if (content.Length > maxLength)
            {
                var lengthFormatted = content.Length.ToString("N0", CultureInfo.InvariantCulture);
                return $"{content[..maxLength]}\n\n[Truncated - file is {lengthFormatted} characters]";
            }

            return content;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or ArgumentException)
        {
            return FormatError("reading", path, ex);
        }
    }

    private static string CreateFolder(string path)
    {
        try
        {
            _ = Directory.CreateDirectory(path);
            return $"Created: {path}";
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or ArgumentException)
        {
            return FormatError("creating", path, ex);
        }
    }

    private static string Delete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
                return $"Deleted file: {path}";
            }

            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
                return $"Deleted directory: {path}";
            }

            return $"Not found: {path}";
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or ArgumentException)
        {
            return FormatError("deleting", path, ex);
        }
    }

    private static string Move(string source, string? destination)
    {
        if (string.IsNullOrEmpty(destination))
        {
            return "Destination required for move operation";
        }

        try
        {
            if (File.Exists(source))
            {
                File.Move(source, destination);
                return $"Moved file: {source} -> {destination}";
            }

            if (Directory.Exists(source))
            {
                Directory.Move(source, destination);
                return $"Moved directory: {source} -> {destination}";
            }

            return $"Source not found: {source}";
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or ArgumentException)
        {
            return FormatError("moving", source, ex);
        }
    }

    private static string Copy(string source, string? destination)
    {
        if (string.IsNullOrEmpty(destination))
        {
            return "Destination required for copy operation";
        }

        try
        {
            if (File.Exists(source))
            {
                File.Copy(source, destination, overwrite: true);
                return $"Copied file: {source} -> {destination}";
            }

            return Directory.Exists(source)
                ? "Directory copy not supported. Use move or copy individual files."
                : $"Source not found: {source}";
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or ArgumentException)
        {
            return FormatError("copying", source, ex);
        }
    }

    private static string WriteFile(string path, string? content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return "Content required for write operation (pass as destination parameter)";
        }

        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                _ = Directory.CreateDirectory(directory);
            }

            // Unescape common escape sequences that LLMs send as literal strings
            var unescaped = content
                .Replace("\\n", "\n", StringComparison.Ordinal)
                .Replace("\\t", "\t", StringComparison.Ordinal)
                .Replace("\\r", "\r", StringComparison.Ordinal);

            File.WriteAllText(path, unescaped);
            var lengthFormatted = unescaped.Length.ToString("N0", CultureInfo.InvariantCulture);
            return $"Written {lengthFormatted} characters to: {path}";
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or ArgumentException)
        {
            return FormatError("writing", path, ex);
        }
    }
}
