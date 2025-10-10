using System.Collections.Generic;
using Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Api.Tests;

/// <summary>
/// Tests for OCR fallback functionality in PdfTextExtractionService
/// </summary>
public class OcrFallbackTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly Mock<ILogger<PdfTextExtractionService>> _loggerMock;
    private readonly Mock<ILogger<TesseractOcrService>> _ocrLoggerMock;
    private readonly IConfiguration _configuration;
    private readonly List<string> _tempFiles = new();

    public OcrFallbackTests(ITestOutputHelper output)
    {
        _output = output;
        _loggerMock = new Mock<ILogger<PdfTextExtractionService>>();
        _ocrLoggerMock = new Mock<ILogger<TesseractOcrService>>();

        // Create real configuration (Moq can't mock extension methods)
        _configuration = new ConfigurationBuilder().Build();
    }

    public void Dispose()
    {
        // Clean up temporary files
        foreach (var file in _tempFiles)
        {
            try
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    private string CreateTempPdfPath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"test_ocr_{Guid.NewGuid()}.pdf");
        _tempFiles.Add(path);
        return path;
    }

    [Fact]
    public async Task ExtractText_FromScannedPdf_TriggersOcrFallback()
    {
        // Arrange
        var scannedPdfPath = CreateTempPdfPath();
        var testText = "This is a scanned document.\nIt should trigger OCR fallback.";

        // Create a "scanned" PDF (text as image)
        CreateScannedPdf.Generate(scannedPdfPath, testText);

        _output.WriteLine($"Created scanned PDF at: {scannedPdfPath}");

        // Create real configuration (Moq can't mock extension methods)
        var configData = new Dictionary<string, string?>
        {
            ["PdfExtraction:Ocr:DefaultLanguage"] = "eng",
            ["PdfExtraction:Ocr:MaxConcurrentOperations"] = "2"
        };
        var ocrConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var ocrService = new TesseractOcrService(_ocrLoggerMock.Object, ocrConfig);

        // Create extraction service WITH OCR
        var service = new PdfTextExtractionService(
            _loggerMock.Object,
            _configuration,
            ocrService);

        // Act
        var result = await service.ExtractTextAsync(scannedPdfPath);

        // Assert
        _output.WriteLine($"Extraction Success: {result.Success}");
        _output.WriteLine($"Used OCR: {result.UsedOcr}");
        _output.WriteLine($"OCR Confidence: {result.OcrConfidence}");
        _output.WriteLine($"Characters extracted: {result.CharacterCount}");
        _output.WriteLine($"Extracted text: {result.ExtractedText}");

        Assert.True(result.Success, "Extraction should succeed");
        Assert.True(result.UsedOcr, "OCR fallback should be triggered for scanned PDF");
        Assert.NotNull(result.OcrConfidence);
        Assert.True(result.OcrConfidence > 0, "OCR confidence should be > 0");
        Assert.True(result.CharacterCount > 0, "Should extract some text via OCR");

        // The OCR should recognize at least some of the text
        // (exact match not guaranteed due to OCR accuracy)
        var extractedLower = result.ExtractedText.ToLower();
        Assert.True(
            extractedLower.Contains("scanned") || extractedLower.Contains("document") || extractedLower.Contains("ocr"),
            "OCR should recognize at least some words from the image");
    }

    [Fact]
    public async Task ExtractText_WithoutOcrService_DoesNotTriggerOcr()
    {
        // Arrange - Create a PDF with no extractable text (empty)
        var emptyPdfPath = CreateTempPdfPath();

        QuestPDF.Settings.License = LicenseType.Community;
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Content().Text(""); // Empty content
            });
        }).GeneratePdf(emptyPdfPath);

        // Create extraction service WITHOUT OCR (null)
        var service = new PdfTextExtractionService(
            _loggerMock.Object,
            _configuration,
            ocrService: null);

        // Act
        var result = await service.ExtractTextAsync(emptyPdfPath);

        // Assert
        _output.WriteLine($"Extraction Success: {result.Success}");
        _output.WriteLine($"Used OCR: {result.UsedOcr}");
        _output.WriteLine($"Characters extracted: {result.CharacterCount}");

        Assert.True(result.Success, "Extraction should succeed");
        Assert.False(result.UsedOcr, "OCR should NOT be used when service is null");
        Assert.Null(result.OcrConfidence);
    }

    [Fact]
    public async Task ExtractText_FromScannedPdf_LogsOcrFallback()
    {
        // Arrange
        var scannedPdfPath = CreateTempPdfPath();
        CreateScannedPdf.Generate(scannedPdfPath, "OCR test document");

        var configData = new Dictionary<string, string?>
        {
            ["PdfExtraction:Ocr:DefaultLanguage"] = "eng",
            ["PdfExtraction:Ocr:MaxConcurrentOperations"] = "2"
        };
        var ocrConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var ocrService = new TesseractOcrService(_ocrLoggerMock.Object, ocrConfig);

        var service = new PdfTextExtractionService(
            _loggerMock.Object,
            _configuration,
            ocrService);

        // Act
        await service.ExtractTextAsync(scannedPdfPath);

        // Assert - Verify OCR fallback was logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Falling back to OCR")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Should log OCR fallback trigger");

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("OCR extraction completed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Should log OCR completion");
    }
}
