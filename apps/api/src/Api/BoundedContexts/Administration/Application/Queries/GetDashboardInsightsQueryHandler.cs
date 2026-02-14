using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Caching.Hybrid;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Handler for GetDashboardInsightsQuery (Issue #3916).
/// Retrieves AI-powered insights with HybridCache for performance optimization.
/// </summary>
internal class GetDashboardInsightsQueryHandler : IQueryHandler<GetDashboardInsightsQuery, DashboardInsightsResponseDto>
{
    private readonly IAiInsightsService _insightsService;
    private readonly HybridCache _cache;
    private readonly ILogger<GetDashboardInsightsQueryHandler> _logger;

    public GetDashboardInsightsQueryHandler(
        IAiInsightsService insightsService,
        HybridCache cache,
        ILogger<GetDashboardInsightsQueryHandler> logger)
    {
        _insightsService = insightsService ?? throw new ArgumentNullException(nameof(insightsService));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DashboardInsightsResponseDto> Handle(
        GetDashboardInsightsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var cacheKey = $"dashboard-insights:{query.UserId}";

        try
        {
            // Try cache first (15min TTL)
            var cachedInsights = await _cache.GetOrCreateAsync(
                cacheKey,
                async ct => await _insightsService.GetInsightsAsync(query.UserId, ct).ConfigureAwait(false),
                new HybridCacheEntryOptions
                {
                    Expiration = TimeSpan.FromMinutes(15),
                    LocalCacheExpiration = TimeSpan.FromMinutes(5),
                    Flags = HybridCacheEntryFlags.DisableCompression
                },
                tags: ["dashboard-insights", $"user:{query.UserId}"],
                cancellationToken: cancellationToken).ConfigureAwait(false);

            // Map AiInsightsDto to DashboardInsightsResponseDto
            return new DashboardInsightsResponseDto(
                cachedInsights.Insights,
                cachedInsights.GeneratedAt,
                cachedInsights.NextRefresh);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to retrieve dashboard insights for user {UserId}",
                query.UserId);

            // Return empty insights on error (graceful degradation)
            return new DashboardInsightsResponseDto(
                Array.Empty<DashboardInsightDto>(),
                DateTime.UtcNow,
                DateTime.UtcNow.AddMinutes(15));
        }
    }
}
