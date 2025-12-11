using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Authentication.Application.Queries;

/// <summary>
/// Handler for GetApiKeyUsageStatsQuery.
/// Calculates comprehensive usage statistics for an API key.
/// </summary>
public class GetApiKeyUsageStatsQueryHandler : IQueryHandler<GetApiKeyUsageStatsQuery, ApiKeyUsageStatsDto?>
{
    private readonly MeepleAiDbContext _db;
    private readonly IApiKeyUsageLogRepository _usageLogRepository;
    private readonly TimeProvider _timeProvider;

    public GetApiKeyUsageStatsQueryHandler(
        MeepleAiDbContext db,
        IApiKeyUsageLogRepository usageLogRepository,
        TimeProvider? timeProvider = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _usageLogRepository = usageLogRepository ?? throw new ArgumentNullException(nameof(usageLogRepository));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<ApiKeyUsageStatsDto?> Handle(
        GetApiKeyUsageStatsQuery request,
        CancellationToken cancellationToken)
    {
        // Get API key and verify ownership
        var apiKey = await _db.ApiKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.Id == request.KeyId && k.UserId == request.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (apiKey == null)
        {
            return null;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var last24Hours = now.AddDays(-1);
        var last7Days = now.AddDays(-7);
        var last30Days = now.AddDays(-30);

        // Get usage counts for different time periods
        var usageCount24h = await _usageLogRepository.GetUsageCountInRangeAsync(
            request.KeyId, last24Hours, now, cancellationToken).ConfigureAwait(false);

        var usageCount7d = await _usageLogRepository.GetUsageCountInRangeAsync(
            request.KeyId, last7Days, now, cancellationToken).ConfigureAwait(false);

        var usageCount30d = await _usageLogRepository.GetUsageCountInRangeAsync(
            request.KeyId, last30Days, now, cancellationToken).ConfigureAwait(false);

        // Calculate average requests per day over last 30 days
        var averagePerDay = usageCount30d / 30.0;

        return new ApiKeyUsageStatsDto
        {
            KeyId = apiKey.Id,
            TotalUsageCount = apiKey.UsageCount,
            LastUsedAt = apiKey.LastUsedAt,
            UsageCountLast24Hours = usageCount24h,
            UsageCountLast7Days = usageCount7d,
            UsageCountLast30Days = usageCount30d,
            AverageRequestsPerDay = Math.Round(averagePerDay, 2)
        };
    }
}
