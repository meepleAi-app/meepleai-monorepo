using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// Issue #3620 — regression guard for the "expired cache serves an expired presign"
/// bug: <see cref="GetSharedGameByIdQueryHandler"/> and
/// <see cref="SearchSharedGamesQueryHandler"/> both resolve a game's cover URL
/// INSIDE their <c>HybridCache.GetOrCreateAsync</c> factory, so the resolved
/// presigned URL is baked verbatim into the cached DTO for the entire cache-entry
/// lifetime. If the presigned URL's own expiry is shorter than (or too close to) the
/// cache TTL, a client can be served an already-dead presign on a later cache hit —
/// the FE (<c>Cover.tsx</c>'s <c>onError</c>) just silently swaps in the placeholder,
/// so the failure mode has no visible error, only a cover that "randomly" disappears.
///
/// This test reads every value from its named source-of-truth constant — never a
/// number copied by hand — so it fails mechanically the moment either side drifts:
/// raising a cache TTL above the presign expiry, or lowering the presign expiry
/// below a cache TTL, both trip it. See the "before/after" evidence for an induced
/// failure in <c>issue-3620-report.md</c>.
/// </summary>
public class CoverPresignCacheInvariantTests
{
    [Fact]
    public void CoverPresignExpiry_ExceedsEveryCachingSurfaceL2Ttl_WithExplicitMargin()
    {
        // The two cache surfaces known to bake a resolved cover URL into their cached
        // payload (see the CLAUDE.md / issue #3620 audit — GetFilteredSharedGames,
        // GetAllSharedGames, GetPendingApprovalGames and GetUserLibrary all resolve
        // covers OUTSIDE any HybridCache.GetOrCreateAsync, so they are not part of
        // this invariant).
        var cachingSurfaceL2Ttls = new[]
        {
            GetSharedGameByIdQueryHandler.DetailCacheL2Expiration,
            SearchSharedGamesQueryHandler.SearchCacheL2Expiration,
        };

        var longestCacheTtl = cachingSurfaceL2Ttls.Max();
        var presignExpiry = TimeSpan.FromSeconds(CoverUrlResolver.CoverPresignExpirySeconds);

        presignExpiry.Should().BeGreaterThan(longestCacheTtl,
            "cached DTOs embed a presigned cover URL resolved once at cache-miss time; " +
            "if the presign expires before the cache entry does, a later cache hit serves " +
            "an already-dead URL");

        // Explicit margin (not just "greater than"): a presign expiry that beats the
        // longest cache TTL by a handful of seconds is still one accidental TTL bump
        // away from reopening the gap. Today: 4h presign - 2h longest cache TTL = 2h
        // margin.
        var margin = presignExpiry - longestCacheTtl;
        margin.Should().BeGreaterThanOrEqualTo(TimeSpan.FromHours(2),
            "the presign expiry must carry an explicit safety margin over the longest " +
            "cache TTL, not just barely outlive it");
    }
}
