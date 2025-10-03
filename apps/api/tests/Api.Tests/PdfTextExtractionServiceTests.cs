using Api.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests;

public class PdfTextExtractionServiceTests
{
    private readonly Mock<ILogger<PdfTextExtractionService>> _loggerMock;
    private readonly PdfTextExtractionService _service;

    public PdfTextExtractionServiceTests()
    {
        _loggerMock = new Mock<ILogger<PdfTextExtractionService>>();
        _service = new PdfTextExtractionService(_loggerMock.Object);
    }

    [Fact]
    public async Task ExtractTextAsync_ReturnsFailure_WhenFilePathIsNull()
    {
        // Act
        var result = await _service.ExtractTextAsync(null!);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("File path is required", result.ErrorMessage);
    }

    [Fact]
    public async Task ExtractTextAsync_ReturnsFailure_WhenFilePathIsEmpty()
    {
        // Act
        var result = await _service.ExtractTextAsync(string.Empty);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("File path is required", result.ErrorMessage);
    }

    [Fact]
    public async Task ExtractTextAsync_ReturnsFailure_WhenFilePathIsWhitespace()
    {
        // Act
        var result = await _service.ExtractTextAsync("   ");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("File path is required", result.ErrorMessage);
    }

    [Fact]
    public async Task ExtractTextAsync_ReturnsFailure_WhenFileDoesNotExist()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".pdf");

        // Act
        var result = await _service.ExtractTextAsync(nonExistentPath);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("File not found", result.ErrorMessage);
    }

    [Fact]
    public void PdfTextExtractionResult_CreateSuccess_SetsPropertiesCorrectly()
    {
        // Act
        var result = PdfTextExtractionResult.CreateSuccess("test text", 5, 100);

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
        Assert.Equal("test text", result.ExtractedText);
        Assert.Equal(5, result.PageCount);
        Assert.Equal(100, result.CharacterCount);
    }

    [Fact]
    public void PdfTextExtractionResult_CreateFailure_SetsPropertiesCorrectly()
    {
        // Act
        var result = PdfTextExtractionResult.CreateFailure("test error");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("test error", result.ErrorMessage);
        Assert.Equal(string.Empty, result.ExtractedText);
        Assert.Equal(0, result.PageCount);
        Assert.Equal(0, result.CharacterCount);
    }
}
