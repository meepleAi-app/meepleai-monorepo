using System.Security;
using Api.Infrastructure.Security;
using Xunit;

namespace Api.Tests.Infrastructure.Security;

public class PathSecurityTests
{
    private readonly string _testBaseDirectory;

    public PathSecurityTests()
    {
        // Use a test directory for all tests
        _testBaseDirectory = Path.Combine(Path.GetTempPath(), "PathSecurityTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testBaseDirectory);
    }

    #region ValidatePathIsInDirectory Tests

    [Fact]
    public void ValidatePathIsInDirectory_WithValidFilename_ReturnsFullPath()
    {
        // Arrange
        var filename = "document.pdf";

        // Act
        var result = PathSecurity.ValidatePathIsInDirectory(_testBaseDirectory, filename);

        // Assert
        Assert.EndsWith("document.pdf", result);
        Assert.StartsWith(_testBaseDirectory, result);
    }

    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData("../../../etc/passwd")]
    [InlineData("..\\..\\..\\Windows\\System32\\config\\SAM")]
    [InlineData("/etc/shadow")]
    [InlineData("C:\\Windows\\System32\\config\\SAM")]
    [InlineData("....//....//etc/passwd")] // Double encoding
    public void ValidatePathIsInDirectory_WithTraversalAttempt_ThrowsSecurityException(string maliciousFilename)
    {
        // Act & Assert
        Assert.Throws<SecurityException>(() =>
            PathSecurity.ValidatePathIsInDirectory(_testBaseDirectory, maliciousFilename)
        );
    }

    [Fact]
    public void ValidatePathIsInDirectory_WithSiblingDirectoryBypass_ThrowsSecurityException()
    {
        // Arrange
        // Create a base directory like "/tmp/games"
        var gamesDir = Path.Combine(Path.GetTempPath(), "PathSecurityTests_SiblingBypass", "games");
        Directory.CreateDirectory(gamesDir);

        // Create a sibling directory like "/tmp/games_backup"
        var siblingDir = Path.Combine(Path.GetTempPath(), "PathSecurityTests_SiblingBypass", "games_backup");
        Directory.CreateDirectory(siblingDir);

        try
        {
            // Act & Assert
            // This should throw because "../games_backup/secret.json" resolves to the sibling directory
            // which shares the same prefix but is not a subdirectory of gamesDir
            Assert.Throws<SecurityException>(() =>
                PathSecurity.ValidatePathIsInDirectory(gamesDir, "../games_backup/secret.json")
            );
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(siblingDir))
                Directory.Delete(siblingDir, true);
            if (Directory.Exists(gamesDir))
                Directory.Delete(gamesDir, true);
            var parentDir = Path.Combine(Path.GetTempPath(), "PathSecurityTests_SiblingBypass");
            if (Directory.Exists(parentDir))
                Directory.Delete(parentDir, true);
        }
    }

    [Fact]
    public void ValidatePathIsInDirectory_WithEmptyFilename_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            PathSecurity.ValidatePathIsInDirectory(_testBaseDirectory, "")
        );
    }

    [Fact]
    public void ValidatePathIsInDirectory_WithNullFilename_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            PathSecurity.ValidatePathIsInDirectory(_testBaseDirectory, null!)
        );
    }

    [Fact]
    public void ValidatePathIsInDirectory_WithEmptyBasePath_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            PathSecurity.ValidatePathIsInDirectory("", "document.pdf")
        );
    }

    #endregion

    #region SanitizeFilename Tests

    [Theory]
    [InlineData("document.pdf", "document.pdf")]
    [InlineData("my-file_2024.txt", "my-file_2024.txt")]
    [InlineData("test (copy).pdf", "test (copy).pdf")]
    public void SanitizeFilename_WithValidFilename_ReturnsSanitized(string input, string expected)
    {
        // Act
        var result = PathSecurity.SanitizeFilename(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("../../../etc/passwd", "..etcpasswd")]
    [InlineData("file<>name.txt", "filename.txt")]
    [InlineData("file:name|test.pdf", "filenametest.pdf")]
    [InlineData("test?.pdf", "test.pdf")]
    [InlineData("file*name.txt", "filename.txt")]
    public void SanitizeFilename_WithDangerousCharacters_RemovesThem(string input, string expected)
    {
        // Act
        var result = PathSecurity.SanitizeFilename(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("  .test.pdf  ", "test.pdf")]
    [InlineData("...file.txt", "file.txt")]
    [InlineData(".hidden.txt", "hidden.txt")]
    public void SanitizeFilename_WithLeadingTrailingDots_RemovesThem(string input, string expected)
    {
        // Act
        var result = PathSecurity.SanitizeFilename(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void SanitizeFilename_WithOnlyInvalidCharacters_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            PathSecurity.SanitizeFilename("///:::***")
        );
    }

    [Fact]
    public void SanitizeFilename_WithVeryLongFilename_TruncatesTo255Characters()
    {
        // Arrange
        var longFilename = new string('a', 300) + ".pdf";

        // Act
        var result = PathSecurity.SanitizeFilename(longFilename);

        // Assert
        Assert.True(result.Length <= 255);
    }

    #endregion

    #region ValidateFileExtension Tests

    [Theory]
    [InlineData("test.pdf", ".pdf", ".png")] // Allowed
    [InlineData("test.PDF", ".pdf", ".png")] // Case insensitive
    [InlineData("document.png", ".pdf", ".png")] // Allowed
    public void ValidateFileExtension_WithAllowedExtension_DoesNotThrow(
        string filename, params string[] allowed)
    {
        // Act & Assert (no exception)
        PathSecurity.ValidateFileExtension(filename, allowed);
    }

    [Theory]
    [InlineData("test.exe", ".pdf", ".png")]
    [InlineData("test.sh", ".pdf", ".png")]
    [InlineData("malware.bat", ".pdf", ".png")]
    public void ValidateFileExtension_WithForbiddenExtension_ThrowsArgumentException(
        string filename, params string[] allowed)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            PathSecurity.ValidateFileExtension(filename, allowed)
        );
    }

    [Fact]
    public void ValidateFileExtension_WithNoAllowedExtensions_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            PathSecurity.ValidateFileExtension("test.pdf")
        );
    }

    #endregion

    #region GenerateSafeFilename Tests

    [Fact]
    public void GenerateSafeFilename_WithValidFilename_ReturnsGuidWithExtension()
    {
        // Arrange
        var originalFilename = "document.pdf";

        // Act
        var result = PathSecurity.GenerateSafeFilename(originalFilename);

        // Assert
        Assert.EndsWith(".pdf", result);
        Assert.Contains("-", result); // GUID format (though normalized to 'N')
        Assert.NotEqual(originalFilename, result);
    }

    [Fact]
    public void GenerateSafeFilename_WithDangerousFilename_ReturnsSafeFilename()
    {
        // Arrange
        var originalFilename = "../../etc/passwd.txt";

        // Act
        var result = PathSecurity.GenerateSafeFilename(originalFilename);

        // Assert
        Assert.EndsWith(".txt", result);
        Assert.DoesNotContain("..", result);
        Assert.DoesNotContain("/", result);
    }

    #endregion

    #region SafeFileExists Tests

    [Fact]
    public void SafeFileExists_WithExistingFile_ReturnsTrue()
    {
        // Arrange
        var filename = "test.txt";
        var fullPath = Path.Combine(_testBaseDirectory, filename);
        File.WriteAllText(fullPath, "test content");

        // Act
        var result = PathSecurity.SafeFileExists(_testBaseDirectory, filename);

        // Assert
        Assert.True(result);

        // Cleanup
        File.Delete(fullPath);
    }

    [Fact]
    public void SafeFileExists_WithNonExistentFile_ReturnsFalse()
    {
        // Arrange
        var filename = "nonexistent.txt";

        // Act
        var result = PathSecurity.SafeFileExists(_testBaseDirectory, filename);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData("../../../test.txt")]
    public void SafeFileExists_WithTraversalAttempt_ReturnsFalse(string maliciousFilename)
    {
        // Act
        var result = PathSecurity.SafeFileExists(_testBaseDirectory, maliciousFilename);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region SafeDirectoryExists Tests

    [Fact]
    public void SafeDirectoryExists_WithExistingDirectory_ReturnsTrue()
    {
        // Arrange
        var dirname = "testdir";
        var fullPath = Path.Combine(_testBaseDirectory, dirname);
        Directory.CreateDirectory(fullPath);

        // Act
        var result = PathSecurity.SafeDirectoryExists(_testBaseDirectory, dirname);

        // Assert
        Assert.True(result);

        // Cleanup
        Directory.Delete(fullPath);
    }

    [Fact]
    public void SafeDirectoryExists_WithNonExistentDirectory_ReturnsFalse()
    {
        // Arrange
        var dirname = "nonexistent";

        // Act
        var result = PathSecurity.SafeDirectoryExists(_testBaseDirectory, dirname);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("../../etc")]
    [InlineData("../../../test")]
    public void SafeDirectoryExists_WithTraversalAttempt_ReturnsFalse(string maliciousDir)
    {
        // Act
        var result = PathSecurity.SafeDirectoryExists(_testBaseDirectory, maliciousDir);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region ValidateIdentifier Tests

    [Theory]
    [InlineData("abc123")]
    [InlineData("test-id")]
    [InlineData("my_identifier")]
    [InlineData("ID_123-ABC")]
    [InlineData("a1b2c3")]
    public void ValidateIdentifier_WithValidIdentifier_DoesNotThrow(string identifier)
    {
        // Act & Assert (no exception)
        PathSecurity.ValidateIdentifier(identifier);
    }

    [Theory]
    [InlineData("../etc/passwd")]
    [InlineData("test/path")]
    [InlineData("test\\path")]
    [InlineData("test.file")]
    [InlineData("test file")] // space
    [InlineData("test@id")]
    [InlineData("test#id")]
    [InlineData("..")]
    [InlineData("...")]
    public void ValidateIdentifier_WithInvalidCharacters_ThrowsArgumentException(string identifier)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            PathSecurity.ValidateIdentifier(identifier)
        );
    }

    [Fact]
    public void ValidateIdentifier_WithEmptyString_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            PathSecurity.ValidateIdentifier("")
        );
    }

    [Fact]
    public void ValidateIdentifier_WithNull_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            PathSecurity.ValidateIdentifier(null!)
        );
    }

    #endregion
}
