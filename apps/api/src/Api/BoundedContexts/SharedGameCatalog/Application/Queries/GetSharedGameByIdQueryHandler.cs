using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Services;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Handler for getting a shared game by ID.
/// Uses HybridCache (L1: Memory 30min, L2: Redis 2h) for performance.
/// Issue #2371 Phase 2, Extended Issue #2373 Phase 4
/// </summary>
internal sealed class GetSharedGameByIdQueryHandler : IRequestHandler<GetSharedGameByIdQuery, SharedGameDetailDto?>
{
    private readonly ISharedGameRepository _repository;
    private readonly HybridCache _cache;
    private readonly ICacheMetricsRecorder _cacheMetrics;
    private readonly ILogger<GetSharedGameByIdQueryHandler> _logger;

    public GetSharedGameByIdQueryHandler(
        ISharedGameRepository repository,
        HybridCache cache,
        ICacheMetricsRecorder cacheMetrics,
        ILogger<GetSharedGameByIdQueryHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _cacheMetrics = cacheMetrics ?? throw new ArgumentNullException(nameof(cacheMetrics));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SharedGameDetailDto?> Handle(GetSharedGameByIdQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var cacheKey = $"shared-game:{query.GameId}";
        bool cacheHit = true;

        // Try cache first (L1: 30min, L2: 2h)
        var cachedGame = await _cache.GetOrCreateAsync<SharedGameDetailDto?>(
            cacheKey,
            async cancel =>
            {
                cacheHit = false;
                await _cacheMetrics.RecordCacheMissAsync("get_by_id", "shared_games").ConfigureAwait(false);
                _logger.LogInformation("Cache miss for shared game: {GameId}", query.GameId);
                return await FetchGameDetailsAsync(query.GameId, cancel).ConfigureAwait(false);
            },
            new HybridCacheEntryOptions
            {
                LocalCacheExpiration = TimeSpan.FromMinutes(30),  // L1
                Expiration = TimeSpan.FromHours(2)  // L2
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (cacheHit)
        {
            await _cacheMetrics.RecordCacheHitAsync("get_by_id", "shared_games").ConfigureAwait(false);
            _logger.LogDebug("Cache hit for shared game: {GameId}", query.GameId);
        }

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

        // Map FAQs (ordered by Order property)
        var faqDtos = game.Faqs
            .OrderBy(f => f.Order)
            .Select(f => new GameFaqDto(f.Id, f.Question, f.Answer, f.Order, f.CreatedAt))
            .ToList();

        // Map Errata (ordered by PublishedDate descending)
        var errataDtos = game.Erratas
            .OrderByDescending(e => e.PublishedDate)
            .Select(e => new GameErrataDto(e.Id, e.Description, e.PageReference, e.PublishedDate, e.CreatedAt))
            .ToList();

        // Map Designers
        var designerDtos = game.Designers
            .Select(d => new GameDesignerDto(d.Id, d.Name))
            .ToList();

        // Map Publishers
        var publisherDtos = game.Publishers
            .Select(p => new GamePublisherDto(p.Id, p.Name))
            .ToList();

        // Map Categories
        var categoryDtos = game.Categories
            .Select(c => new GameCategorySimpleDto(c.Id, c.Name, c.Slug))
            .ToList();

        // Map Mechanics
        var mechanicDtos = game.Mechanics
            .Select(m => new GameMechanicSimpleDto(m.Id, m.Name, m.Slug))
            .ToList();

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
            game.ModifiedAt,
            faqDtos,
            errataDtos,
            designerDtos,
            publisherDtos,
            categoryDtos,
            mechanicDtos);
    }
}