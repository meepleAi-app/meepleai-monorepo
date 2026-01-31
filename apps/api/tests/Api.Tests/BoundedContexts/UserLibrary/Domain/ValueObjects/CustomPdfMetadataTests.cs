using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Domain.ValueObjects;

/// <summary>
/// Tests for the CustomPdfMetadata value object.
/// Issue #3025: Backend 90% Coverage Target - Phase 20
/// </summary>
[Trait("Category", "Unit")]
public sealed class CustomPdfMetadataTests
{
    #region Create Factory Tests

    [Fact]
    public void Create_WithValidParameters_CreatesMetadata()
    {
        // Act
        var metadata = CustomPdfMetadata.Create(
            url: "https://storage.example.com/rulebooks/catan.pdf",
            fileSizeBytes: 1024 * 1024, // 1 MB
            originalFileName: "Catan_Rulebook.pdf");

        // Assert
        metadata.Url.Should().Be("https://storage.example.com/rulebooks/catan.pdf");
        metadata.FileSizeBytes.Should().Be(1024 * 1024);
        metadata.OriginalFileName.Should().Be("Catan_Rulebook.pdf");
        metadata.UploadedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_WithEmptyUrl_ThrowsArgumentException()
    {
        // Act
        var action = () => CustomPdfMetadata.Create(
            url: "",
            fileSizeBytes: 1024,
            originalFileName: "test.pdf");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("url")
            .WithMessage("*URL cannot be empty*");
    }

    [Fact]
    public void Create_WithHttpUrl_ThrowsArgumentException()
    {
        // Act - HTTP is not allowed, only HTTPS
        var action = () => CustomPdfMetadata.Create(
            url: "http://storage.example.com/test.pdf",
            fileSizeBytes: 1024,
            originalFileName: "test.pdf");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*URL must use HTTPS scheme*");
    }

    [Fact]
    public void Create_WithLocalhostUrl_ThrowsArgumentException()
    {
        // Act - localhost/loopback not allowed (SSRF protection)
        var action = () => CustomPdfMetadata.Create(
            url: "https://localhost/test.pdf",
            fileSizeBytes: 1024,
            originalFileName: "test.pdf");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*URL cannot reference localhost*");
    }

    [Fact]
    public void Create_WithPrivateIPUrl_ThrowsArgumentException()
    {
        // Act - private IP not allowed (SSRF protection)
        var action = () => CustomPdfMetadata.Create(
            url: "https://192.168.1.1/test.pdf",
            fileSizeBytes: 1024,
            originalFileName: "test.pdf");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*URL cannot reference private*");
    }

    [Theory]
    [InlineData("https://10.0.0.1/test.pdf")]
    [InlineData("https://172.16.0.1/test.pdf")]
    public void Create_WithInternalIPUrl_ThrowsArgumentException(string internalUrl)
    {
        // Act
        var action = () => CustomPdfMetadata.Create(
            url: internalUrl,
            fileSizeBytes: 1024,
            originalFileName: "test.pdf");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*URL cannot reference private*");
    }

    [Fact]
    public void Create_WithInvalidUrl_ThrowsArgumentException()
    {
        // Act
        var action = () => CustomPdfMetadata.Create(
            url: "not-a-url",
            fileSizeBytes: 1024,
            originalFileName: "test.pdf");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*URL must be a valid absolute URL*");
    }

    [Fact]
    public void Create_WithFileSizeZero_ThrowsArgumentOutOfRangeException()
    {
        // Act
        var action = () => CustomPdfMetadata.Create(
            url: "https://storage.example.com/test.pdf",
            fileSizeBytes: 0,
            originalFileName: "test.pdf");

        // Assert
        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("fileSizeBytes");
    }

    [Fact]
    public void Create_WithFileSizeExceeding100MB_ThrowsArgumentOutOfRangeException()
    {
        // Act
        var action = () => CustomPdfMetadata.Create(
            url: "https://storage.example.com/test.pdf",
            fileSizeBytes: 100_000_001, // 100 MB + 1 byte
            originalFileName: "test.pdf");

        // Assert
        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("fileSizeBytes");
    }

    [Fact]
    public void Create_WithMaxFileSize_Succeeds()
    {
        // Act
        var metadata = CustomPdfMetadata.Create(
            url: "https://storage.example.com/test.pdf",
            fileSizeBytes: 100_000_000, // Exactly 100 MB
            originalFileName: "test.pdf");

        // Assert
        metadata.FileSizeBytes.Should().Be(100_000_000);
    }

    [Fact]
    public void Create_WithEmptyFileName_ThrowsArgumentException()
    {
        // Act
        var action = () => CustomPdfMetadata.Create(
            url: "https://storage.example.com/test.pdf",
            fileSizeBytes: 1024,
            originalFileName: "");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("originalFileName")
            .WithMessage("*Original file name cannot be empty*");
    }

    [Fact]
    public void Create_WithFileNameExceeding255Characters_ThrowsArgumentException()
    {
        // Arrange
        var longFileName = new string('a', 252) + ".pdf"; // 256 chars

        // Act
        var action = () => CustomPdfMetadata.Create(
            url: "https://storage.example.com/test.pdf",
            fileSizeBytes: 1024,
            originalFileName: longFileName);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("originalFileName")
            .WithMessage("*File name cannot exceed 255 characters*");
    }

    [Fact]
    public void Create_TrimsUrlAndFileName()
    {
        // Act
        var metadata = CustomPdfMetadata.Create(
            url: "  https://storage.example.com/test.pdf  ",
            fileSizeBytes: 1024,
            originalFileName: "  test.pdf  ");

        // Assert
        metadata.Url.Should().Be("https://storage.example.com/test.pdf");
        metadata.OriginalFileName.Should().Be("test.pdf");
    }

    #endregion

    #region CreateWithTimestamp Tests

    [Fact]
    public void CreateWithTimestamp_UsesProvidedTimestamp()
    {
        // Arrange
        var timestamp = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc);

        // Act
        var metadata = CustomPdfMetadata.CreateWithTimestamp(
            url: "https://storage.example.com/test.pdf",
            uploadedAt: timestamp,
            fileSizeBytes: 1024,
            originalFileName: "test.pdf");

        // Assert
        metadata.UploadedAt.Should().Be(timestamp);
    }

    #endregion

    #region GetFormattedFileSize Tests

    [Fact]
    public void GetFormattedFileSize_WithBytes_ReturnsBytes()
    {
        // Arrange
        var metadata = CustomPdfMetadata.Create(
            url: "https://storage.example.com/test.pdf",
            fileSizeBytes: 512,
            originalFileName: "test.pdf");

        // Act
        var formatted = metadata.GetFormattedFileSize();

        // Assert
        formatted.Should().Be("512 B");
    }

    [Fact]
    public void GetFormattedFileSize_WithKilobytes_ReturnsKB()
    {
        // Arrange
        var metadata = CustomPdfMetadata.Create(
            url: "https://storage.example.com/test.pdf",
            fileSizeBytes: 5 * 1024, // 5 KB
            originalFileName: "test.pdf");

        // Act
        var formatted = metadata.GetFormattedFileSize();

        // Assert
        formatted.Should().Be("5 KB");
    }

    [Fact]
    public void GetFormattedFileSize_WithMegabytes_ReturnsMB()
    {
        // Arrange
        var metadata = CustomPdfMetadata.Create(
            url: "https://storage.example.com/test.pdf",
            fileSizeBytes: 5 * 1024 * 1024, // 5 MB
            originalFileName: "test.pdf");

        // Act
        var formatted = metadata.GetFormattedFileSize();

        // Assert
        formatted.Should().Contain("MB");
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_WithSameValues_ReturnsTrue()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;
        var metadata1 = CustomPdfMetadata.CreateWithTimestamp(
            "https://storage.example.com/test.pdf", timestamp, 1024, "test.pdf");
        var metadata2 = CustomPdfMetadata.CreateWithTimestamp(
            "https://storage.example.com/test.pdf", timestamp, 1024, "test.pdf");

        // Assert
        metadata1.Should().Be(metadata2);
    }

    [Fact]
    public void Equals_WithDifferentUrl_ReturnsFalse()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;
        var metadata1 = CustomPdfMetadata.CreateWithTimestamp(
            "https://storage.example.com/test1.pdf", timestamp, 1024, "test.pdf");
        var metadata2 = CustomPdfMetadata.CreateWithTimestamp(
            "https://storage.example.com/test2.pdf", timestamp, 1024, "test.pdf");

        // Assert
        metadata1.Should().NotBe(metadata2);
    }

    [Fact]
    public void GetHashCode_WithSameValues_ReturnsSameHash()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;
        var metadata1 = CustomPdfMetadata.CreateWithTimestamp(
            "https://storage.example.com/test.pdf", timestamp, 1024, "test.pdf");
        var metadata2 = CustomPdfMetadata.CreateWithTimestamp(
            "https://storage.example.com/test.pdf", timestamp, 1024, "test.pdf");

        // Assert
        metadata1.GetHashCode().Should().Be(metadata2.GetHashCode());
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        // Arrange
        var metadata = CustomPdfMetadata.Create(
            url: "https://storage.example.com/test.pdf",
            fileSizeBytes: 1024,
            originalFileName: "Catan_Rules.pdf");

        // Act
        var result = metadata.ToString();

        // Assert
        result.Should().Contain("File=Catan_Rules.pdf");
        result.Should().Contain("KB");
    }

    #endregion
}
