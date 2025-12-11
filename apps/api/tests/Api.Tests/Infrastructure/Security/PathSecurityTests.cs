using Api.Infrastructure.Security;
using FluentAssertions;
using System.Security;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.Infrastructure.Security;

/// <summary>
/// Comprehensive unit tests for PathSecurity utility class (Issue #1819 - #1746).
///
/// Tests all 7 public methods with focus on:
/// - Path traversal attack prevention (OWASP A01:2021)
/// - Filename sanitization against XSS, SQL injection, null bytes
/// - File extension validation
/// - Identifier validation for safe path construction
///
/// <para><b>Production Scenario:</b> Prevents directory traversal attacks that could expose system files or escape storage directories.</para>
/// <para><b>Security Standard:</b> OWASP Top 10 2021 - A01 Broken Access Control</para>
/// <para><b>Acceptance Criteria:</b> Issue #1819 (#1746) - PathSecurity.SanitizeFilename verified</para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class PathSecurityTests
{
    /// <summary>
    /// Tests that ValidatePathIsInDirectory correctly validates paths within allowed directory.
    ///
    /// <para><b>Test Category:</b> Security - Path Traversal Prevention</para>
    /// <para><b>Production Scenario:</b> User uploads PDF - must stay within /storage/pdfs directory</para>
    /// <para><b>Expected Behavior:</b> Returns full path when file is within allowed directory</para>
    /// </summary>
    [Fact]
    public void ValidatePathIsInDirectory_WithValidPath_ReturnsFullPath()
    {
        // Arrange
        var basePath = Path.Combine(Path.GetTempPath(), "test-storage");
        var filename = "document.pdf";

        // Act
        var result = PathSecurity.ValidatePathIsInDirectory(basePath, filename);

        // Assert
        result.Should().StartWith(basePath);
        result.Should().EndWith(filename);
    }

    /// <summary>
    /// Tests that path traversal attack using ../ is blocked.
    ///
    /// <para><b>Security Threat:</b> ../../etc/passwd could expose system files on Linux</para>
    /// <para><b>OWASP:</b> A01:2021 - Broken Access Control</para>
    /// <para><b>Validation:</b> Throws SecurityException before path resolution</para>
    /// </summary>
    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("..\\..\\..\\Windows\\System32\\config\\SAM")]
    [InlineData("documents/../../../etc/passwd")]
    [InlineData("./../../secrets.txt")]
    [InlineData("file..pdf")] // Not a traversal, should be allowed
    public void ValidatePathIsInDirectory_WithPathTraversal_ThrowsSecurityException(string maliciousPath)
    {
        // Arrange
        var basePath = Path.Combine(Path.GetTempPath(), "test-storage");

        // For "file..pdf" (not a traversal), should NOT throw
        if (!maliciousPath.Contains("..") || maliciousPath.Contains("file..pdf"))
        {
            // Act & Assert - Should succeed
            var act = () => PathSecurity.ValidatePathIsInDirectory(basePath, maliciousPath);
            if (maliciousPath == "file..pdf")
            {
                act.Should().NotThrow("consecutive dots in filename are allowed if not path traversal");
            }
            return;
        }

        // Act & Assert - Path traversal should throw
        var action = () => PathSecurity.ValidatePathIsInDirectory(basePath, maliciousPath);
        action.Should().Throw<SecurityException>()
            .WithMessage("*traversal*", "should detect path traversal pattern");
    }

    /// <summary>
    /// Tests that double slash attack (//) is blocked to prevent web server bypass attacks.
    ///
    /// <para><b>Security Threat:</b> // can bypass certain path validators</para>
    /// </summary>
    [Theory]
    [InlineData("//etc/passwd")]
    [InlineData("documents//secrets.txt")]
    [InlineData("\\\\network\\share")]
    public void ValidatePathIsInDirectory_WithDoubleSlash_ThrowsSecurityException(string maliciousPath)
    {
        // Arrange
        var basePath = Path.Combine(Path.GetTempPath(), "test-storage");

        // Act & Assert
        var action = () => PathSecurity.ValidatePathIsInDirectory(basePath, maliciousPath);
        action.Should().Throw<SecurityException>()
            .WithMessage("*traversal*");
    }

    /// <summary>
    /// Tests that empty or null parameters are rejected with clear error messages.
    /// </summary>
    [Theory]
    [InlineData(null, "document.pdf", "basePath")]
    [InlineData("", "document.pdf", "basePath")]
    [InlineData("/valid/path", null, "filename")]
    [InlineData("/valid/path", "", "filename")]
    [InlineData("/valid/path", "   ", "filename")]
    public void ValidatePathIsInDirectory_WithInvalidParameters_ThrowsArgumentException(
        string? basePath, string? filename, string expectedParam)
    {
        // Act & Assert
        var action = () => PathSecurity.ValidatePathIsInDirectory(basePath!, filename!);
        action.Should().Throw<ArgumentException>()
            .WithParameterName(expectedParam);
    }
    /// <summary>
    /// Tests that dangerous filename characters are removed to prevent XSS and filesystem attacks.
    ///
    /// <para><b>Test Category:</b> Security - Filename Sanitization</para>
    /// <para><b>Production Scenario:</b> Malicious user uploads file with XSS payload in filename</para>
    /// <para><b>OWASP:</b> A03:2021 - Injection (XSS)</para>
    /// <para><b>Expected Behavior:</b> Removes &lt;, &gt;, quotes, path separators</para>
    /// </summary>
    [Theory]
    [InlineData("<script>alert('XSS')</script>.pdf", "scriptalert('XSS')script.pdf")] // ' is VALID in filenames
    [InlineData("document<test>.pdf", "documenttest.pdf")]
    [InlineData("file\"with\"quotes.pdf", "filewithquotes.pdf")]
    [InlineData("path/to/file.pdf", "pathtofile.pdf")]
    [InlineData("windows\\path\\file.pdf", "windowspathfile.pdf")]
    public void SanitizeFilename_WithDangerousCharacters_RemovesThem(string malicious, string expected)
    {
        // Act
        var result = PathSecurity.SanitizeFilename(malicious);

        // Assert
        result.Should().Be(expected, "dangerous characters should be removed");
        result.Should().NotContain("<");
        result.Should().NotContain(">");
        result.Should().NotContain("/");
        result.Should().NotContain("\\");
    }

    /// <summary>
    /// Tests SQL injection patterns in filenames are sanitized.
    ///
    /// <para><b>Security Threat:</b> '; DROP TABLE PdfDocuments; --</para>
    /// <para><b>OWASP:</b> A03:2021 - Injection (SQL)</para>
    /// <para><b>Protection:</b> EF Core parameterization prevents SQL injection, but filename sanitization is defense-in-depth</para>
    /// <para><b>Note:</b> Single quotes ('), equals (=), and semicolons (;) are VALID filename characters on Windows.</para>
    /// <para>SQL injection is prevented by EF Core's parameterized queries, not filename sanitization.</para>
    /// <para>Only truly invalid chars (&lt; &gt; : * ? " | / \) are removed.</para>
    /// </summary>
    [Theory]
    [InlineData("'; DROP TABLE PdfDocuments; --.pdf", "'; DROP TABLE PdfDocuments; --.pdf")] // All chars are valid!
    [InlineData("1' OR '1'='1.pdf", "1' OR '1'='1.pdf")] // Only invalid chars removed
    [InlineData("test<injection>.pdf", "testinjection.pdf")] // < > are invalid
    public void SanitizeFilename_WithSqlInjection_Sanitizes(string malicious, string expected)
    {
        // Act
        var result = PathSecurity.SanitizeFilename(malicious);

        // Assert
        result.Should().Be(expected);
        // Verify truly invalid chars are removed
        result.Should().NotContain("<", "angle brackets are invalid");
        result.Should().NotContain(">", "angle brackets are invalid");
    }

    /// <summary>
    /// Tests null byte injection is blocked.
    ///
    /// <para><b>Security Threat:</b> document\0.exe.pdf could bypass extension validation on some systems</para>
    /// <para><b>Expected Behavior:</b> Null bytes removed, .pdf extension preserved</para>
    /// </summary>
    [Fact]
    public void SanitizeFilename_WithNullBytes_RemovesThem()
    {
        // Arrange
        var malicious = "document\0.exe.pdf";

        // Act
        var result = PathSecurity.SanitizeFilename(malicious);

        // Assert
        result.Should().NotContain("\0", "null bytes should be removed");
        result.Should().EndWith(".pdf", "extension should be preserved");
    }

    /// <summary>
    /// Tests that leading/trailing dots and spaces are removed to prevent filesystem issues.
    ///
    /// <para><b>Production Scenario:</b> ".htaccess" or "...pdf" could cause issues on Linux/Windows</para>
    /// <para><b>Expected Behavior:</b> Leading/trailing dots/spaces trimmed, internal dots preserved</para>
    /// </summary>
    [Theory]
    [InlineData(".hidden.pdf", "hidden.pdf")]
    [InlineData("...pdf", "pdf")]
    [InlineData("file.name.pdf", "file.name.pdf")] // Internal dots OK
    [InlineData("  document.pdf  ", "document.pdf")]
    [InlineData(".  .pdf", "pdf")]
    public void SanitizeFilename_WithLeadingTrailingDots_TrimsThem(string input, string expected)
    {
        // Act
        var result = PathSecurity.SanitizeFilename(input);

        // Assert
        result.Should().Be(expected);
    }

    /// <summary>
    /// Tests that very long filenames are truncated to filesystem limit (255 chars).
    ///
    /// <para><b>Production Scenario:</b> Prevents "Filename too long" errors on Windows/Linux</para>
    /// <para><b>Expected Behavior:</b> Truncated to 255 characters max</para>
    /// </summary>
    [Fact]
    public void SanitizeFilename_WithVeryLongName_TruncatesTo255Chars()
    {
        // Arrange
        var longName = new string('a', 300) + ".pdf";

        // Act
        var result = PathSecurity.SanitizeFilename(longName);

        // Assert
        result.Length.Should().BeLessThanOrEqualTo(255, "filesystem limit is 255 characters");
        result.Should().Contain("a", "some original content should remain");
    }

    /// <summary>
    /// Tests that filename with only invalid characters throws exception.
    ///
    /// <para><b>Production Scenario:</b> File named only "/////" or "<<>>" should be rejected</para>
    /// <para><b>Expected Behavior:</b> ArgumentException with clear message</para>
    /// <para><b>Note:</b> Whitespace-only is caught by IsNullOrWhiteSpace check (different error message)</para>
    /// </summary>
    [Theory]
    [InlineData("/////")]
    [InlineData("<<>>")]
    [InlineData("***")]
    [InlineData(".")] // Single dot is trimmed, resulting in empty
    public void SanitizeFilename_WithOnlyInvalidChars_ThrowsArgumentException(string invalid)
    {
        // Act & Assert
        var action = () => PathSecurity.SanitizeFilename(invalid);
        action.Should().Throw<ArgumentException>()
            .Which.Message.Should().ContainAny("only invalid characters", "cannot be empty");
    }

    /// <summary>
    /// Tests that empty or null filename throws exception.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void SanitizeFilename_WithNullOrEmpty_ThrowsArgumentException(string? invalid)
    {
        // Act & Assert
        var action = () => PathSecurity.SanitizeFilename(invalid!);
        action.Should().Throw<ArgumentException>()
            .WithMessage("*cannot be empty*")
            .WithParameterName("filename");
    }
    /// <summary>
    /// Tests that valid file extensions are accepted.
    ///
    /// <para><b>Test Category:</b> Security - Extension Whitelist Validation</para>
    /// <para><b>Production Scenario:</b> Only allow .pdf uploads, block .exe, .sh, .bat</para>
    /// </summary>
    [Theory]
    [InlineData("document.pdf", ".pdf")]
    [InlineData("IMAGE.PNG", ".png", ".jpg")]
    [InlineData("file.PDF", ".pdf")] // Case insensitive
    public void ValidateFileExtension_WithAllowedExtension_DoesNotThrow(string filename, params string[] allowed)
    {
        // Act & Assert
        var action = () => PathSecurity.ValidateFileExtension(filename, allowed);
        action.Should().NotThrow("allowed extension should pass validation");
    }

    /// <summary>
    /// Tests that invalid file extensions are rejected with clear error message.
    ///
    /// <para><b>Security Threat:</b> Uploading .exe, .sh, .bat could lead to code execution</para>
    /// <para><b>Expected Behavior:</b> ArgumentException listing allowed extensions</para>
    /// </summary>
    [Theory]
    [InlineData("malware.exe", new[] { ".pdf" })]
    [InlineData("script.sh", new[] { ".pdf", ".png" })]
    [InlineData("batch.bat", new[] { ".pdf" })]
    [InlineData("document.txt", new[] { ".pdf" })]
    public void ValidateFileExtension_WithDisallowedExtension_ThrowsArgumentException(string filename, string[] allowed)
    {
        // Act & Assert
        var action = () => PathSecurity.ValidateFileExtension(filename, allowed);
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Invalid file extension*");
    }

    /// <summary>
    /// Tests that validation requires at least one allowed extension.
    /// </summary>
    [Fact]
    public void ValidateFileExtension_WithNoAllowedExtensions_ThrowsArgumentException()
    {
        // Act & Assert
        var action = () => PathSecurity.ValidateFileExtension("file.pdf");
        action.Should().Throw<ArgumentException>()
            .WithMessage("*At least one allowed extension*")
            .WithParameterName("allowedExtensions");
    }
    /// <summary>
    /// Tests that generated filenames are unique GUIDs with original extension.
    ///
    /// <para><b>Production Scenario:</b> Prevents filename collisions and overwrites</para>
    /// <para><b>Expected Behavior:</b> GUID (32 hex chars) + original extension (case preserved)</para>
    /// </summary>
    [Theory]
    [InlineData("document.pdf", ".pdf")]
    [InlineData("image.PNG", ".PNG")] // Case is PRESERVED, not converted
    [InlineData("file.tar.gz", ".gz")]
    public void GenerateSafeFilename_WithValidFile_ReturnsGuidWithExtension(string original, string expectedExt)
    {
        // Act
        var result = PathSecurity.GenerateSafeFilename(original);

        // Assert
        result.Should().MatchRegex(@"^[a-f0-9]{32}\.\w+$", "should be GUID + extension");
        result.Should().EndWith(expectedExt, "extension case should be preserved");
        result.Length.Should().BeGreaterThan(32, "should include extension");
    }

    /// <summary>
    /// Tests that file without extension returns GUID only.
    /// </summary>
    [Theory]
    [InlineData("noextension")]
    [InlineData("file_without_dot")]
    public void GenerateSafeFilename_WithoutExtension_ReturnsGuidOnly(string original)
    {
        // Act
        var result = PathSecurity.GenerateSafeFilename(original);

        // Assert
        result.Should().MatchRegex(@"^[a-f0-9]{32}$", "should be GUID without extension");
        result.Length.Should().Be(32, "GUID without dashes is 32 chars");
    }

    /// <summary>
    /// Tests that generated filenames are unique (not deterministic).
    /// </summary>
    [Fact]
    public void GenerateSafeFilename_CalledMultipleTimes_ReturnsUniqueNames()
    {
        // Act
        var name1 = PathSecurity.GenerateSafeFilename("test.pdf");
        var name2 = PathSecurity.GenerateSafeFilename("test.pdf");
        var name3 = PathSecurity.GenerateSafeFilename("test.pdf");

        // Assert
        name1.Should().NotBe(name2);
        name2.Should().NotBe(name3);
        name1.Should().NotBe(name3);
        new[] { name1, name2, name3 }.Should().OnlyHaveUniqueItems();
    }
    /// <summary>
    /// Tests that SafeFileExists returns false for path traversal attempts.
    ///
    /// <para><b>Security Behavior:</b> Catches SecurityException and returns false instead of throwing</para>
    /// </summary>
    [Fact]
    public void SafeFileExists_WithPathTraversal_ReturnsFalse()
    {
        // Arrange
        var basePath = Path.GetTempPath();
        var maliciousPath = "../../../etc/passwd";

        // Act
        var result = PathSecurity.SafeFileExists(basePath, maliciousPath);

        // Assert
        result.Should().BeFalse("path traversal should return false, not throw");
    }

    /// <summary>
    /// Tests that SafeFileExists returns true for existing files within allowed directory.
    /// </summary>
    [Fact]
    public void SafeFileExists_WithValidExistingFile_ReturnsTrue()
    {
        // Arrange
        var basePath = Path.GetTempPath();
        var filename = $"test-{Guid.NewGuid():N}.txt";
        var fullPath = Path.Combine(basePath, filename);
        File.WriteAllText(fullPath, "test");

        try
        {
            // Act
            var result = PathSecurity.SafeFileExists(basePath, filename);

            // Assert
            result.Should().BeTrue("existing file should be detected");
        }
        finally
        {
            File.Delete(fullPath);
        }
    }
    /// <summary>
    /// Tests that SafeDirectoryExists returns false for path traversal attempts.
    /// </summary>
    [Fact]
    public void SafeDirectoryExists_WithPathTraversal_ReturnsFalse()
    {
        // Arrange
        var basePath = Path.GetTempPath();
        var maliciousPath = "../../etc";

        // Act
        var result = PathSecurity.SafeDirectoryExists(basePath, maliciousPath);

        // Assert
        result.Should().BeFalse("path traversal should return false");
    }

    /// <summary>
    /// Tests that SafeDirectoryExists returns true for existing directories.
    /// </summary>
    [Fact]
    public void SafeDirectoryExists_WithValidExistingDirectory_ReturnsTrue()
    {
        // Arrange
        var basePath = Path.GetTempPath();
        var dirName = $"test-dir-{Guid.NewGuid():N}";
        var fullPath = Path.Combine(basePath, dirName);
        Directory.CreateDirectory(fullPath);

        try
        {
            // Act
            var result = PathSecurity.SafeDirectoryExists(basePath, dirName);

            // Assert
            result.Should().BeTrue("existing directory should be detected");
        }
        finally
        {
            Directory.Delete(fullPath);
        }
    }
    /// <summary>
    /// Tests that valid identifiers (alphanumeric, hyphens, underscores) are accepted.
    ///
    /// <para><b>Production Scenario:</b> Game IDs, user IDs used in file paths must be safe</para>
    /// <para><b>Expected Behavior:</b> Allows a-z, A-Z, 0-9, -, _</para>
    /// </summary>
    [Theory]
    [InlineData("game-123")]
    [InlineData("user_456")]
    [InlineData("ABC-def_789")]
    [InlineData("simple")]
    [InlineData("12345")]
    public void ValidateIdentifier_WithValidIdentifier_DoesNotThrow(string identifier)
    {
        // Act & Assert
        var action = () => PathSecurity.ValidateIdentifier(identifier);
        action.Should().NotThrow("valid identifier should pass");
    }

    /// <summary>
    /// Tests that dangerous characters in identifiers are rejected.
    ///
    /// <para><b>Security Threat:</b> ../../../ or path separators could enable directory traversal</para>
    /// </summary>
    [Theory]
    [InlineData("../game")]
    [InlineData("user/admin")]
    [InlineData("game\\windows")]
    [InlineData("id;drop")]
    [InlineData("id<script>")]
    [InlineData("...")]
    public void ValidateIdentifier_WithDangerousChars_ThrowsArgumentException(string dangerous)
    {
        // Act & Assert
        var action = () => PathSecurity.ValidateIdentifier(dangerous);
        action.Should().Throw<ArgumentException>()
            .WithMessage("*invalid characters*");
    }

    /// <summary>
    /// Tests that identifiers consisting only of dots are rejected.
    /// <para><b>Note:</b> Dots fail regex validation first, so error message is about "invalid characters"</para>
    /// </summary>
    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("...")]
    public void ValidateIdentifier_WithOnlyDots_ThrowsArgumentException(string dots)
    {
        // Act & Assert
        var action = () => PathSecurity.ValidateIdentifier(dots);
        action.Should().Throw<ArgumentException>()
            .WithMessage("*invalid characters*"); // Regex fails before "only dots" check
    }
}
