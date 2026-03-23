using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetSeedingStatus;

internal sealed class GetSeedingStatusQueryHandler
    : IRequestHandler<GetSeedingStatusQuery, List<SeedingGameDto>>
{
    private static readonly Dictionary<int, string> DataStatusNames = new()
    {
        [0] = "Skeleton",
        [1] = "EnrichmentQueued",
        [2] = "Enriching",
        [3] = "Enriched",
        [4] = "PdfDownloading",
        [5] = "Complete",
        [6] = "Failed"
    };

    private static readonly Dictionary<int, string> GameStatusNames = new()
    {
        [0] = "Draft",
        [1] = "PendingApproval",
        [2] = "Published",
        [3] = "Archived"
    };

    private readonly MeepleAiDbContext _context;

    public GetSeedingStatusQueryHandler(MeepleAiDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<List<SeedingGameDto>> Handle(
        GetSeedingStatusQuery query, CancellationToken cancellationToken)
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
                g.BggId,
                g.Title,
                g.GameDataStatus,
                g.Status,
                g.HasUploadedPdf,
                g.CreatedAt
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Get error messages for failed games from BggImportQueue
        var failedErrors = await _context.Set<BggImportQueueEntity>()
            .AsNoTracking()
            .Where(q => q.Status == BggImportStatus.Failed && q.ErrorMessage != null && q.BggId != null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var errorByBggId = failedErrors
            .GroupBy(q => q.BggId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.CreatedAt).First().ErrorMessage);

        return games.Select(g => new SeedingGameDto(
            g.Id, g.BggId, g.Title, g.GameDataStatus,
            DataStatusNames.GetValueOrDefault(g.GameDataStatus, "Unknown"),
            g.Status, GameStatusNames.GetValueOrDefault(g.Status, "Unknown"),
            g.HasUploadedPdf,
            ragReadySet.Contains(g.Id),
            g.BggId.HasValue && errorByBggId.TryGetValue(g.BggId.Value, out var err) ? err : null,
            g.CreatedAt
        )).ToList();
    }
}
