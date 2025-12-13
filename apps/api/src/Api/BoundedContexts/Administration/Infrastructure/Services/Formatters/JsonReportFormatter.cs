using System.Text;
using System.Text.Json;

namespace Api.BoundedContexts.Administration.Infrastructure.Services.Formatters;

/// <summary>
/// JSON report formatter
/// ISSUE-916: Formats reports as JSON for programmatic consumption
/// </summary>
public sealed class JsonReportFormatter : IReportFormatter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public byte[] Format(ReportContent content)
    {
        var reportObject = new
        {
            content.Title,
            content.Description,
            GeneratedAt = content.GeneratedAt.ToString("O"), // ISO 8601
            content.Metadata,
            Sections = content.Sections.Select(s => new
            {
                s.Title,
                s.Description,
                Data = s.Data.Select(row => row.Values).ToList()
            }).ToList()
        };

        var json = JsonSerializer.Serialize(reportObject, SerializerOptions);
        return Encoding.UTF8.GetBytes(json);
    }

    public string GetFileExtension() => "json";

    public string GetContentType() => "application/json";
}
