using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.UserLibrary.Domain;

/// <summary>
/// Unit tests for CustomPdfMetadata value object.
/// </summary>
public class CustomPdfMetadataTests
{
    [Fact]
    public void Create_WithValidParameters_ShouldCreateMetadata()
    {
        // Act
        var metadata = CustomPdfMetadata.Create(
            url: "https://storage.example.com/pdfs/rulebook.pdf",
            fileSizeBytes: 5_000_000,
            originalFileName: "rulebook.pdf"
        );

        // Assert
        metadata.Should().NotBeNull();
        metadata.Url.Should().Be("https://storage.example.com/pdfs/rulebook.pdf");
        metadata.FileSizeBytes.Should().Be(5_000_000);
        metadata.OriginalFileName.Should().Be("rulebook.pdf");
        metadata.UploadedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_WithInvalidUrl_ShouldThrowArgumentException(string? url)
    {
        // Act & Assert
        var act = () => CustomPdfMetadata.Create(
            url: url!,
            fileSizeBytes: 1000,
            originalFileName: "test.pdf"
        );

        act.Should().Throw<ArgumentException>()
            .WithMessage("*URL cannot be empty*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    [InlineData(101_000_000)] // > 100 MB
    public void Create_WithInvalidFileSize_ShouldThrowArgumentOutOfRangeException(long fileSize)
    {
        // Act & Assert
        var act = () => CustomPdfMetadata.Create(
            url: "https://example.com/pdf",
            fileSizeBytes: fileSize,
            originalFileName: "test.pdf"
        );

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*File size must be between*");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_WithInvalidFileName_ShouldThrowArgumentException(string? fileName)
    {
        // Act & Assert
        var act = () => CustomPdfMetadata.Create(
            url: "https://example.com/pdf",
            fileSizeBytes: 1000,
            originalFileName: fileName!
        );

        act.Should().Throw<ArgumentException>()
            .WithMessage("*file name cannot be empty*");
    }

    [Fact]
    public void GetFormattedFileSize_WithBytesValue_ShouldReturnBytes()
    {
        // Arrange
        var metadata = CustomPdfMetadata.Create("https://test.com", 512, "test.pdf");

        // Act
        var formatted = metadata.GetFormattedFileSize();

        // Assert
        formatted.Should().Be("512 B");
    }

    [Fact]
    public void GetFormattedFileSize_WithKilobytesValue_ShouldReturnKB()
    {
        // Arrange
        var metadata = CustomPdfMetadata.Create("https://test.com", 50_000, "test.pdf");

        // Act
        var formatted = metadata.GetFormattedFileSize();

        // Assert
        formatted.Should().Contain("KB");
    }

    [Fact]
    public void GetFormattedFileSize_WithMegabytesValue_ShouldReturnMB()
    {
        // Arrange
        var metadata = CustomPdfMetadata.Create("https://test.com", 5_000_000, "test.pdf");

        // Act
        var formatted = metadata.GetFormattedFileSize();

        // Assert
        formatted.Should().Contain("MB");
    }

    [Fact]
    public void Equality_WithSameValues_ShouldBeEqual()
    {
        // Arrange
        var uploadedAt = DateTime.UtcNow;
        var metadata1 = CustomPdfMetadata.CreateWithTimestamp("url", uploadedAt, 1000, "file.pdf");
        var metadata2 = CustomPdfMetadata.CreateWithTimestamp("url", uploadedAt, 1000, "file.pdf");

        // Assert
        metadata1.Should().Be(metadata2);
        (metadata1 == metadata2).Should().BeTrue();
    }

    [Fact]
    public void Equality_WithDifferentValues_ShouldNotBeEqual()
    {
        // Arrange
        var metadata1 = CustomPdfMetadata.Create("url1", 1000, "file.pdf");
        var metadata2 = CustomPdfMetadata.Create("url2", 1000, "file.pdf");

        // Assert
        metadata1.Should().NotBe(metadata2);
        (metadata1 == metadata2).Should().BeFalse();
    }
}
