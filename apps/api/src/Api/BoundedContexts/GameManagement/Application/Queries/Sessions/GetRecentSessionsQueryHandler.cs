using Api.BoundedContexts.GameManagement.Application.Queries.Sessions;
using Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace Api.BoundedContexts.GameManagement.Application.Queries.Sessions;

/// <summary>
/// Handler for GetRecentSessionsQuery (Issue #4579)
/// Retrieves recent play records with caching
/// </summary>
internal class GetRecentSessionsQueryHandler : IRequestHandler<GetRecentSessionsQuery, List<SessionSummaryDto>>
{
    private readonly MeepleAiDbContext _context;
    private readonly HybridCache _cache;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public GetRecentSessionsQueryHandler(
        MeepleAiDbContext context,
        HybridCache cache,
        IHttpContextAccessor httpContextAccessor)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    public async Task<List<SessionSummaryDto>> Handle(GetRecentSessionsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = GetCurrentUserId();
        var cacheKey = $"recent-sessions:{userId}:{request.Limit}";

        return await _cache.GetOrCreateAsync(
            cacheKey,
            async cancel =>
            {
                var sessions = await _context.PlayRecords
                    .Where(pr => pr.CreatedByUserId == userId)
                    .OrderByDescending(pr => pr.SessionDate)
                    .Take(request.Limit)
                    .Select(pr => new SessionSummaryDto(
                        pr.Id,
                        pr.GameName,
                        pr.Game != null ? pr.Game.ImageUrl : null,
                        pr.SessionDate,
                        pr.Players.Count,
                        pr.Duration,
                        pr.Players.SelectMany(p => p.Scores).Any()
                            ? (int?)pr.Players.SelectMany(p => p.Scores).Average(s => s.Value)
                            : null,
                        pr.Players
                            .SelectMany(p => p.Scores)
                            .OrderByDescending(s => s.Value)
                            .Select(s => s.RecordPlayer.DisplayName)
                            .FirstOrDefault()
                    ))
                    .ToListAsync(cancel)
                    .ConfigureAwait(false);

                return sessions;
            },
            new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(2),
                LocalCacheExpiration = TimeSpan.FromMinutes(1)
            },
            cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private Guid GetCurrentUserId()
    {
        var httpContext = _httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("No HTTP context available");

        var userIdClaim = httpContext.User.FindFirst("sub")?.Value
            ?? httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? throw new UnauthorizedAccessException("User ID not found in claims");

        return Guid.Parse(userIdClaim);
    }
}
