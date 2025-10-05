using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Services;

public class AiRequestLogService
{
    private readonly MeepleAiDbContext _db;
    private readonly ILogger<AiRequestLogService> _logger;
    private readonly string _tenantId;

    public AiRequestLogService(MeepleAiDbContext db, ILogger<AiRequestLogService> logger, IOptions<SingleTenantOptions> tenantOptions)
    {
        _db = db;
        _logger = logger;
        _tenantId = (tenantOptions?.Value ?? new SingleTenantOptions()).GetTenantId();
    }

    public async Task LogRequestAsync(
        string? userId,
        string? gameId,
        string endpoint,
        string? query,
        string? responseSnippet,
        int latencyMs,
        int? tokenCount = null,
        double? confidence = null,
        string status = "Success",
        string? errorMessage = null,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken ct = default)
    {
        try
        {
            var log = new AiRequestLogEntity
            {
                TenantId = _tenantId,
                UserId = userId,
                GameId = gameId,
                Endpoint = endpoint,
                Query = query,
                ResponseSnippet = responseSnippet,
                LatencyMs = latencyMs,
                TokenCount = tokenCount,
                Confidence = confidence,
                Status = status,
                ErrorMessage = errorMessage,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                CreatedAt = DateTime.UtcNow
            };

            _db.AiRequestLogs.Add(log);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Don't fail the request if logging fails
            _logger.LogError(ex, "Failed to log AI request for endpoint {Endpoint}", endpoint);
        }
    }

    public async Task<List<AiRequestLogEntity>> GetRequestsAsync(
        int limit = 100,
        int offset = 0,
        string? endpoint = null,
        string? userId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken ct = default)
    {
        var query = _db.AiRequestLogs
            .Where(log => log.TenantId == _tenantId);

        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            query = query.Where(log => log.Endpoint == endpoint);
        }

        if (!string.IsNullOrWhiteSpace(userId))
        {
            query = query.Where(log => log.UserId == userId);
        }

        if (startDate.HasValue)
        {
            query = query.Where(log => log.CreatedAt >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(log => log.CreatedAt <= endDate.Value);
        }

        return await query
            .OrderByDescending(log => log.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<AiRequestStats> GetStatsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken ct = default)
    {
        var query = _db.AiRequestLogs
            .Where(log => log.TenantId == _tenantId);

        if (startDate.HasValue)
        {
            query = query.Where(log => log.CreatedAt >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(log => log.CreatedAt <= endDate.Value);
        }

        var totalRequests = await query.CountAsync(ct);
        var avgLatency = await query.AverageAsync(log => (double?)log.LatencyMs, ct) ?? 0;
        var totalTokens = await query.SumAsync(log => log.TokenCount ?? 0, ct);
        var successCount = await query.CountAsync(log => log.Status == "Success", ct);

        var endpointCounts = await query
            .GroupBy(log => log.Endpoint)
            .Select(g => new { Endpoint = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return new AiRequestStats
        {
            TotalRequests = totalRequests,
            AvgLatencyMs = avgLatency,
            TotalTokens = totalTokens,
            SuccessRate = totalRequests > 0 ? (double)successCount / totalRequests : 0,
            EndpointCounts = endpointCounts.ToDictionary(x => x.Endpoint, x => x.Count)
        };
    }
}

public record AiRequestStats
{
    public int TotalRequests { get; init; }
    public double AvgLatencyMs { get; init; }
    public int TotalTokens { get; init; }
    public double SuccessRate { get; init; }
    public Dictionary<string, int> EndpointCounts { get; init; } = new();
}
