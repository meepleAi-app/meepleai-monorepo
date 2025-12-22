using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Authentication.Infrastructure.Persistence;

/// <summary>
/// Repository implementation for ApiKeyUsageLog aggregate.
/// Handles persistence of API key usage logs.
/// </summary>
internal class ApiKeyUsageLogRepository : IApiKeyUsageLogRepository
{
    private readonly MeepleAiDbContext _context;

    public ApiKeyUsageLogRepository(MeepleAiDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public async Task AddAsync(ApiKeyUsageLog usageLog, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(usageLog);

        var entity = new ApiKeyUsageLogEntity
        {
            Id = usageLog.Id,
            KeyId = usageLog.KeyId,
            UsedAt = usageLog.UsedAt,
            Endpoint = usageLog.Endpoint,
            IpAddress = usageLog.IpAddress,
            UserAgent = usageLog.UserAgent,
            HttpMethod = usageLog.HttpMethod,
            StatusCode = usageLog.StatusCode,
            ResponseTimeMs = usageLog.ResponseTimeMs,
            ApiKey = null! // Navigation property, will be resolved by EF Core
        };

        await _context.ApiKeyUsageLogs.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<ApiKeyUsageLog>> GetByKeyIdAsync(
        Guid keyId,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        var entities = await _context.ApiKeyUsageLogs
            .AsNoTracking()
            .Where(e => e.KeyId == keyId)
            .OrderByDescending(e => e.UsedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<List<ApiKeyUsageLog>> GetByKeyIdAndDateRangeAsync(
        Guid keyId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default)
    {
        var entities = await _context.ApiKeyUsageLogs
            .AsNoTracking()
            .Where(e => e.KeyId == keyId && e.UsedAt >= fromDate && e.UsedAt <= toDate)
            .OrderByDescending(e => e.UsedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<int> GetUsageCountAsync(Guid keyId, CancellationToken cancellationToken = default)
    {
        return await _context.ApiKeyUsageLogs
            .AsNoTracking()
            .CountAsync(e => e.KeyId == keyId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> GetUsageCountInRangeAsync(
        Guid keyId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default)
    {
        return await _context.ApiKeyUsageLogs
            .AsNoTracking()
            .CountAsync(
                e => e.KeyId == keyId && e.UsedAt >= fromDate && e.UsedAt <= toDate,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> DeleteOlderThanAsync(DateTime cutoffDate, CancellationToken cancellationToken = default)
    {
        return await _context.ApiKeyUsageLogs
            .Where(e => e.UsedAt < cutoffDate)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static ApiKeyUsageLog MapToDomain(ApiKeyUsageLogEntity entity)
    {
        return ApiKeyUsageLog.Create(
            entity.Id,
            entity.KeyId,
            entity.Endpoint,
            entity.IpAddress,
            entity.UserAgent,
            entity.HttpMethod,
            entity.StatusCode,
            entity.ResponseTimeMs,
            entity.UsedAt);
    }
}
