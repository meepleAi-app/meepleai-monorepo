using Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Api.Tests;

/// <summary>
/// Tests for PDF text extraction on real rulebook PDFs from data/ directory
/// Used to measure baseline extraction quality for PDF-02 issue
/// </summary>
public class RealPdfExtractionTests
{
    private readonly ITestOutputHelper _output;
    private readonly Mock<ILogger<PdfTextExtractionService>> _loggerMock;
    private readonly IConfiguration _configuration;
    private readonly PdfTextExtractionService _service;

    public RealPdfExtractionTests(ITestOutputHelper output)
    {
        _output = output;
        _loggerMock = new Mock<ILogger<PdfTextExtractionService>>();

        // Create real configuration (Moq can't mock extension methods)
        _configuration = new ConfigurationBuilder().Build();

        _service = new PdfTextExtractionService(_loggerMock.Object, _configuration, ocrService: null);
    }

    private string GetTestPdfPath(string filename)
    {
        // From test bin directory (e.g., tests/Api.Tests/bin/Debug/net8.0/),
        // navigate up 7 levels to repo root, then to data/
        var testDir = Directory.GetCurrentDirectory();

        // bin -> Debug -> net8.0 -> Api.Tests -> tests -> api -> apps -> repo root (7 levels)
        var repoRoot = Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", "..", "..", "..", ".."));
        var pdfPath = Path.Combine(repoRoot, "data", filename);

        return pdfPath;
    }

    [Fact]
    public async Task ExtractText_FromHarmoniesRules_ShowsBaselineQuality()
    {
        // Arrange
        var pdfPath = GetTestPdfPath("Test-EN-LIBELLUD_HARMONIES_RULES_EN.pdf");

        if (!File.Exists(pdfPath))
        {
            _output.WriteLine($"SKIP: Test PDF not found at {pdfPath}");
            return;
        }

        // Act
        var result = await _service.ExtractTextAsync(pdfPath);

        // Assert & Report
        Assert.True(result.Success, $"Extraction failed: {result.ErrorMessage}");

        _output.WriteLine("=== HARMONIES RULES EXTRACTION RESULTS ===");
        _output.WriteLine($"Success: {result.Success}");
        _output.WriteLine($"Pages: {result.PageCount}");
        _output.WriteLine($"Characters: {result.CharacterCount}");
        _output.WriteLine($"Chars/Page: {(result.PageCount > 0 ? result.CharacterCount / result.PageCount : 0)}");
        _output.WriteLine("");
        _output.WriteLine("First 500 chars of extracted text:");
        _output.WriteLine(result.ExtractedText.Length > 500
            ? result.ExtractedText.Substring(0, 500)
            : result.ExtractedText);
        _output.WriteLine("");
        _output.WriteLine("Last 500 chars of extracted text:");
        if (result.ExtractedText.Length > 500)
        {
            _output.WriteLine(result.ExtractedText.Substring(result.ExtractedText.Length - 500));
        }

        // Baseline quality check - should extract meaningful text
        Assert.True(result.CharacterCount > 1000,
            $"Expected >1000 chars but got {result.CharacterCount}. Likely needs OCR.");
    }

    [Fact]
    public async Task ExtractText_FromLorenzoRules_ShowsBaselineQuality()
    {
        // Arrange
        var pdfPath = GetTestPdfPath("Test-EN-LorenzoRules.pdf");

        if (!File.Exists(pdfPath))
        {
            _output.WriteLine($"SKIP: Test PDF not found at {pdfPath}");
            return;
        }

        // Act
        var result = await _service.ExtractTextAsync(pdfPath);

        // Assert & Report
        Assert.True(result.Success, $"Extraction failed: {result.ErrorMessage}");

        _output.WriteLine("=== LORENZO RULES EXTRACTION RESULTS ===");
        _output.WriteLine($"Success: {result.Success}");
        _output.WriteLine($"Pages: {result.PageCount}");
        _output.WriteLine($"Characters: {result.CharacterCount}");
        _output.WriteLine($"Chars/Page: {(result.PageCount > 0 ? result.CharacterCount / result.PageCount : 0)}");
        _output.WriteLine("");
        _output.WriteLine("First 500 chars of extracted text:");
        _output.WriteLine(result.ExtractedText.Length > 500
            ? result.ExtractedText.Substring(0, 500)
            : result.ExtractedText);
        _output.WriteLine("");
        _output.WriteLine("Last 500 chars of extracted text:");
        if (result.ExtractedText.Length > 500)
        {
            _output.WriteLine(result.ExtractedText.Substring(result.ExtractedText.Length - 500));
        }

        // Baseline quality check - should extract meaningful text
        Assert.True(result.CharacterCount > 1000,
            $"Expected >1000 chars but got {result.CharacterCount}. Likely needs OCR.");
    }

    [Fact]
    public async Task CompareExtractionQuality_AcrossTestPdfs()
    {
        // Test both PDFs and compare extraction quality
        var pdfs = new[]
        {
            "Test-EN-LIBELLUD_HARMONIES_RULES_EN.pdf",
            "Test-EN-LorenzoRules.pdf"
        };

        _output.WriteLine("=== PDF EXTRACTION QUALITY COMPARISON ===");
        _output.WriteLine("");

        foreach (var pdfFilename in pdfs)
        {
            var pdfPath = GetTestPdfPath(pdfFilename);

            if (!File.Exists(pdfPath))
            {
                _output.WriteLine($"SKIP: {pdfFilename} not found");
                continue;
            }

            var result = await _service.ExtractTextAsync(pdfPath);

            var charsPerPage = result.PageCount > 0 ? result.CharacterCount / result.PageCount : 0;
            var quality = charsPerPage < 100 ? "POOR (likely needs OCR)" :
                         charsPerPage < 500 ? "FAIR" :
                         charsPerPage < 1500 ? "GOOD" : "EXCELLENT";

            _output.WriteLine($"{pdfFilename}:");
            _output.WriteLine($"  Pages: {result.PageCount}");
            _output.WriteLine($"  Total chars: {result.CharacterCount}");
            _output.WriteLine($"  Chars/page: {charsPerPage}");
            _output.WriteLine($"  Quality: {quality}");
            _output.WriteLine("");
        }
    }
}
