using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

public class AiRequestLogService
{
    private readonly MeepleAiDbContext _db;
    private readonly ILogger<AiRequestLogService> _logger;
    public AiRequestLogService(MeepleAiDbContext db, ILogger<AiRequestLogService> logger)
    {
        _db = db;
        _logger = logger;
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
        int? promptTokens = null,
        int? completionTokens = null,
        string? model = null,
        string? finishReason = null,
        string? apiKeyId = null,
        CancellationToken ct = default)
    {
        try
        {
            var log = new AiRequestLogEntity
            {
                UserId = userId,
                ApiKeyId = apiKeyId,
                GameId = gameId,
                Endpoint = endpoint,
                Query = query,
                ResponseSnippet = responseSnippet,
                LatencyMs = latencyMs,
                TokenCount = tokenCount ?? 0,
                PromptTokens = promptTokens ?? 0,
                CompletionTokens = completionTokens ?? 0,
                Confidence = confidence,
                Status = status,
                ErrorMessage = errorMessage,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                Model = model,
                FinishReason = finishReason,
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

    public async Task<AiRequestListResult> GetRequestsAsync(
        int limit = 100,
        int offset = 0,
        string? endpoint = null,
        string? userId = null,
        string? gameId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken ct = default)
    {
        var query = _db.AiRequestLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            query = query.Where(log => log.Endpoint == endpoint);
        }

        if (!string.IsNullOrWhiteSpace(userId))
        {
            query = query.Where(log => log.UserId == userId);
        }

        if (!string.IsNullOrWhiteSpace(gameId))
        {
            query = query.Where(log => log.GameId == gameId);
        }

        if (startDate.HasValue)
        {
            query = query.Where(log => log.CreatedAt >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(log => log.CreatedAt <= endDate.Value);
        }

        // Get total count before pagination
        var totalCount = await query.CountAsync(ct);

        // Get paginated results
        var requests = await query
            .OrderByDescending(log => log.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(ct);

        return new AiRequestListResult
        {
            Requests = requests,
            TotalCount = totalCount
        };
    }

    public async Task<AiRequestStats> GetStatsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? userId = null,
        string? gameId = null,
        CancellationToken ct = default)
    {
        var query = _db.AiRequestLogs.AsQueryable();

        if (startDate.HasValue)
        {
            query = query.Where(log => log.CreatedAt >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(log => log.CreatedAt <= endDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(userId))
        {
            query = query.Where(log => log.UserId == userId);
        }

        if (!string.IsNullOrWhiteSpace(gameId))
        {
            query = query.Where(log => log.GameId == gameId);
        }

        // PERF-03: Single aggregate query instead of 4 separate queries
        var stats = await query
            .GroupBy(_ => 1) // Group all into single aggregate
            .Select(g => new
            {
                TotalRequests = g.Count(),
                AvgLatencyMs = g.Average(log => (double?)log.LatencyMs) ?? 0,
                TotalTokens = g.Sum(log => log.TokenCount),
                SuccessCount = g.Count(log => log.Status == "Success")
            })
            .FirstOrDefaultAsync(ct);

        var endpointCounts = await query
            .GroupBy(log => log.Endpoint)
            .Select(g => new { Endpoint = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        if (stats == null)
        {
            // No data found - return empty stats
            return new AiRequestStats
            {
                TotalRequests = 0,
                AvgLatencyMs = 0,
                TotalTokens = 0,
                SuccessRate = 0,
                EndpointCounts = new Dictionary<string, int>()
            };
        }

        return new AiRequestStats
        {
            TotalRequests = stats.TotalRequests,
            AvgLatencyMs = stats.AvgLatencyMs,
            TotalTokens = stats.TotalTokens,
            SuccessRate = stats.TotalRequests > 0 ? (double)stats.SuccessCount / stats.TotalRequests : 0,
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

public record AiRequestListResult
{
    public List<AiRequestLogEntity> Requests { get; init; } = new();
    public int TotalCount { get; init; }
}
