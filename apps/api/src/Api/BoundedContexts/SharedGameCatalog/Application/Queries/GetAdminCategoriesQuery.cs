using Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Admin-only query for the categories management table.
/// Returns the same shape as <see cref="GameCategoryDto"/> but with
/// <c>GameCount</c> populated (derived via JOIN on the
/// <c>shared_game_categories</c> junction).
///
/// Issue #1440 — Phase 2 backend wiring for the categories CRUD surface.
/// </summary>
internal sealed record GetAdminCategoriesQuery : IRequest<List<GameCategoryDto>>;

internal sealed class GetAdminCategoriesQueryHandler : IRequestHandler<GetAdminCategoriesQuery, List<GameCategoryDto>>
{
    private readonly MeepleAiDbContext _context;

    public GetAdminCategoriesQueryHandler(MeepleAiDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<List<GameCategoryDto>> Handle(GetAdminCategoriesQuery query, CancellationToken cancellationToken)
    {
        return await _context.GameCategories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new GameCategoryDto(
                c.Id,
                c.Name,
                c.Slug,
                c.Emoji,
                c.Color,
                c.SharedGames.Count))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
