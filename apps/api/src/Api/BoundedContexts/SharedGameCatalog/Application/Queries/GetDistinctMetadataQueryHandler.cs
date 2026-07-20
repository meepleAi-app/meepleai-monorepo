using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Handler for getting all distinct metadata values (categories, mechanics, designers, publishers).
/// Used for autocomplete inputs when creating/editing shared games.
/// </summary>
internal sealed class GetDistinctMetadataQueryHandler : IRequestHandler<GetDistinctMetadataQuery, DistinctMetadataDto>
{
    private readonly MeepleAiDbContext _context;

    public GetDistinctMetadataQueryHandler(MeepleAiDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<DistinctMetadataDto> Handle(GetDistinctMetadataQuery query, CancellationToken cancellationToken)
    {
        // Issue #3228: await sequentially — a single scoped DbContext cannot run queries
        // concurrently (EF's ConcurrencyDetector throws), so the parallelism was illusory and
        // these Distinct lookups are cheap.
        var categories = await _context.Set<GameCategoryEntity>()
            .AsNoTracking()
            .Select(c => c.Name)
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var mechanics = await _context.Set<GameMechanicEntity>()
            .AsNoTracking()
            .Select(m => m.Name)
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var designers = await _context.Set<GameDesignerEntity>()
            .AsNoTracking()
            .Select(d => d.Name)
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var publishers = await _context.Set<GamePublisherEntity>()
            .AsNoTracking()
            .Select(p => p.Name)
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new DistinctMetadataDto(categories, mechanics, designers, publishers);
    }
}
