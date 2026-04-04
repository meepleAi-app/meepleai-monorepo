using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Application.Interfaces;
using ClosedXML.Excel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.ExportSharedGamesTracking;

internal sealed class ExportSharedGamesTrackingQueryHandler
    : IRequestHandler<ExportSharedGamesTrackingQuery, byte[]>
{
    private readonly MeepleAiDbContext _context;

    public ExportSharedGamesTrackingQueryHandler(MeepleAiDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<byte[]> Handle(
        ExportSharedGamesTrackingQuery query, CancellationToken cancellationToken)
    {
        var ragReadyGameIds = await (
            from sgd in _context.Set<SharedGameDocumentEntity>()
            join vd in _context.VectorDocuments on sgd.PdfDocumentId equals vd.PdfDocumentId
            where vd.IndexingStatus == "completed"
            select sgd.SharedGameId
        ).Distinct().ToListAsync(cancellationToken).ConfigureAwait(false);

        var ragReadySet = ragReadyGameIds.ToHashSet();

        var games = await _context.SharedGames
            .AsNoTracking()
            .Where(g => !g.IsDeleted)
            .OrderBy(g => g.Title)
            .Select(g => new
            {
                g.Id,
                g.Title,
                g.BggId,
                g.GameDataStatus,
                g.Status,
                g.HasUploadedPdf,
                g.YearPublished,
                g.MinPlayers,
                g.MaxPlayers,
                g.ComplexityRating,
                g.CreatedAt
            })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Tracking");

        var headers = new[] {
            "Title", "BGG ID", "Data Status", "Game Status",
            "Has PDF", "RAG Ready", "Created At",
            "Year", "Players", "Complexity"
        };

        for (var i = 0; i < headers.Length; i++)
        {
            var cell = worksheet.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.LightGray;
        }

        var dataStatusNames = new Dictionary<int, string>
        {
            [0] = "Skeleton",
            [1] = "EnrichmentQueued",
            [2] = "Enriching",
            [3] = "Enriched",
            [4] = "PdfDownloading",
            [5] = "Complete",
            [6] = "Failed"
        };

        var gameStatusNames = new Dictionary<int, string>
        {
            [0] = "Draft",
            [1] = "PendingApproval",
            [2] = "Published",
            [3] = "Archived"
        };

        for (var row = 0; row < games.Count; row++)
        {
            var g = games[row];
            var r = row + 2;

            worksheet.Cell(r, 1).Value = g.Title;
            worksheet.Cell(r, 2).Value = g.BggId ?? 0;
            worksheet.Cell(r, 3).Value = dataStatusNames.GetValueOrDefault(g.GameDataStatus, "Unknown");
            worksheet.Cell(r, 4).Value = gameStatusNames.GetValueOrDefault(g.Status, "Unknown");
            worksheet.Cell(r, 5).Value = g.HasUploadedPdf ? "Yes" : "No";
            var isRagReady = ragReadySet.Contains(g.Id);
            worksheet.Cell(r, 6).Value = isRagReady ? "Yes" : "No";
            worksheet.Cell(r, 7).Value = g.CreatedAt.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
            worksheet.Cell(r, 8).Value = g.YearPublished == 0 ? "-" : g.YearPublished.ToString(System.Globalization.CultureInfo.InvariantCulture);
            worksheet.Cell(r, 9).Value = g.MinPlayers == 0 ? "-" : $"{g.MinPlayers}-{g.MaxPlayers}";
            worksheet.Cell(r, 10).Value = g.ComplexityRating?.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) ?? "-";

            var statusCell = worksheet.Cell(r, 3);
            statusCell.Style.Fill.BackgroundColor = g.GameDataStatus switch
            {
                0 => XLColor.LightCoral,
                3 => XLColor.LightYellow,
                5 => XLColor.LightGreen,
                6 => XLColor.Orange,
                _ => XLColor.NoColor
            };

            if (g.HasUploadedPdf)
                worksheet.Cell(r, 5).Style.Fill.BackgroundColor = XLColor.LightGreen;

            if (isRagReady)
                worksheet.Cell(r, 6).Style.Fill.BackgroundColor = XLColor.LightGreen;
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
