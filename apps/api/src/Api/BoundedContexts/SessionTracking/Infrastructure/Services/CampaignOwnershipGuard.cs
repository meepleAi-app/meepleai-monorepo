using Api.BoundedContexts.SessionTracking.Application.Services;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Observability;
using Microsoft.AspNetCore.Http;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Services;

/// <summary>
/// Request-scoped ownership verifier. Caches positive verification result in
/// <see cref="HttpContext.Items"/> keyed by <c>"campaign-owner:{campaignId}:{userId}"</c>
/// so the SSE pre-flight check + downstream handler call resolve to a single
/// ownership-check DB roundtrip in the happy path (the handler still fetches the
/// campaign entity separately for mutation operations). Negative outcomes
/// (mismatch, missing campaign) are NOT cached so that subsequent re-checks
/// always observe authoritative state.
/// </summary>
/// <remarks>
/// Issue #1415. Used by <see cref="Api.Routing.GamebookPhotoEndpoints"/> (pre-flight) and
/// by <c>TranslateGamebookSegmentQueryHandler</c> (in-handler ownership check) to keep a
/// single source of truth for ownership semantics while eliminating the duplicate
/// ownership-check roundtrip.
/// </remarks>
internal sealed class CampaignOwnershipGuard : ICampaignOwnershipGuard
{
    private const string CacheKeyPrefix = "campaign-owner:";
    private static readonly TimeSpan DefaultPreflightTimeout = TimeSpan.FromSeconds(2);

    private readonly IGamebookCampaignSessionRepository _campaigns;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly TimeSpan _preflightTimeout;

    public CampaignOwnershipGuard(
        IGamebookCampaignSessionRepository campaigns,
        IHttpContextAccessor httpContextAccessor)
        : this(campaigns, httpContextAccessor, DefaultPreflightTimeout)
    {
    }

    // Internal ctor for tests: lets a fast timeout (e.g., 50ms) be injected so the
    // timeout path can be exercised without a 2-second wall-clock wait.
    internal CampaignOwnershipGuard(
        IGamebookCampaignSessionRepository campaigns,
        IHttpContextAccessor httpContextAccessor,
        TimeSpan preflightTimeout)
    {
        _campaigns = campaigns ?? throw new ArgumentNullException(nameof(campaigns));
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _preflightTimeout = preflightTimeout;
    }

    public async Task AssertOwnedByAsync(Guid campaignId, Guid userId, CancellationToken cancellationToken)
    {
        var cacheKey = $"{CacheKeyPrefix}{campaignId}:{userId}";
        var items = _httpContextAccessor.HttpContext?.Items;

        if (items is not null && items.TryGetValue(cacheKey, out var cached) && cached is true)
        {
            return; // cache hit — ownership previously verified within this request
        }

        // Issue #1415 code review fix: keep timeout CTS separate from the caller's linked
        // token so we can unambiguously identify the cancellation source. The earlier
        // implementation used a single linked CTS + `when (!cancellationToken.IsCancellationRequested)`
        // filter, which misclassified the rare simultaneous-cancel race as a client
        // disconnect instead of a server-side timeout (the preflight_timeout metric was
        // not incremented). With a dedicated `timeoutCts` we always observe the timeout
        // signal regardless of caller cancellation order.
        using var timeoutCts = new CancellationTokenSource(_preflightTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, timeoutCts.Token);

        try
        {
            var campaign = await _campaigns.GetByIdAsync(campaignId, linkedCts.Token).ConfigureAwait(false)
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
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            // Internal preflight timeout fired (independent of whether the caller token
            // also cancelled simultaneously) — always record the timeout metric + surface
            // a TimeoutException that ApiExceptionHandlerMiddleware maps to HTTP 504.
            MeepleAiMetrics.RecordGamebookTranslationPreflightTimeout();
            throw new TimeoutException(
                $"Campaign ownership check exceeded {_preflightTimeout.TotalSeconds}s preflight timeout");
        }
    }
}
