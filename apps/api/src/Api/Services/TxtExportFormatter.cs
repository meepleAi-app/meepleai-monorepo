using System.Text;
using System.Text.Json;
using System.Globalization;
using Api.Infrastructure.Entities;

namespace Api.Services;

/// <summary>
/// Exports chat conversations as plain text with timestamps and citations.
/// </summary>
public class TxtExportFormatter : IExportFormatter
{
    private readonly ILogger<TxtExportFormatter>? _logger;

    public string Format => "txt";
    public string ContentType => "text/plain; charset=utf-8";
    public string FileExtension => "txt";

    public TxtExportFormatter(ILogger<TxtExportFormatter>? logger = null)
    {
        _logger = logger;
    }

    public Task<Stream> FormatAsync(ChatEntity chat, DateTime? dateFrom, DateTime? dateTo)
    {
        // Filter logs by date range
        var filteredLogs = FilterLogsByDateRange(chat.Logs, dateFrom, dateTo);

        var sb = new StringBuilder();

        // Header
        var gameName = chat.Game?.Name ?? "Unknown Game";
        sb.AppendLine(CultureInfo.InvariantCulture, $"========================================");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Chat Export: {gameName}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Exported: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Messages: {filteredLogs.Count}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"========================================");
        sb.AppendLine();

        if (filteredLogs.Count == 0)
        {
            sb.AppendLine("No messages in this chat.");
        }
        else
        {
            foreach (var log in filteredLogs.OrderBy(l => l.CreatedAt))
            {
                // Timestamp and level
                sb.AppendLine(CultureInfo.InvariantCulture, $"[{log.CreatedAt:yyyy-MM-dd HH:mm:ss}] {log.Level.ToUpper(CultureInfo.InvariantCulture)}:");
                sb.AppendLine(log.Message);

                // Parse and display citations if present
                var citations = ParseCitations(log.MetadataJson);
                if (citations.Any())
                {
                    sb.AppendLine();
                    sb.AppendLine("Citations:");
                    foreach (var citation in citations)
                    {
                        sb.AppendLine(CultureInfo.InvariantCulture, $"  - {citation.Source}, Page {citation.Page}");
                    }
                }

                sb.AppendLine();
                sb.AppendLine("----------------------------------------");
                sb.AppendLine();
            }
        }

        // Footer
        sb.AppendLine("========================================");
        sb.AppendLine("End of Chat Export");
        sb.AppendLine("========================================");

        var content = sb.ToString();
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        stream.Position = 0;

        return Task.FromResult<Stream>(stream);
    }

    private List<ChatLogEntity> FilterLogsByDateRange(
        ICollection<ChatLogEntity> logs,
        DateTime? dateFrom,
        DateTime? dateTo)
    {
        var filtered = logs.AsEnumerable();

        if (dateFrom.HasValue)
        {
            filtered = filtered.Where(log => log.CreatedAt >= dateFrom.Value);
        }

        if (dateTo.HasValue)
        {
            // Include entire day for dateTo (up to 23:59:59.999)
            var dateToEndOfDay = dateTo.Value.Date.AddDays(1).AddTicks(-1);
            filtered = filtered.Where(log => log.CreatedAt <= dateToEndOfDay);
        }

        return filtered.ToList();
    }

    /// <summary>
    /// Safely parses citation metadata from JSON.
    /// Returns empty list if parsing fails or citations are missing.
    /// </summary>
    private List<CitationMetadata> ParseCitations(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return new List<CitationMetadata>();
        }

        try
        {
            var metadata = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(metadataJson);
            if (metadata == null || !metadata.ContainsKey("citations"))
            {
                return new List<CitationMetadata>();
            }

            var citationsElement = metadata["citations"];
            if (citationsElement.ValueKind != JsonValueKind.Array)
            {
                return new List<CitationMetadata>();
            }

            var citations = new List<CitationMetadata>();
            foreach (var element in citationsElement.EnumerateArray())
            {
                try
                {
                    var source = element.GetProperty("source").GetString() ?? "Unknown";
                    var page = element.GetProperty("page").GetInt32();
                    citations.Add(new CitationMetadata(source, page));
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception ex)
#pragma warning restore CA1031
                {
                    // DATA ROBUSTNESS PATTERN: Malformed citation entries are skipped, not fatal
                    // Rationale: Chat export with incomplete/malformed citations should still succeed.
                    // Citations are metadata enrichment - missing a citation field (source/page) should
                    // not prevent the entire export. We skip the problematic entry and continue parsing
                    // remaining citations. User gets valid citations that parsed successfully.
                    // Context: Citation malformation from RAG JSON structure changes or DB corruption
                    _logger?.LogWarning(ex, "Failed to parse citation from metadata: {Metadata}", element.ToString());
                }
            }

            return citations;
        }
        catch (JsonException ex)
        {
            _logger?.LogWarning(ex, "Failed to parse metadata JSON: {MetadataJson}", metadataJson);
            return new List<CitationMetadata>();
        }
    }

    private record CitationMetadata(string Source, int Page);
}