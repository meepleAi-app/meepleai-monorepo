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
        CancellationToken ct = default)
    {
        try
        {
            var log = new AiRequestLogEntity
            {
                UserId = userId,
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

    public async Task<List<AiRequestLogEntity>> GetRequestsAsync(
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

        return await query
            .OrderByDescending(log => log.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(ct);
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

        var totalRequests = await query.CountAsync(ct);
        var avgLatency = await query.AverageAsync(log => (double?)log.LatencyMs, ct) ?? 0;
        var totalTokens = await query.SumAsync(log => log.TokenCount, ct);
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
