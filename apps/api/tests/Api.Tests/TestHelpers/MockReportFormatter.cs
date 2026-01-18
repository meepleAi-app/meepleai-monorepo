using Api.BoundedContexts.Administration.Domain.Services;
using System.Text;

namespace Api.Tests.TestHelpers;

/// <summary>
/// Mock report formatter for unit testing
/// ISSUE-2601: Provides minimal valid output for PDF/CSV/JSON without external dependencies
/// </summary>
public sealed class MockReportFormatter : IReportFormatter
{
    private readonly string _fileExtension;
    private readonly byte[] _mockContent;

    private MockReportFormatter(string fileExtension, byte[] mockContent)
    {
        _fileExtension = fileExtension;
        _mockContent = mockContent;
    }

    public static MockReportFormatter CreatePdfFormatter()
    {
        // Minimal valid PDF header (recognized as PDF by most tools)
        var pdfHeader = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 }; // "%PDF-1.4"
        return new MockReportFormatter("pdf", pdfHeader);
    }

    public static MockReportFormatter CreateCsvFormatter()
    {
        var csv = "Column1,Column2,Column3\nValue1,Value2,Value3\n";
        return new MockReportFormatter("csv", Encoding.UTF8.GetBytes(csv));
    }

    public static MockReportFormatter CreateJsonFormatter()
    {
        var json = "{\"data\": [], \"generatedAt\": \"" + DateTime.UtcNow.ToString("O") + "\"}";
        return new MockReportFormatter("json", Encoding.UTF8.GetBytes(json));
    }

    public byte[] Format(object content)
    {
        // Return deterministic mock content regardless of input
        return _mockContent;
    }

    public string GetFileExtension() => _fileExtension;
}
