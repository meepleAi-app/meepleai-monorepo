using System.Globalization;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Command to export shared games catalog as an Excel file.
/// Issue: Admin Bulk Excel Import — export counterpart.
/// </summary>
public sealed record ExportGamesToExcelCommand(
    IReadOnlyList<GameDataStatus>? StatusFilter,
    bool? HasPdfFilter) : ICommand<byte[]>;

/// <summary>
/// Handler for <see cref="ExportGamesToExcelCommand"/>.
/// Queries SharedGames with optional filters and produces an .xlsx byte array.
/// Uses DbContext directly (query-side pattern) to avoid loading full aggregates.
/// </summary>
internal sealed class ExportGamesToExcelCommandHandler
    : ICommandHandler<ExportGamesToExcelCommand, byte[]>
{
    private readonly MeepleAiDbContext _context;
    private readonly ILogger<ExportGamesToExcelCommandHandler> _logger;

    private const int MaxExportRows = 10_000;

    public ExportGamesToExcelCommandHandler(
        MeepleAiDbContext context,
        ILogger<ExportGamesToExcelCommandHandler> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<byte[]> Handle(
        ExportGamesToExcelCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var query = _context.SharedGames.AsNoTracking();

        // Apply status filter
        if (command.StatusFilter is { Count: > 0 })
        {
            var statusInts = command.StatusFilter.Select(s => (int)s).ToList();
            query = query.Where(g => statusInts.Contains(g.GameDataStatus));
        }

        // Apply HasPdf filter
        if (command.HasPdfFilter.HasValue)
        {
            query = query.Where(g => g.HasUploadedPdf == command.HasPdfFilter.Value);
        }

        var games = await query
            .OrderBy(g => g.Title)
            .Take(MaxExportRows)
            .Select(g => new
            {
                g.Id,
                g.Title,
                g.BggId,
                g.Status,
                g.GameDataStatus,
                g.YearPublished,
                g.MinPlayers,
                g.MaxPlayers,
                g.PlayingTimeMinutes,
                g.MinAge,
                g.ComplexityRating,
                g.AverageRating,
                g.Description,
                g.HasUploadedPdf,
                g.CreatedAt
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Exporting {Count} games to Excel (StatusFilter={StatusFilter}, HasPdfFilter={HasPdfFilter})",
            games.Count,
            command.StatusFilter is { Count: > 0 }
                ? string.Join(",", command.StatusFilter)
                : "All",
            command.HasPdfFilter?.ToString() ?? "All");

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Catalog");

        // Header row
        var headers = new[]
        {
            "Id", "Name", "BggId", "Status", "GameDataStatus",
            "YearPublished", "MinPlayers", "MaxPlayers", "PlayingTime",
            "MinAge", "ComplexityRating", "AverageRating",
            "Description", "HasPdf", "CreatedAt"
        };

        for (var col = 1; col <= headers.Length; col++)
        {
            var cell = worksheet.Cell(1, col);
            cell.Value = headers[col - 1];
            cell.Style.Font.Bold = true;
        }

        // Data rows
        for (var i = 0; i < games.Count; i++)
        {
            var game = games[i];
            var row = i + 2;

            worksheet.Cell(row, 1).Value = game.Id.ToString();
            worksheet.Cell(row, 2).Value = game.Title;
            worksheet.Cell(row, 3).Value = game.BggId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            worksheet.Cell(row, 4).Value = ((GameStatus)game.Status).ToString();
            worksheet.Cell(row, 5).Value = ((GameDataStatus)game.GameDataStatus).ToString();
            worksheet.Cell(row, 6).Value = game.YearPublished;
            worksheet.Cell(row, 7).Value = game.MinPlayers;
            worksheet.Cell(row, 8).Value = game.MaxPlayers;
            worksheet.Cell(row, 9).Value = game.PlayingTimeMinutes;
            worksheet.Cell(row, 10).Value = game.MinAge;
            worksheet.Cell(row, 11).Value = game.ComplexityRating?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            worksheet.Cell(row, 12).Value = game.AverageRating?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            worksheet.Cell(row, 13).Value = game.Description;
            worksheet.Cell(row, 14).Value = game.HasUploadedPdf ? "Yes" : "No";
            worksheet.Cell(row, 15).Value = game.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }

        // Auto-fit columns
        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
