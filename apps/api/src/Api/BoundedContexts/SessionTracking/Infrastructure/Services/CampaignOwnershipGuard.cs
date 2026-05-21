using Api.BoundedContexts.SessionTracking.Application.Services;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Observability;
using Microsoft.AspNetCore.Http;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Services;

/// <summary>
/// Request-scoped ownership verifier. Caches positive verification result in
/// <see cref="HttpContext.Items"/> keyed by <c>"campaign-owner:{campaignId}:{userId}"</c>
/// so the SSE pre-flight check + downstream handler call resolve to a single DB roundtrip
/// in the happy path. Negative outcomes (mismatch, missing campaign) are NOT cached so
/// that subsequent re-checks always observe authoritative state.
/// </summary>
/// <remarks>
/// Issue #1415. Used by <see cref="Api.Routing.GamebookPhotoEndpoints"/> (pre-flight) and
/// by <c>TranslateGamebookSegmentQueryHandler</c> (in-handler ownership check) to avoid
/// double-DB roundtrip while keeping a single source of truth for ownership semantics.
/// </remarks>
internal sealed class CampaignOwnershipGuard : ICampaignOwnershipGuard
{
    private const string CacheKeyPrefix = "campaign-owner:";
    private const int PreflightTimeoutSeconds = 2;

    private readonly IGamebookCampaignSessionRepository _campaigns;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CampaignOwnershipGuard(
        IGamebookCampaignSessionRepository campaigns,
        IHttpContextAccessor httpContextAccessor)
    {
        _campaigns = campaigns ?? throw new ArgumentNullException(nameof(campaigns));
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    public async Task AssertOwnedByAsync(Guid campaignId, Guid userId, CancellationToken cancellationToken)
    {
        var cacheKey = $"{CacheKeyPrefix}{campaignId}:{userId}";
        var items = _httpContextAccessor.HttpContext?.Items;

        if (items is not null && items.TryGetValue(cacheKey, out var cached) && cached is true)
        {
            return; // cache hit — ownership previously verified within this request
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(PreflightTimeoutSeconds));

        try
        {
            var campaign = await _campaigns.GetByIdAsync(campaignId, cts.Token).ConfigureAwait(false)
                ?? throw new NotFoundException($"Campaign {campaignId} not found");

            if (campaign.OwnerUserId != userId)
            {
                MeepleAiMetrics.RecordGamebookTranslationAuthzFailure("forbidden");
                throw new ForbiddenException("Forbidden");
            }

            if (items is not null)
            {
                items[cacheKey] = true;
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Linked CTS triggered by the 2-second timeout, NOT the caller's token
            MeepleAiMetrics.RecordGamebookTranslationPreflightTimeout();
            throw new TimeoutException(
                $"Campaign ownership check exceeded {PreflightTimeoutSeconds}s preflight timeout");
        }
    }
}
