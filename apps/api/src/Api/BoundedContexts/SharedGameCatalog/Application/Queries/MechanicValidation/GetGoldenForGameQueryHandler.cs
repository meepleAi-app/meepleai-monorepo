using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicValidation;

/// <summary>
/// Handler for <see cref="GetGoldenForGameQuery"/>. Returns the curated golden-set for a
/// shared game, cached for 10 minutes via <see cref="IHybridCacheService"/> with a fallback
/// to direct DB lookups when the cache layer fails.
/// </summary>
/// <remarks>
/// ADR-051 Sprint 1, Phase 6 / Task 27. Mirrors the wrapper-record + try/catch fallback
/// pattern established by <c>GetActiveRulebookAnalysisQueryHandler</c>.
/// </remarks>
internal sealed class GetGoldenForGameQueryHandler
    : IQueryHandler<GetGoldenForGameQuery, GoldenForGameDto>
{
    /// <summary>
    /// Wrapper required because <see cref="IHybridCacheService.GetOrCreateAsync{T}"/> has a
    /// <c>where T : class</c> constraint and we want to cache empty-collection results too.
    /// Internal (not private) so unit tests can configure the cache mock with a typed
    /// <c>Func&lt;CancellationToken, Task&lt;CachedGoldenForGameResult&gt;&gt;</c> matcher.
    /// </summary>
    internal sealed record CachedGoldenForGameResult(GoldenForGameDto Value);

    private readonly IMechanicGoldenClaimRepository _claimRepository;
    private readonly IMechanicGoldenBggTagRepository _tagRepository;
    private readonly IHybridCacheService _cache;
    private readonly ILogger<GetGoldenForGameQueryHandler> _logger;

    public GetGoldenForGameQueryHandler(
        IMechanicGoldenClaimRepository claimRepository,
        IMechanicGoldenBggTagRepository tagRepository,
        IHybridCacheService cache,
        ILogger<GetGoldenForGameQueryHandler> logger)
    {
        _claimRepository = claimRepository ?? throw new ArgumentNullException(nameof(claimRepository));
        _tagRepository = tagRepository ?? throw new ArgumentNullException(nameof(tagRepository));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<GoldenForGameDto> Handle(
        GetGoldenForGameQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        _logger.LogInformation("Loading golden set for game {SharedGameId}", query.SharedGameId);

        var cacheKey = $"meepleai:mechanic-golden:{query.SharedGameId}";
        var cacheTags = new[] { $"game:{query.SharedGameId}", "mechanic-golden" };

        try
        {
            var cached = await _cache.GetOrCreateAsync(
                cacheKey,
                async ct =>
                {
                    var dto = await BuildDtoAsync(query.SharedGameId, ct).ConfigureAwait(false);
                    return new CachedGoldenForGameResult(dto);
                },
                cacheTags,
                TimeSpan.FromMinutes(10),
                cancellationToken).ConfigureAwait(false);

            return cached.Value;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Cache operation failed for game {SharedGameId}. Falling back to direct DB query.",
                query.SharedGameId);

            return await BuildDtoAsync(query.SharedGameId, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<GoldenForGameDto> BuildDtoAsync(Guid sharedGameId, CancellationToken cancellationToken)
    {
        var claims = await _claimRepository.GetByGameAsync(sharedGameId, cancellationToken).ConfigureAwait(false);
        var tags = await _tagRepository.GetByGameAsync(sharedGameId, cancellationToken).ConfigureAwait(false);
        var versionHash = await _claimRepository.GetVersionHashAsync(sharedGameId, cancellationToken).ConfigureAwait(false);

        var claimDtos = claims.Select(MapClaim).ToList();
        var tagDtos = tags.Select(MapTag).ToList();

        return new GoldenForGameDto(
            sharedGameId,
            versionHash?.Value ?? string.Empty,
            claimDtos,
            tagDtos);
    }

    private static GoldenClaimDto MapClaim(MechanicGoldenClaim claim) =>
        new(
            claim.Id,
            claim.Section,
            claim.Statement,
            claim.ExpectedPage,
            claim.SourceQuote,
            claim.Keywords,
            claim.CreatedAt);

    private static BggTagDto MapTag(MechanicGoldenBggTag tag) =>
        new(tag.Name, tag.Category);
}
