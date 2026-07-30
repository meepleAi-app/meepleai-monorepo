using System;
using System.Threading;
using System.Threading.Tasks;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicExtractor;
using Api.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services.MechanicClaimInjection;

/// <summary>Cache wrapper so a "no card" result is cacheable too (<c>where T : class</c> on the cache API).</summary>
internal sealed record MechanicCardCacheEntry(PublishedMechanicCardDto? Card);

/// <summary>
/// Read-time + cache provider for the published mechanic card (spec §6.2, D2). Wraps the SharedGameCatalog
/// published query behind <see cref="IHybridCacheService"/> (stampede-safe) and swallows any failure so the
/// RAG path never aborts on a cross-BC fault (fail-open, mirror of IHouseRuleMatcher).
/// </summary>
internal sealed class MechanicCardProvider : IMechanicCardProvider
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    private readonly IMediator _mediator;
    private readonly IHybridCacheService _cache;
    private readonly ILogger<MechanicCardProvider> _logger;

    public MechanicCardProvider(IMediator mediator, IHybridCacheService cache, ILogger<MechanicCardProvider> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PublishedMechanicCardDto?> GetActiveCardAsync(Guid sharedGameId, CancellationToken cancellationToken)
    {
        if (sharedGameId == Guid.Empty)
        {
            return null;
        }

        _logger.LogDebug("[MechanicCardProvider] Fetching active card for game {GameId}", sharedGameId);

        try
        {
            var entry = await _cache.GetOrCreateAsync(
                $"mechanic-card:{sharedGameId}",
                async ct => new MechanicCardCacheEntry(
                    await _mediator.Send(new GetPublishedMechanicCardByGameQuery(sharedGameId), ct).ConfigureAwait(false)),
                tags: new[] { "mechanic-card", $"game:{sharedGameId}" },
                expiration: CacheTtl,
                ct: cancellationToken).ConfigureAwait(false);

            return entry.Card;
        }
        catch (Exception ex)
        {
            // Best-effort / fail-open (spec §9, D7): a cross-BC read fault must never abort the RAG path.
            _logger.LogWarning(ex, "[MechanicCardProvider] Best-effort read failed for game {GameId}", sharedGameId);
            return null;
        }
    }
}
