using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Handler for getting a shared game by ID.
/// Uses HybridCache (L1: Memory 30min, L2: Redis 2h) for performance.
/// Issue #2371 Phase 2
/// </summary>
internal sealed class GetSharedGameByIdQueryHandler : IRequestHandler<GetSharedGameByIdQuery, SharedGameDetailDto?>
{
    private readonly ISharedGameRepository _repository;
    private readonly HybridCache _cache;
    private readonly ILogger<GetSharedGameByIdQueryHandler> _logger;

    public GetSharedGameByIdQueryHandler(
        ISharedGameRepository repository,
        HybridCache cache,
        ILogger<GetSharedGameByIdQueryHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SharedGameDetailDto?> Handle(GetSharedGameByIdQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var cacheKey = $"shared-game:{query.GameId}";

        // Try cache first (L1: 30min, L2: 2h)
        var cachedGame = await _cache.GetOrCreateAsync<SharedGameDetailDto?>(
            cacheKey,
            async cancel =>
            {
                _logger.LogInformation("Cache miss for shared game: {GameId}", query.GameId);
                return await FetchGameDetailsAsync(query.GameId, cancel).ConfigureAwait(false);
            },
            new HybridCacheEntryOptions
            {
                LocalCacheExpiration = TimeSpan.FromMinutes(30),  // L1
                Expiration = TimeSpan.FromHours(2)  // L2
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return cachedGame;
    }

    private async Task<SharedGameDetailDto?> FetchGameDetailsAsync(Guid gameId, CancellationToken cancellationToken)
    {
        var game = await _repository.GetByIdAsync(gameId, cancellationToken).ConfigureAwait(false);
        if (game is null)
        {
            _logger.LogWarning("Shared game not found: {GameId}", gameId);
            return null;
        }

        GameRulesDto? rulesDto = null;
        if (game.Rules is not null)
        {
            rulesDto = new GameRulesDto(game.Rules.Content, game.Rules.Language);
        }

        return new SharedGameDetailDto(
            game.Id,
            game.BggId,
            game.Title,
            game.YearPublished,
            game.Description,
            game.MinPlayers,
            game.MaxPlayers,
            game.PlayingTimeMinutes,
            game.MinAge,
            game.ComplexityRating,
            game.AverageRating,
            game.ImageUrl,
            game.ThumbnailUrl,
            rulesDto,
            game.Status,
            game.CreatedBy,
            game.ModifiedBy,
            game.CreatedAt,
            game.ModifiedAt);
    }
}
