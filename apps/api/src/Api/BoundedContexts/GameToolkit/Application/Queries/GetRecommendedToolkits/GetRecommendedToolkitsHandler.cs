using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameToolkit.Application.Queries.GetRecommendedToolkits;

/// <summary>
/// Returns published toolkits ordered by CreatedAt DESC.
/// GameToolkitEntity has no InstallCount field — MVP uses freshness as ranking proxy.
/// InstallCount is always 0 in the DTO. GameName is resolved via the Game navigation
/// property (GameEntity.Name). Limit is clamped to [1, 20].
/// Issue #728: Discover dashboard — "Recommended Toolkits" widget.
/// </summary>
internal sealed class GetRecommendedToolkitsHandler : IQueryHandler<GetRecommendedToolkitsQuery, IReadOnlyList<RecommendedToolkitDto>>
{
    private const int MaxLimit = 20;
    private const int MinLimit = 1;

    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GetRecommendedToolkitsHandler> _logger;

    public GetRecommendedToolkitsHandler(MeepleAiDbContext dbContext, ILogger<GetRecommendedToolkitsHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<RecommendedToolkitDto>> Handle(GetRecommendedToolkitsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var limit = Math.Clamp(query.Limit, MinLimit, MaxLimit);

        _logger.LogInformation("Fetching top {Limit} recommended toolkits ordered by CreatedAt DESC", limit);

        return await _dbContext.GameToolkits.AsNoTracking()
            .Where(t => t.IsPublished)
            .OrderByDescending(t => t.CreatedAt)
            .Take(limit)
            .Select(t => new RecommendedToolkitDto(
                t.Id,
                t.Name,
                t.Game != null ? t.Game.Name : string.Empty,
                t.Version,
                0,          // InstallCount — entity has no such field; MVP default
                t.CreatedAt
            ))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
