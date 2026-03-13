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
        var categoriesTask = _context.Set<GameCategoryEntity>()
            .AsNoTracking()
            .Select(c => c.Name)
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync(cancellationToken);

        var mechanicsTask = _context.Set<GameMechanicEntity>()
            .AsNoTracking()
            .Select(m => m.Name)
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync(cancellationToken);

        var designersTask = _context.Set<GameDesignerEntity>()
            .AsNoTracking()
            .Select(d => d.Name)
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync(cancellationToken);

        var publishersTask = _context.Set<GamePublisherEntity>()
            .AsNoTracking()
            .Select(p => p.Name)
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync(cancellationToken);

        await Task.WhenAll(categoriesTask, mechanicsTask, designersTask, publishersTask).ConfigureAwait(false);

        return new DistinctMetadataDto(
            categoriesTask.Result,
            mechanicsTask.Result,
            designersTask.Result,
            publishersTask.Result);
    }
}
