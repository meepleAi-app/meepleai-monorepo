using System.Security.Cryptography;
using System.Text;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

/// <summary>
/// Repository implementation for LLM request log persistence.
/// Issue #5072: OpenRouter monitoring — request log with 30-day retention.
/// Issues #5078-#5083: Analytics query methods for admin usage dashboard.
/// </summary>
public sealed class LlmRequestLogRepository : ILlmRequestLogRepository
{
    private static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(30);

    private readonly MeepleAiDbContext _context;
    private readonly ILogger<LlmRequestLogRepository> _logger;

    public LlmRequestLogRepository(
        MeepleAiDbContext context,
        ILogger<LlmRequestLogRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task LogRequestAsync(
        string modelId,
        string provider,
        RequestSource source,
        Guid? userId,
        string? userRole,
        int promptTokens,
        int completionTokens,
        decimal costUsd,
        int latencyMs,
        bool success,
        string? errorMessage,
        bool isStreaming,
        bool isFreeModel,
        string? sessionId,
        string? userRegion = null,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var entity = new LlmRequestLogEntity
        {
            ModelId = modelId,
            Provider = provider,
            RequestSource = source.ToString(),
            UserId = userId,
            UserRole = userRole,
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens,
            TotalTokens = promptTokens + completionTokens,
            CostUsd = costUsd,
            LatencyMs = latencyMs,
            Success = success,
            ErrorMessage = errorMessage,
            IsStreaming = isStreaming,
            IsFreeModel = isFreeModel,
            SessionId = sessionId,
            UserRegion = userRegion,
            RequestedAt = now,
            ExpiresAt = now.Add(RetentionPeriod)
        };

        _context.LlmRequestLogs.Add(entity);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogDebug(
            "Logged LLM request: {Provider}/{Model}, source={Source}, tokens={Tokens}, cost=${Cost:F6}, latency={Latency}ms",
            provider, modelId, source, promptTokens + completionTokens, costUsd, latencyMs);
    }

    public async Task<int> GetActiveAiUserCountAsync(DateTime from, CancellationToken cancellationToken = default)
    {
        return await _context.LlmRequestLogs
            .AsNoTracking()
            .Where(x => x.RequestedAt >= from && x.UserId != null && !x.IsAnonymized)
            .Select(x => x.UserId)
            .Distinct()
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> GetTodayCountAsync(DateOnly utcDate, CancellationToken cancellationToken = default)
    {
        var startUtc = utcDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endUtc = utcDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        return await _context.LlmRequestLogs
            .CountAsync(x => x.RequestedAt >= startUtc && x.RequestedAt <= endUtc, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> DeleteExpiredAsync(DateTime cutoff, CancellationToken cancellationToken = default)
    {
        var deleted = await _context.LlmRequestLogs
            .Where(x => x.ExpiresAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        if (deleted > 0)
        {
            _logger.LogInformation(
                "Deleted {Count} expired LLM request logs (cutoff: {Cutoff})",
                deleted, cutoff);
        }

        return deleted;
    }

    public async Task<int> DeleteByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var deleted = await _context.LlmRequestLogs
            .Where(x => x.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        if (deleted > 0)
        {
            _logger.LogInformation(
                "Deleted {Count} LLM request logs for user {UserId} (GDPR erasure)",
                deleted, userId);
        }

        return deleted;
    }

    public async Task<IReadOnlyList<(DateTime Bucket, string Source, int Count, decimal CostUsd)>> GetTimelineAsync(
        DateTime from,
        DateTime until,
        bool groupByHour,
        CancellationToken cancellationToken = default)
    {
        var rows = await _context.LlmRequestLogs
            .AsNoTracking()
            .Where(x => x.RequestedAt >= from && x.RequestedAt <= until)
            .Select(x => new
            {
                x.RequestedAt,
                x.RequestSource,
                x.CostUsd
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var result = rows
            .GroupBy(x => new
            {
                Bucket = groupByHour
                    ? new DateTime(x.RequestedAt.Year, x.RequestedAt.Month, x.RequestedAt.Day,
                                   x.RequestedAt.Hour, 0, 0, DateTimeKind.Utc)
                    : new DateTime(x.RequestedAt.Year, x.RequestedAt.Month, x.RequestedAt.Day,
                                   0, 0, 0, DateTimeKind.Utc),
                x.RequestSource
            })
            .Select(g => (
                Bucket: g.Key.Bucket,
                Source: g.Key.RequestSource,
                Count: g.Count(),
                CostUsd: g.Sum(x => x.CostUsd)
            ))
            .OrderBy(x => x.Bucket)
            .ThenBy(x => x.Source, StringComparer.Ordinal)
            .ToList();

        return result;
    }

    public async Task<(
        IReadOnlyList<(string ModelId, decimal CostUsd, int Requests, int TotalTokens)> ByModel,
        IReadOnlyList<(string Source, decimal CostUsd, int Requests)> BySource,
        IReadOnlyList<(string Tier, decimal CostUsd, int Requests)> ByTier,
        decimal TotalCostUsd,
        int TotalRequests
    )> GetCostBreakdownAsync(
        DateTime from,
        DateTime until,
        CancellationToken cancellationToken = default)
    {
        var rows = await _context.LlmRequestLogs
            .AsNoTracking()
            .Where(x => x.RequestedAt >= from && x.RequestedAt <= until)
            .Select(x => new
            {
                x.ModelId,
                x.RequestSource,
                x.UserRole,
                x.CostUsd,
                x.TotalTokens
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var byModel = rows
            .GroupBy(x => x.ModelId, StringComparer.Ordinal)
            .Select(g => (
                ModelId: g.Key,
                CostUsd: g.Sum(x => x.CostUsd),
                Requests: g.Count(),
                TotalTokens: g.Sum(x => x.TotalTokens)
            ))
            .OrderByDescending(x => x.CostUsd)
            .ToList<(string ModelId, decimal CostUsd, int Requests, int TotalTokens)>();

        var bySource = rows
            .GroupBy(x => x.RequestSource, StringComparer.Ordinal)
            .Select(g => (
                Source: g.Key,
                CostUsd: g.Sum(x => x.CostUsd),
                Requests: g.Count()
            ))
            .OrderByDescending(x => x.CostUsd)
            .ToList<(string Source, decimal CostUsd, int Requests)>();

        var byTier = rows
            .GroupBy(x => x.UserRole ?? "Unknown", StringComparer.Ordinal)
            .Select(g => (
                Tier: g.Key,
                CostUsd: g.Sum(x => x.CostUsd),
                Requests: g.Count()
            ))
            .OrderByDescending(x => x.CostUsd)
            .ToList<(string Tier, decimal CostUsd, int Requests)>();

        var totalCostUsd = rows.Sum(x => x.CostUsd);
        var totalRequests = rows.Count;

        return (byModel, bySource, byTier, totalCostUsd, totalRequests);
    }

    public async Task<IReadOnlyList<(string ModelId, int RequestsToday)>> GetFreeModelUsageAsync(
        DateOnly forDate,
        CancellationToken cancellationToken = default)
    {
        var startUtc = forDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endUtc = forDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        var rows = await _context.LlmRequestLogs
            .AsNoTracking()
            .Where(x => x.IsFreeModel
                     && x.RequestedAt >= startUtc
                     && x.RequestedAt <= endUtc)
            .Select(x => x.ModelId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .GroupBy(modelId => modelId, StringComparer.Ordinal)
            .Select(g => (ModelId: g.Key, RequestsToday: g.Count()))
            .OrderByDescending(x => x.RequestsToday)
            .ToList();
    }

    public async Task<int> PseudonymizeOldLogsAsync(DateTime cutoff, string salt, CancellationToken cancellationToken = default)
    {
        // Batch processing to avoid loading too many entities at once
        const int batchSize = 500;
        var totalPseudonymized = 0;

        while (true)
        {
            var batch = await _context.LlmRequestLogs
                .Where(x => x.RequestedAt < cutoff
                          && !x.IsAnonymized
                          && x.UserId != null)
                .Take(batchSize)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (batch.Count == 0)
                break;

            foreach (var log in batch)
            {
                var input = $"{log.UserId}{salt}";
                var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
                // Store the hash as a deterministic GUID (first 16 bytes of SHA-256)
                log.UserId = new Guid(hashBytes.AsSpan(0, 16));
                log.IsAnonymized = true;
            }

            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            totalPseudonymized += batch.Count;
        }

        if (totalPseudonymized > 0)
        {
            _logger.LogInformation(
                "Pseudonymized {Count} LLM request logs older than {Cutoff}",
                totalPseudonymized, cutoff);
        }

        return totalPseudonymized;
    }

    public async Task<(IReadOnlyList<LlmRequestLogEntity> Items, int Total)> GetPagedAsync(
        string? source,
        string? model,
        DateTime? from,
        DateTime? until,
        bool? successOnly,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.LlmRequestLogs.AsNoTracking();

        if (!string.IsNullOrEmpty(source))
            query = query.Where(x => x.RequestSource == source);

        if (!string.IsNullOrEmpty(model))
            query = query.Where(x => EF.Functions.ILike(x.ModelId, $"%{model}%"));

        if (from.HasValue)
            query = query.Where(x => x.RequestedAt >= from.Value);

        if (until.HasValue)
            query = query.Where(x => x.RequestedAt <= until.Value);

        if (successOnly.HasValue)
            query = query.Where(x => x.Success == successOnly.Value);

        var total = await query
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = await query
            .OrderByDescending(x => x.RequestedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (items, total);
    }
}
