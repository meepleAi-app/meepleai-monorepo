using System.Text;
using System.Text.Json;
using Api.BoundedContexts.Administration.Infrastructure.Services.Formatters;

namespace Api.Tests.TestInfrastructure.InMemory;

/// <summary>
/// In-memory JSON formatter for unit tests
/// ISSUE-2601: Minimal valid JSON generation without external dependencies
/// </summary>
internal sealed class InMemoryJsonReportFormatter : IReportFormatter
{
    public byte[] Format(ReportContent content)
    {
        // Minimal valid JSON object
        var minimalReport = new
        {
            title = content.Title,
            description = content.Description,
            generatedAt = content.GeneratedAt.ToString("O"),
            sections = Array.Empty<object>()
        };

        var json = JsonSerializer.Serialize(minimalReport, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        return Encoding.UTF8.GetBytes(json);
    }

    public string GetFileExtension() => "json";

    public string GetContentType() => "application/json";
}
