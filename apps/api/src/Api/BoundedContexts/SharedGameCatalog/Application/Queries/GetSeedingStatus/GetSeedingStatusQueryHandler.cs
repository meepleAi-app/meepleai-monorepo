using Api.Infrastructure;
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

        return games.Select(g => new SeedingGameDto(
            g.Id, g.BggId, g.Title, g.GameDataStatus,
            DataStatusNames.GetValueOrDefault(g.GameDataStatus, "Unknown"),
            g.Status, GameStatusNames.GetValueOrDefault(g.Status, "Unknown"),
            g.HasUploadedPdf, g.CreatedAt
        )).ToList();
    }
}
