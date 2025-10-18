using System.Text.Json;
using Api.Infrastructure.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Api.Services;

/// <summary>
/// Exports chat conversations as professionally formatted PDF documents.
/// Uses QuestPDF library for document generation.
/// </summary>
public class PdfExportFormatter : IExportFormatter
{
    private readonly ILogger<PdfExportFormatter>? _logger;

    public string Format => "pdf";
    public string ContentType => "application/pdf";
    public string FileExtension => "pdf";

    public PdfExportFormatter(ILogger<PdfExportFormatter>? logger = null)
    {
        _logger = logger;

        // Required for QuestPDF Community license (free tier)
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public Task<Stream> FormatAsync(ChatEntity chat, DateTime? dateFrom, DateTime? dateTo)
    {
        // Filter logs by date range
        var filteredLogs = FilterLogsByDateRange(chat.Logs, dateFrom, dateTo);

        var stream = new MemoryStream();

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));

                page.Header()
                    .Element(container => ComposeHeader(container, chat.Game.Name, filteredLogs.Count));

                page.Content()
                    .Element(container => ComposeContent(container, filteredLogs));

                page.Footer()
                    .AlignCenter()
                    .Text(x =>
                    {
                        x.Span("Page ");
                        x.CurrentPageNumber();
                        x.Span(" of ");
                        x.TotalPages();
                    });
            });
        })
        .GeneratePdf(stream);

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

    private void ComposeHeader(IContainer container, string gameName, int messageCount)
    {
        container.Column(column =>
        {
            column.Item().BorderBottom(1).PaddingBottom(10).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text($"Chat Export: {gameName}")
                        .FontSize(16)
                        .Bold()
                        .FontColor(Colors.Blue.Darken2);

                    col.Item().PaddingTop(5).Text(text =>
                    {
                        text.Span("Exported: ").FontSize(9).FontColor(Colors.Grey.Darken1);
                        text.Span($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC").FontSize(9);
                    });

                    col.Item().Text(text =>
                    {
                        text.Span("Messages: ").FontSize(9).FontColor(Colors.Grey.Darken1);
                        text.Span($"{messageCount}").FontSize(9);
                    });
                });
            });

            column.Item().PaddingTop(10);
        });
    }

    private void ComposeContent(IContainer container, List<ChatLogEntity> logs)
    {
        container.Column(column =>
        {
            if (logs.Count == 0)
            {
                column.Item()
                    .PaddingVertical(20)
                    .AlignCenter()
                    .Text("No messages in this chat.")
                    .FontSize(12)
                    .Italic()
                    .FontColor(Colors.Grey.Medium);
                return;
            }

            foreach (var log in logs.OrderBy(l => l.CreatedAt))
            {
                column.Item().Element(container => ComposeMessage(container, log));
                column.Item().PaddingBottom(15);
            }
        });
    }

    private void ComposeMessage(IContainer container, ChatLogEntity log)
    {
        var isUser = log.Level.Equals("user", StringComparison.OrdinalIgnoreCase);
        var bgColor = isUser ? Colors.Blue.Lighten4 : Colors.Green.Lighten5;
        var borderColor = isUser ? Colors.Blue.Medium : Colors.Green.Medium;

        container.Border(1)
            .BorderColor(borderColor)
            .Background(bgColor)
            .Padding(12)
            .Column(column =>
            {
                // Message header
                column.Item().Row(row =>
                {
                    row.RelativeItem().Text(log.Level.ToUpper())
                        .FontSize(10)
                        .Bold()
                        .FontColor(borderColor);

                    row.AutoItem().Text($"{log.CreatedAt:yyyy-MM-dd HH:mm:ss}")
                        .FontSize(9)
                        .FontColor(Colors.Grey.Darken1);
                });

                // Message content
                column.Item().PaddingTop(8).Text(log.Message)
                    .FontSize(11)
                    .LineHeight(1.4f);

                // Citations
                var citations = ParseCitations(log.MetadataJson);
                if (citations.Any())
                {
                    column.Item().PaddingTop(10).Column(citationColumn =>
                    {
                        citationColumn.Item().Text("Citations:")
                            .FontSize(9)
                            .Bold()
                            .FontColor(Colors.Grey.Darken2);

                        citationColumn.Item().PaddingTop(3).Column(listColumn =>
                        {
                            foreach (var citation in citations)
                            {
                                listColumn.Item().Text(text =>
                                {
                                    text.Span("• ").FontColor(Colors.Grey.Medium);
                                    text.Span($"{citation.Source}, Page {citation.Page}")
                                        .FontSize(9)
                                        .Italic()
                                        .FontColor(Colors.Grey.Darken1);
                                });
                            }
                        });
                    });
                }
            });
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
                catch (Exception ex)
                {
                    // Skip malformed citation entries, log warning
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
