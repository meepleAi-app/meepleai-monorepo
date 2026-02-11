using System.Globalization;
using System.Text;
using Api.BoundedContexts.BusinessSimulations.Domain.Entities;
using Api.BoundedContexts.BusinessSimulations.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Api.BoundedContexts.BusinessSimulations.Application.Queries;

/// <summary>
/// Handler for ExportLedgerQuery. Exports ledger entries as CSV, Excel, or PDF.
/// Issue #3724: Export Ledger (PDF/CSV/Excel) (Epic #3688)
/// </summary>
internal sealed class ExportLedgerQueryHandler : IQueryHandler<ExportLedgerQuery, ExportLedgerResult>
{
    private const int MaxExportRecords = 10000;
    private static readonly string[] PdfTableHeaders = ["Date", "Type", "Category", "Amount", "Source", "Description"];
    private readonly ILedgerEntryRepository _repository;

    public ExportLedgerQueryHandler(ILedgerEntryRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<ExportLedgerResult> Handle(ExportLedgerQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var (entries, _) = await _repository.GetFilteredAsync(
            request.Type,
            request.Category,
            source: null,
            request.DateFrom,
            request.DateTo,
            page: 1,
            pageSize: MaxExportRecords).ConfigureAwait(false);

        return request.Format switch
        {
            LedgerExportFormat.Csv => FormatCsv(entries, request),
            LedgerExportFormat.Excel => FormatExcel(entries, request),
            LedgerExportFormat.Pdf => FormatPdf(entries, request),
            _ => throw new ArgumentOutOfRangeException(nameof(request), request.Format, "Unknown export format")
        };
    }

    private static ExportLedgerResult FormatCsv(IReadOnlyList<LedgerEntry> entries, ExportLedgerQuery request)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Date,Type,Category,Amount,Currency,Source,Description,CreatedAt");

        foreach (var e in entries)
        {
            sb.AppendLine(string.Join(",",
                e.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                e.Type.ToString(),
                e.Category.ToString(),
                e.Amount.Amount.ToString("F2", CultureInfo.InvariantCulture),
                EscapeCsv(e.Amount.Currency),
                e.Source.ToString(),
                EscapeCsv(e.Description ?? ""),
                e.CreatedAt.ToString("o", CultureInfo.InvariantCulture)));
        }

        var fileName = BuildFileName("ledger-export", request, "csv");
        return new ExportLedgerResult(
            Encoding.UTF8.GetBytes(sb.ToString()),
            "text/csv",
            fileName);
    }

    private static ExportLedgerResult FormatExcel(IReadOnlyList<LedgerEntry> entries, ExportLedgerQuery request)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Ledger Entries");

        // Headers
        var headers = new[] { "Date", "Type", "Category", "Amount", "Currency", "Source", "Description", "Created At" };
        for (var i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(0x2563EB);
            cell.Style.Font.FontColor = XLColor.White;
        }

        // Data rows
        for (var row = 0; row < entries.Count; row++)
        {
            var e = entries[row];
            var r = row + 2;
            ws.Cell(r, 1).Value = e.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            ws.Cell(r, 2).Value = e.Type.ToString();
            ws.Cell(r, 3).Value = e.Category.ToString();
            ws.Cell(r, 4).Value = (double)e.Amount.Amount;
            ws.Cell(r, 4).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(r, 5).Value = e.Amount.Currency;
            ws.Cell(r, 6).Value = e.Source.ToString();
            ws.Cell(r, 7).Value = e.Description ?? "";
            ws.Cell(r, 8).Value = e.CreatedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }

        // Summary row
        var summaryRow = entries.Count + 3;
        var totalIncome = entries.Where(e => e.Type == Domain.Enums.LedgerEntryType.Income).Sum(e => e.Amount.Amount);
        var totalExpense = entries.Where(e => e.Type == Domain.Enums.LedgerEntryType.Expense).Sum(e => e.Amount.Amount);

        ws.Cell(summaryRow, 1).Value = "TOTALS";
        ws.Cell(summaryRow, 1).Style.Font.Bold = true;
        ws.Cell(summaryRow, 2).Value = $"Income: {totalIncome:F2}";
        ws.Cell(summaryRow, 3).Value = $"Expense: {totalExpense:F2}";
        ws.Cell(summaryRow, 4).Value = (double)(totalIncome - totalExpense);
        ws.Cell(summaryRow, 4).Style.NumberFormat.Format = "#,##0.00";
        ws.Cell(summaryRow, 4).Style.Font.Bold = true;

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        var fileName = BuildFileName("ledger-export", request, "xlsx");
        return new ExportLedgerResult(
            ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    private static ExportLedgerResult FormatPdf(IReadOnlyList<LedgerEntry> entries, ExportLedgerQuery request)
    {
        var totalIncome = entries.Where(e => e.Type == Domain.Enums.LedgerEntryType.Income).Sum(e => e.Amount.Amount);
        var totalExpense = entries.Where(e => e.Type == Domain.Enums.LedgerEntryType.Expense).Sum(e => e.Amount.Amount);
        var netBalance = totalIncome - totalExpense;

        var dateRange = BuildDateRangeLabel(request);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10));

                // Header
                page.Header().Column(col =>
                {
                    col.Item().Text("Financial Ledger Report")
                        .FontSize(22).Bold().FontColor(Colors.Blue.Darken2);
                    col.Item().PaddingTop(3).Text(dateRange)
                        .FontSize(12).FontColor(Colors.Grey.Darken1);
                    col.Item().PaddingTop(3).Text($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC")
                        .FontSize(9).FontColor(Colors.Grey.Medium);
                    col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                });

                // Content
                page.Content().PaddingVertical(0.5f, Unit.Centimetre).Column(col =>
                {
                    // Summary cards
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Background(Colors.Green.Lighten4).Padding(10).Column(c =>
                        {
                            c.Item().Text("Total Income").FontSize(10).FontColor(Colors.Grey.Darken1);
                            c.Item().Text($"{totalIncome:N2} EUR").FontSize(16).Bold().FontColor(Colors.Green.Darken2);
                        });
                        row.ConstantItem(10);
                        row.RelativeItem().Background(Colors.Red.Lighten4).Padding(10).Column(c =>
                        {
                            c.Item().Text("Total Expenses").FontSize(10).FontColor(Colors.Grey.Darken1);
                            c.Item().Text($"{totalExpense:N2} EUR").FontSize(16).Bold().FontColor(Colors.Red.Darken2);
                        });
                        row.ConstantItem(10);
                        row.RelativeItem().Background(netBalance >= 0 ? Colors.Green.Lighten4 : Colors.Red.Lighten4).Padding(10).Column(c =>
                        {
                            c.Item().Text("Net Balance").FontSize(10).FontColor(Colors.Grey.Darken1);
                            c.Item().Text($"{netBalance:N2} EUR").FontSize(16).Bold()
                                .FontColor(netBalance >= 0 ? Colors.Green.Darken2 : Colors.Red.Darken2);
                        });
                    });

                    col.Item().PaddingTop(15);

                    // Entries table
                    col.Item().Text($"Entries ({entries.Count})").FontSize(14).Bold();
                    col.Item().PaddingTop(5);

                    if (entries.Count == 0)
                    {
                        col.Item().Padding(20).AlignCenter()
                            .Text("No entries found for the selected period.")
                            .FontColor(Colors.Grey.Medium);
                    }
                    else
                    {
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(2); // Date
                                columns.RelativeColumn(1.5f); // Type
                                columns.RelativeColumn(2); // Category
                                columns.RelativeColumn(2); // Amount
                                columns.RelativeColumn(1); // Source
                                columns.RelativeColumn(4); // Description
                            });

                            // Header
                            table.Header(header =>
                            {
                                foreach (var h in PdfTableHeaders)
                                {
                                    header.Cell().Background(Colors.Blue.Darken2).Padding(5)
                                        .Text(h).FontSize(10).Bold().FontColor(Colors.White);
                                }
                            });

                            // Data
                            for (var idx = 0; idx < entries.Count; idx++)
                            {
                                var e = entries[idx];
                                var bgColor = idx % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;

                                table.Cell().Background(bgColor).Padding(4)
                                    .Text(e.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).FontSize(9);
                                table.Cell().Background(bgColor).Padding(4)
                                    .Text(e.Type.ToString()).FontSize(9)
                                    .FontColor(e.Type == Domain.Enums.LedgerEntryType.Income ? Colors.Green.Darken2 : Colors.Red.Darken2);
                                table.Cell().Background(bgColor).Padding(4)
                                    .Text(e.Category.ToString()).FontSize(9);
                                table.Cell().Background(bgColor).Padding(4)
                                    .Text($"{e.Amount.Amount:N2} {e.Amount.Currency}").FontSize(9)
                                    .FontColor(e.Type == Domain.Enums.LedgerEntryType.Income ? Colors.Green.Darken2 : Colors.Red.Darken2);
                                table.Cell().Background(bgColor).Padding(4)
                                    .Text(e.Source.ToString()).FontSize(9);
                                table.Cell().Background(bgColor).Padding(4)
                                    .Text(e.Description ?? "-").FontSize(9);
                            }
                        });
                    }
                });

                // Footer
                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Page ");
                    x.CurrentPageNumber();
                    x.Span(" of ");
                    x.TotalPages();
                    x.Span(" | MeepleAI Financial Ledger");
                });
            });
        });

        using var ms = new MemoryStream();
        document.GeneratePdf(ms);
        var fileName = BuildFileName("ledger-report", request, "pdf");
        return new ExportLedgerResult(ms.ToArray(), "application/pdf", fileName);
    }

    private static string BuildFileName(string prefix, ExportLedgerQuery request, string extension)
    {
        var datePart = request.DateFrom.HasValue && request.DateTo.HasValue
            ? $"{request.DateFrom.Value:yyyyMMdd}-{request.DateTo.Value:yyyyMMdd}"
            : DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);

        return $"{prefix}-{datePart}.{extension}";
    }

    private static string BuildDateRangeLabel(ExportLedgerQuery request)
    {
        if (request.DateFrom.HasValue && request.DateTo.HasValue)
            return $"Period: {request.DateFrom.Value:MMMM d, yyyy} - {request.DateTo.Value:MMMM d, yyyy}";
        if (request.DateFrom.HasValue)
            return $"From: {request.DateFrom.Value:MMMM d, yyyy}";
        if (request.DateTo.HasValue)
            return $"Until: {request.DateTo.Value:MMMM d, yyyy}";
        return "All entries";
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',', StringComparison.Ordinal) ||
            value.Contains('"', StringComparison.Ordinal) ||
            value.Contains('\n', StringComparison.Ordinal))
        {
            return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }
        return value;
    }
}
