using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Authentication.Application.Queries;

/// <summary>
/// Handler for GetAllApiKeysWithStatsQuery.
/// Returns all API keys with comprehensive usage statistics (admin only).
/// </summary>
public class GetAllApiKeysWithStatsQueryHandler : IQueryHandler<GetAllApiKeysWithStatsQuery, List<ApiKeyWithStatsDto>>
{
    private readonly MeepleAiDbContext _db;
    private readonly IApiKeyUsageLogRepository _usageLogRepository;
    private readonly TimeProvider _timeProvider;

    public GetAllApiKeysWithStatsQueryHandler(
        MeepleAiDbContext db,
        IApiKeyUsageLogRepository usageLogRepository,
        TimeProvider? timeProvider = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _usageLogRepository = usageLogRepository ?? throw new ArgumentNullException(nameof(usageLogRepository));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<List<ApiKeyWithStatsDto>> Handle(
        GetAllApiKeysWithStatsQuery request,
        CancellationToken cancellationToken)
    {
        // Build query with optional filters
        var query = _db.ApiKeys.AsNoTracking();

        if (request.UserId.HasValue)
        {
            query = query.Where(k => k.UserId == request.UserId.Value);
        }

        if (!request.IncludeRevoked)
        {
            query = query.Where(k => k.RevokedAt == null);
        }

        var apiKeys = await query
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var last24Hours = now.AddDays(-1);
        var last7Days = now.AddDays(-7);
        var last30Days = now.AddDays(-30);

        var result = new List<ApiKeyWithStatsDto>();

        // Calculate stats for each API key
        foreach (var key in apiKeys)
        {
            var usageCount24h = await _usageLogRepository.GetUsageCountInRangeAsync(
                key.Id, last24Hours, now, cancellationToken).ConfigureAwait(false);

            var usageCount7d = await _usageLogRepository.GetUsageCountInRangeAsync(
                key.Id, last7Days, now, cancellationToken).ConfigureAwait(false);

            var usageCount30d = await _usageLogRepository.GetUsageCountInRangeAsync(
                key.Id, last30Days, now, cancellationToken).ConfigureAwait(false);

            var averagePerDay = usageCount30d / 30.0;

            result.Add(new ApiKeyWithStatsDto
            {
                ApiKey = new ApiKeyDto(
                    key.Id,
                    key.KeyName,
                    key.KeyPrefix,
                    key.Scopes,
                    key.CreatedAt,
                    key.ExpiresAt,
                    key.LastUsedAt,
                    key.IsActive
                ),
                UsageStats = new ApiKeyUsageStatsDto
                {
                    KeyId = key.Id,
                    TotalUsageCount = key.UsageCount,
                    LastUsedAt = key.LastUsedAt,
                    UsageCountLast24Hours = usageCount24h,
                    UsageCountLast7Days = usageCount7d,
                    UsageCountLast30Days = usageCount30d,
                    AverageRequestsPerDay = Math.Round(averagePerDay, 2)
                }
            });
        }

        return result;
    }
}
