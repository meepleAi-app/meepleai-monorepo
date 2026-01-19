using System.Text;
using Api.BoundedContexts.Administration.Infrastructure.Services.Formatters;

namespace Api.Tests.TestInfrastructure.InMemory;

/// <summary>
/// In-memory CSV formatter for unit tests
/// ISSUE-2601: Minimal valid CSV generation without external dependencies
/// </summary>
internal sealed class InMemoryCsvReportFormatter : IReportFormatter
{
    public byte[] Format(ReportContent content)
    {
        var sb = new StringBuilder();

        // Minimal valid CSV header
        sb.AppendLine("Column1,Column2");
        sb.AppendLine("Value1,Value2");

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    public string GetFileExtension() => "csv";

    public string GetContentType() => "text/csv";
}
