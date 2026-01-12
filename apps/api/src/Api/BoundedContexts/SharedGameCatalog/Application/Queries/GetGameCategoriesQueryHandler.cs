using Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

internal sealed class GetGameCategoriesQueryHandler : IRequestHandler<GetGameCategoriesQuery, List<GameCategoryDto>>
{
    private readonly MeepleAiDbContext _context;

    public GetGameCategoriesQueryHandler(MeepleAiDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<List<GameCategoryDto>> Handle(GetGameCategoriesQuery query, CancellationToken cancellationToken)
    {
        var categories = await _context.GameCategories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new GameCategoryDto(c.Id, c.Name, c.Slug))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return categories;
    }
}
