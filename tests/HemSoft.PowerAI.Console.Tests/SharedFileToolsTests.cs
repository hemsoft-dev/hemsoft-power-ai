// <copyright file="SharedFileToolsTests.cs" company="HemSoft">
// Copyright © 2025 HemSoft
// </copyright>

namespace HemSoft.PowerAI.Console.Tests;

using System.Globalization;

using HemSoft.PowerAI.Common.Tools;

/// <summary>
/// Unit tests for <see cref="FileTools"/> in the Shared project.
/// </summary>
public sealed class SharedFileToolsTests : IDisposable
{
    private readonly string testDir;

    /// <summary>
    /// Initializes a new instance of the <see cref="SharedFileToolsTests"/> class.
    /// </summary>
    public SharedFileToolsTests()
    {
        this.testDir = Path.Combine(Path.GetTempPath(), $"SharedFileToolsTests_{Guid.NewGuid():N}");
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
    /// Verifies QueryFileSystem list mode is case insensitive for mode.
    /// </summary>
    [Fact]
    public void QueryFileSystemListModeIsCaseInsensitive()
    {
        // Arrange
        File.WriteAllText(Path.Combine(this.testDir, "test.txt"), "content");

        // Act
        var result = FileTools.QueryFileSystem("LIST", this.testDir);

        // Assert
        Assert.Contains("test.txt", result, StringComparison.Ordinal);
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
    /// Verifies QueryFileSystem returns error for null mode.
    /// </summary>
    [Fact]
    public void QueryFileSystemNullMode()
    {
        // Act
        var result = FileTools.QueryFileSystem(null!, this.testDir);

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
    /// Verifies ModifyFileSystem mkdir is case insensitive.
    /// </summary>
    [Fact]
    public void ModifyFileSystemMkdirIsCaseInsensitive()
    {
        // Arrange
        var newFolder = Path.Combine(this.testDir, "anotherfolder");

        // Act
        var result = FileTools.ModifyFileSystem("MKDIR", newFolder);

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
    /// Verifies ModifyFileSystem move handles destination exists error.
    /// </summary>
    [Fact]
    public void ModifyFileSystemMoveDestinationExists()
    {
        // Arrange
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
    /// Verifies ModifyFileSystem copy overwrites existing file.
    /// </summary>
    [Fact]
    public void ModifyFileSystemCopyOverwritesExisting()
    {
        // Arrange
        var source = Path.Combine(this.testDir, "source.txt");
        var dest = Path.Combine(this.testDir, "existing.txt");
        File.WriteAllText(source, "new content");
        File.WriteAllText(dest, "old content");

        // Act
        var result = FileTools.ModifyFileSystem("copy", source, dest);

        // Assert
        Assert.Contains("Copied file", result, StringComparison.Ordinal);
        Assert.Equal("new content", File.ReadAllText(dest));
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
    /// Verifies ModifyFileSystem copy handles invalid destination.
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
        Assert.Contains("12", result, StringComparison.Ordinal);
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
    /// Verifies ModifyFileSystem write unescapes newlines.
    /// </summary>
    [Fact]
    public void ModifyFileSystemWriteUnescapesNewlines()
    {
        // Arrange
        var filePath = Path.Combine(this.testDir, "escaped.txt");
        const string Content = "line1\\nline2\\tindented";

        // Act
        _ = FileTools.ModifyFileSystem("write", filePath, Content);

        // Assert
        var actual = File.ReadAllText(filePath);
        Assert.Contains("\n", actual, StringComparison.Ordinal);
        Assert.Contains("\t", actual, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies ModifyFileSystem write unescapes carriage returns.
    /// </summary>
    [Fact]
    public void ModifyFileSystemWriteUnescapesCarriageReturns()
    {
        // Arrange
        var filePath = Path.Combine(this.testDir, "crlf.txt");
        const string Content = "line1\\r\\nline2";

        // Act
        _ = FileTools.ModifyFileSystem("write", filePath, Content);

        // Assert
        var actual = File.ReadAllText(filePath);
        Assert.Contains("\r\n", actual, StringComparison.Ordinal);
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
    /// Verifies ModifyFileSystem returns error for null mode.
    /// </summary>
    [Fact]
    public void ModifyFileSystemNullMode()
    {
        // Act
        var result = FileTools.ModifyFileSystem(null!, this.testDir);

        // Assert
        Assert.Contains("Unknown mode", result, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies list mode limits results to 100 files.
    /// </summary>
    [Fact]
    public void QueryFileSystemListLimitsTo100Files()
    {
        // Arrange - create 105 files
        for (var i = 0; i < 105; i++)
        {
            var fileName = i.ToString("D3", CultureInfo.InvariantCulture);
            File.WriteAllText(Path.Combine(this.testDir, $"file{fileName}.txt"), "content");
        }

        // Act
        var result = FileTools.QueryFileSystem("list", this.testDir);
        var fileCount = result.Split(',').Length;

        // Assert
        var msg = string.Create(CultureInfo.InvariantCulture, $"Expected at most 100 files, but got {fileCount}");
        Assert.True(fileCount <= 100, msg);
    }
}
