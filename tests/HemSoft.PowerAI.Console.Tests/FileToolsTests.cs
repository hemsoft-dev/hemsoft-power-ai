// <copyright file="FileToolsTests.cs" company="HemSoft">
// Copyright © 2025 HemSoft
// </copyright>

namespace HemSoft.PowerAI.Console.Tests;

using HemSoft.PowerAI.Console.Tools;

/// <summary>
/// Unit tests for <see cref="FileTools"/>.
/// </summary>
public sealed class FileToolsTests : IDisposable
{
    private readonly string testDir;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileToolsTests"/> class.
    /// </summary>
    public FileToolsTests()
    {
        this.testDir = Path.Combine(Path.GetTempPath(), $"FileToolsTests_{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(this.testDir);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Directory.Exists(this.testDir))
        {
            Directory.Delete(this.testDir, recursive: true);
        }
    }

    /// <summary>
    /// Verifies QueryFileSystem list mode returns files.
    /// </summary>
    [Fact]
    public void QueryFileSystemListReturnsFiles()
    {
        // Arrange
        File.WriteAllText(Path.Combine(this.testDir, "test1.txt"), "content1");
        File.WriteAllText(Path.Combine(this.testDir, "test2.txt"), "content2");

        // Act
        var result = FileTools.QueryFileSystem("list", this.testDir);

        // Assert
        Assert.Contains("test1.txt", result, StringComparison.Ordinal);
        Assert.Contains("test2.txt", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies QueryFileSystem list mode returns empty for empty directory.
    /// </summary>
    [Fact]
    public void QueryFileSystemListEmptyDirectory()
    {
        // Act
        var result = FileTools.QueryFileSystem("list", this.testDir);

        // Assert
        Assert.Equal("Empty directory", result);
    }

    /// <summary>
    /// Verifies QueryFileSystem list mode handles non-existent path.
    /// </summary>
    [Fact]
    public void QueryFileSystemListNonExistentPath()
    {
        // Act
        var result = FileTools.QueryFileSystem("list", Path.Combine(this.testDir, "nonexistent"));

        // Assert
        Assert.Contains("not found", result, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies QueryFileSystem count mode returns correct count.
    /// </summary>
    [Fact]
    public void QueryFileSystemCountReturnsCount()
    {
        // Arrange
        File.WriteAllText(Path.Combine(this.testDir, "file1.txt"), "content");
        File.WriteAllText(Path.Combine(this.testDir, "file2.txt"), "content");

        // Act
        var result = FileTools.QueryFileSystem("count", this.testDir);

        // Assert
        Assert.Equal("2 files", result);
    }

    /// <summary>
    /// Verifies QueryFileSystem count mode handles non-existent path.
    /// </summary>
    [Fact]
    public void QueryFileSystemCountNonExistentPath()
    {
        // Act
        var result = FileTools.QueryFileSystem("count", Path.Combine(this.testDir, "nonexistent"));

        // Assert
        Assert.Contains("not found", result, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies QueryFileSystem info mode returns file information.
    /// </summary>
    [Fact]
    public void QueryFileSystemInfoReturnsFileInfo()
    {
        // Arrange
        var filePath = Path.Combine(this.testDir, "info.txt");
        File.WriteAllText(filePath, "test content");

        // Act
        var result = FileTools.QueryFileSystem("info", filePath);

        // Assert
        Assert.Contains("info.txt", result, StringComparison.Ordinal);
        Assert.Contains("bytes", result, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies QueryFileSystem info mode handles non-existent file.
    /// </summary>
    [Fact]
    public void QueryFileSystemInfoNonExistentFile()
    {
        // Act
        var result = FileTools.QueryFileSystem("info", Path.Combine(this.testDir, "nonexistent.txt"));

        // Assert
        Assert.Contains("not found", result, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies QueryFileSystem returns error for unknown mode.
    /// </summary>
    [Fact]
    public void QueryFileSystemUnknownMode()
    {
        // Act
        var result = FileTools.QueryFileSystem("invalid", this.testDir);

        // Assert
        Assert.Contains("Unknown mode", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies ModifyFileSystem mkdir creates folder.
    /// </summary>
    [Fact]
    public void ModifyFileSystemMkdirCreatesFolder()
    {
        // Arrange
        var newFolder = Path.Combine(this.testDir, "newfolder");

        // Act
        var result = FileTools.ModifyFileSystem("mkdir", newFolder);

        // Assert
        Assert.Contains("Created", result, StringComparison.OrdinalIgnoreCase);
        Assert.True(Directory.Exists(newFolder));
    }

    /// <summary>
    /// Verifies ModifyFileSystem mkdir handles invalid path.
    /// </summary>
    [Fact]
    public void ModifyFileSystemMkdirInvalidPath()
    {
        // Arrange
        var invalidPath = Path.Combine(this.testDir, "invalid\0path");

        // Act
        var result = FileTools.ModifyFileSystem("mkdir", invalidPath);

        // Assert
        Assert.Contains("Error creating", result, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies ModifyFileSystem delete removes file.
    /// </summary>
    [Fact]
    public void ModifyFileSystemDeleteRemovesFile()
    {
        // Arrange
        var filePath = Path.Combine(this.testDir, "todelete.txt");
        File.WriteAllText(filePath, "content");

        // Act
        var result = FileTools.ModifyFileSystem("delete", filePath);

        // Assert
        Assert.Contains("Deleted file", result, StringComparison.Ordinal);
        Assert.False(File.Exists(filePath));
    }

    /// <summary>
    /// Verifies ModifyFileSystem delete removes directory.
    /// </summary>
    [Fact]
    public void ModifyFileSystemDeleteRemovesDirectory()
    {
        // Arrange
        var dirPath = Path.Combine(this.testDir, "todelete");
        _ = Directory.CreateDirectory(dirPath);

        // Act
        var result = FileTools.ModifyFileSystem("delete", dirPath);

        // Assert
        Assert.Contains("Deleted directory", result, StringComparison.Ordinal);
        Assert.False(Directory.Exists(dirPath));
    }

    /// <summary>
    /// Verifies ModifyFileSystem delete handles non-existent path.
    /// </summary>
    [Fact]
    public void ModifyFileSystemDeleteNonExistent()
    {
        // Act
        var result = FileTools.ModifyFileSystem("delete", Path.Combine(this.testDir, "nonexistent"));

        // Assert
        Assert.Contains("Not found", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies ModifyFileSystem move moves file.
    /// </summary>
    [Fact]
    public void ModifyFileSystemMoveMovesFile()
    {
        // Arrange
        var source = Path.Combine(this.testDir, "source.txt");
        var dest = Path.Combine(this.testDir, "dest.txt");
        File.WriteAllText(source, "content");

        // Act
        var result = FileTools.ModifyFileSystem("move", source, dest);

        // Assert
        Assert.Contains("Moved file", result, StringComparison.Ordinal);
        Assert.False(File.Exists(source));
        Assert.True(File.Exists(dest));
    }

    /// <summary>
    /// Verifies ModifyFileSystem move requires destination.
    /// </summary>
    [Fact]
    public void ModifyFileSystemMoveRequiresDestination()
    {
        // Act
        var result = FileTools.ModifyFileSystem("move", this.testDir);

        // Assert
        Assert.Contains("Destination required", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies ModifyFileSystem copy copies file.
    /// </summary>
    [Fact]
    public void ModifyFileSystemCopyCopiesFile()
    {
        // Arrange
        var source = Path.Combine(this.testDir, "source.txt");
        var dest = Path.Combine(this.testDir, "copy.txt");
        File.WriteAllText(source, "content");

        // Act
        var result = FileTools.ModifyFileSystem("copy", source, dest);

        // Assert
        Assert.Contains("Copied file", result, StringComparison.Ordinal);
        Assert.True(File.Exists(source));
        Assert.True(File.Exists(dest));
    }

    /// <summary>
    /// Verifies ModifyFileSystem copy requires destination.
    /// </summary>
    [Fact]
    public void ModifyFileSystemCopyRequiresDestination()
    {
        // Act
        var result = FileTools.ModifyFileSystem("copy", this.testDir);

        // Assert
        Assert.Contains("Destination required", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies ModifyFileSystem copy handles non-existent source.
    /// </summary>
    [Fact]
    public void ModifyFileSystemCopyNonExistentSource()
    {
        // Act
        var result = FileTools.ModifyFileSystem("copy", Path.Combine(this.testDir, "nonexistent"), "dest");

        // Assert
        Assert.Contains("Source not found", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies ModifyFileSystem copy handles directory (not supported).
    /// </summary>
    [Fact]
    public void ModifyFileSystemCopyDirectoryNotSupported()
    {
        // Arrange
        var sourceDir = Path.Combine(this.testDir, "sourcedir");
        _ = Directory.CreateDirectory(sourceDir);

        // Act
        var result = FileTools.ModifyFileSystem("copy", sourceDir, "dest");

        // Assert
        Assert.Contains("Directory copy not supported", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies ModifyFileSystem move handles non-existent source.
    /// </summary>
    [Fact]
    public void ModifyFileSystemMoveNonExistentSource()
    {
        // Act
        var result = FileTools.ModifyFileSystem("move", Path.Combine(this.testDir, "nonexistent"), "dest");

        // Assert
        Assert.Contains("Source not found", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies ModifyFileSystem move moves directory.
    /// </summary>
    [Fact]
    public void ModifyFileSystemMoveMovesDirectory()
    {
        // Arrange
        var sourceDir = Path.Combine(this.testDir, "sourcedir");
        var destDir = Path.Combine(this.testDir, "destdir");
        _ = Directory.CreateDirectory(sourceDir);

        // Act
        var result = FileTools.ModifyFileSystem("move", sourceDir, destDir);

        // Assert
        Assert.Contains("Moved directory", result, StringComparison.Ordinal);
        Assert.False(Directory.Exists(sourceDir));
        Assert.True(Directory.Exists(destDir));
    }

    /// <summary>
    /// Verifies ModifyFileSystem move handles IO error (destination exists).
    /// </summary>
    [Fact]
    public void ModifyFileSystemMoveDestinationExists()
    {
        // Arrange - create source and destination with same name
        var source = Path.Combine(this.testDir, "movesource.txt");
        var dest = Path.Combine(this.testDir, "movedest.txt");
        File.WriteAllText(source, "source");
        File.WriteAllText(dest, "dest");

        // Act
        var result = FileTools.ModifyFileSystem("move", source, dest);

        // Assert
        Assert.Contains("Error moving", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies ModifyFileSystem copy handles IO error (invalid destination).
    /// </summary>
    [Fact]
    public void ModifyFileSystemCopyInvalidDestination()
    {
        // Arrange
        var source = Path.Combine(this.testDir, "copysource.txt");
        File.WriteAllText(source, "content");
        var invalidDest = Path.Combine(this.testDir, "invalid\0dest.txt");

        // Act
        var result = FileTools.ModifyFileSystem("copy", source, invalidDest);

        // Assert
        Assert.Contains("Error copying", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies ModifyFileSystem delete handles IO error (file in use - simulated via invalid path).
    /// </summary>
    [Fact]
    public void ModifyFileSystemDeleteInvalidPath()
    {
        // Arrange - use path that will cause IO error on some systems
        const string InvalidPath = "CON"; // Reserved Windows device name

        // Act
        var result = FileTools.ModifyFileSystem("delete", InvalidPath);

        // Assert - should return "Not found" since reserved names don't exist as files
        Assert.Contains("Not found", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies ModifyFileSystem returns error for unknown mode.
    /// </summary>
    [Fact]
    public void ModifyFileSystemUnknownMode()
    {
        // Act
        var result = FileTools.ModifyFileSystem("invalid", this.testDir);

        // Assert
        Assert.Contains("Unknown mode", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies FormatError handles UnauthorizedAccessException.
    /// </summary>
    [Fact]
    public void FormatErrorUnauthorizedAccessException()
    {
        // Act
        var result = FileTools.FormatError("creating", "/test/path", new UnauthorizedAccessException());

        // Assert
        Assert.Equal("Access denied: /test/path", result);
    }

    /// <summary>
    /// Verifies FormatError handles IOException.
    /// </summary>
    [Fact]
    public void FormatErrorIOException()
    {
        // Act
        var result = FileTools.FormatError("copying", "/test/path", new IOException("disk full"));

        // Assert
        Assert.Equal("Error copying /test/path: disk full", result);
    }

    /// <summary>
    /// Verifies QueryFileSystem read mode returns file contents.
    /// </summary>
    [Fact]
    public void QueryFileSystemReadReturnsContents()
    {
        // Arrange
        var filePath = Path.Combine(this.testDir, "read.txt");
        const string Content = "Hello, World!";
        File.WriteAllText(filePath, Content);

        // Act
        var result = FileTools.QueryFileSystem("read", filePath);

        // Assert
        Assert.Equal(Content, result);
    }

    /// <summary>
    /// Verifies QueryFileSystem read mode handles non-existent file.
    /// </summary>
    [Fact]
    public void QueryFileSystemReadNonExistentFile()
    {
        // Act
        var result = FileTools.QueryFileSystem("read", Path.Combine(this.testDir, "nonexistent.txt"));

        // Assert
        Assert.Contains("not found", result, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies QueryFileSystem read mode returns empty file message.
    /// </summary>
    [Fact]
    public void QueryFileSystemReadEmptyFile()
    {
        // Arrange
        var filePath = Path.Combine(this.testDir, "empty.txt");
        File.WriteAllText(filePath, string.Empty);

        // Act
        var result = FileTools.QueryFileSystem("read", filePath);

        // Assert
        Assert.Equal("[Empty file]", result);
    }

    /// <summary>
    /// Verifies QueryFileSystem read mode truncates large files.
    /// </summary>
    [Fact]
    public void QueryFileSystemReadTruncatesLargeFile()
    {
        // Arrange
        var filePath = Path.Combine(this.testDir, "large.txt");
        var largeContent = new string('x', 15000);
        File.WriteAllText(filePath, largeContent);

        // Act
        var result = FileTools.QueryFileSystem("read", filePath);

        // Assert
        Assert.Contains("[Truncated", result, StringComparison.Ordinal);
        Assert.Contains("15,000 characters", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies ModifyFileSystem write creates new file.
    /// </summary>
    [Fact]
    public void ModifyFileSystemWriteCreatesFile()
    {
        // Arrange
        var filePath = Path.Combine(this.testDir, "newfile.txt");
        const string Content = "Test content";

        // Act
        var result = FileTools.ModifyFileSystem("write", filePath, Content);

        // Assert
        Assert.Contains("Written", result, StringComparison.Ordinal);
        Assert.Contains("12", result, StringComparison.Ordinal); // 12 characters
        Assert.True(File.Exists(filePath));
        Assert.Equal(Content, File.ReadAllText(filePath));
    }

    /// <summary>
    /// Verifies ModifyFileSystem write overwrites existing file.
    /// </summary>
    [Fact]
    public void ModifyFileSystemWriteOverwritesFile()
    {
        // Arrange
        var filePath = Path.Combine(this.testDir, "existing.txt");
        File.WriteAllText(filePath, "old content");
        const string NewContent = "new content";

        // Act
        var result = FileTools.ModifyFileSystem("write", filePath, NewContent);

        // Assert
        Assert.Contains("Written", result, StringComparison.Ordinal);
        Assert.Equal(NewContent, File.ReadAllText(filePath));
    }

    /// <summary>
    /// Verifies ModifyFileSystem write creates parent directories.
    /// </summary>
    [Fact]
    public void ModifyFileSystemWriteCreatesParentDirectories()
    {
        // Arrange
        var filePath = Path.Combine(this.testDir, "subdir1", "subdir2", "file.txt");
        const string Content = "nested content";

        // Act
        var result = FileTools.ModifyFileSystem("write", filePath, Content);

        // Assert
        Assert.Contains("Written", result, StringComparison.Ordinal);
        Assert.True(File.Exists(filePath));
        Assert.Equal(Content, File.ReadAllText(filePath));
    }

    /// <summary>
    /// Verifies ModifyFileSystem write requires content.
    /// </summary>
    [Fact]
    public void ModifyFileSystemWriteRequiresContent()
    {
        // Act
        var result = FileTools.ModifyFileSystem("write", Path.Combine(this.testDir, "file.txt"));

        // Assert
        Assert.Contains("Content required", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies ModifyFileSystem write handles invalid path.
    /// </summary>
    [Fact]
    public void ModifyFileSystemWriteInvalidPath()
    {
        // Arrange
        var invalidPath = Path.Combine(this.testDir, "invalid\0file.txt");

        // Act
        var result = FileTools.ModifyFileSystem("write", invalidPath, "content");

        // Assert
        Assert.Contains("Error writing", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies QueryFileSystem read handles directory as path (returns not found).
    /// </summary>
    [Fact]
    public void QueryFileSystemReadDirectoryAsFile()
    {
        // Arrange - create a subdirectory
        var subDir = Path.Combine(this.testDir, "subdir_for_read_test");
        _ = Directory.CreateDirectory(subDir);

        // Act - try to read a directory as if it were a file
        var result = FileTools.QueryFileSystem("read", subDir);

        // Assert - File.Exists returns false for directories, so returns "File not found"
        Assert.Contains("File not found", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies path sanitization removes PowerShell encoding flags from paths.
    /// This tests the fix for LLMs appending shell syntax to file paths.
    /// </summary>
    [Fact]
    public void PathSanitizationRemovesEncodingFlag()
    {
        // Arrange - LLM sent path with PowerShell encoding parameter
        var pathWithFlag = Path.Combine(this.testDir, "test.md") + " -Encoding utf8";
        var expectedPath = Path.Combine(this.testDir, "test.md");
        const string Content = "Test content";

        // Act
        var result = FileTools.ModifyFileSystem("write", pathWithFlag, Content);

        // Assert - Should have written to sanitized path
        Assert.Contains("Written", result, StringComparison.Ordinal);
        Assert.True(File.Exists(expectedPath));
        Assert.Equal(Content, File.ReadAllText(expectedPath));
    }

    /// <summary>
    /// Verifies path sanitization removes Force flag.
    /// </summary>
    [Fact]
    public void PathSanitizationRemovesForceFlag()
    {
        // Arrange
        var pathWithFlag = Path.Combine(this.testDir, "test.txt") + " -Force";
        var expectedPath = Path.Combine(this.testDir, "test.txt");

        // Act
        _ = FileTools.ModifyFileSystem("write", pathWithFlag, "content");

        // Assert
        Assert.True(File.Exists(expectedPath));
    }

    /// <summary>
    /// Verifies path sanitization removes redirect operator.
    /// </summary>
    [Fact]
    public void PathSanitizationRemovesRedirectOperator()
    {
        // Arrange
        var pathWithOperator = Path.Combine(this.testDir, "out.txt") + " > other.txt";
        var expectedPath = Path.Combine(this.testDir, "out.txt");

        // Act
        _ = FileTools.ModifyFileSystem("write", pathWithOperator, "content");

        // Assert
        Assert.True(File.Exists(expectedPath));
        Assert.False(File.Exists(Path.Combine(this.testDir, "other.txt")));
    }

    /// <summary>
    /// Verifies path sanitization removes pipe operator.
    /// </summary>
    [Fact]
    public void PathSanitizationRemovesPipeOperator()
    {
        // Arrange
        var pathWithPipe = Path.Combine(this.testDir, "piped.txt") + " | Out-Null";
        var expectedPath = Path.Combine(this.testDir, "piped.txt");

        // Act
        _ = FileTools.ModifyFileSystem("write", pathWithPipe, "content");

        // Assert
        Assert.True(File.Exists(expectedPath));
    }

    /// <summary>
    /// Verifies path sanitization removes surrounding quotes.
    /// </summary>
    [Fact]
    public void PathSanitizationRemovesQuotes()
    {
        // Arrange - LLM sent path with quotes
        var quotedPath = $"\"{Path.Combine(this.testDir, "quoted.txt")}\"";
        var expectedPath = Path.Combine(this.testDir, "quoted.txt");

        // Act
        _ = FileTools.ModifyFileSystem("write", quotedPath, "content");

        // Assert
        Assert.True(File.Exists(expectedPath));
    }

    /// <summary>
    /// Verifies path sanitization also works for QueryFileSystem.
    /// </summary>
    [Fact]
    public void PathSanitizationWorksForQuery()
    {
        // Arrange
        var testFile = Path.Combine(this.testDir, "query-test.txt");
        File.WriteAllText(testFile, "Hello World");
        var pathWithFlag = testFile + " -NoNewline";

        // Act
        var result = FileTools.QueryFileSystem("read", pathWithFlag);

        // Assert
        Assert.Equal("Hello World", result);
    }
}
