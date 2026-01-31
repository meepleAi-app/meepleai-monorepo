using Api.BoundedContexts.Administration.Infrastructure.Services.Formatters;

namespace Api.Tests.TestInfrastructure.InMemory;

/// <summary>
/// In-memory PDF formatter for unit tests
/// ISSUE-2601: Minimal valid PDF generation without QuestPDF dependency
/// </summary>
internal sealed class InMemoryPdfReportFormatter : IReportFormatter
{
    public byte[] Format(ReportContent content)
    {
        // Minimal valid PDF signature
        // %PDF-1.4 header (magic bytes for PDF files)
        return new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 };
    }

    public string GetFileExtension() => "pdf";

    public string GetContentType() => "application/pdf";
}
