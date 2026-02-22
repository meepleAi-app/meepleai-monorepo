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
            RequestedAt = now,
            ExpiresAt = now.Add(RetentionPeriod)
        };

        _context.LlmRequestLogs.Add(entity);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogDebug(
            "Logged LLM request: {Provider}/{Model}, source={Source}, tokens={Tokens}, cost=${Cost:F6}, latency={Latency}ms",
            provider, modelId, source, promptTokens + completionTokens, costUsd, latencyMs);
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
}
