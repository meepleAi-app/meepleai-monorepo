using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

public class AgentFeedbackService
{
    private readonly MeepleAiDbContext _db;
    private readonly ILogger<AgentFeedbackService> _logger;

    public AgentFeedbackService(MeepleAiDbContext db, ILogger<AgentFeedbackService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task RecordFeedbackAsync(
        string messageId,
        string endpoint,
        string userId,
        string? outcome,
        string? gameId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(messageId))
        {
            throw new ArgumentException("messageId is required", nameof(messageId));
        }

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new ArgumentException("endpoint is required", nameof(endpoint));
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("userId is required", nameof(userId));
        }

        try
        {
            var existing = await _db.AgentFeedbacks
                .FirstOrDefaultAsync(f => f.MessageId == messageId && f.UserId == userId, ct);

            if (string.IsNullOrWhiteSpace(outcome))
            {
                if (existing != null)
                {
                    _db.AgentFeedbacks.Remove(existing);
                    await _db.SaveChangesAsync(ct);
                }

                return;
            }

            if (existing == null)
            {
                var entity = new AgentFeedbackEntity
                {
                    MessageId = messageId,
                    Endpoint = endpoint,
                    GameId = gameId,
                    UserId = userId,
                    Outcome = outcome,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _db.AgentFeedbacks.Add(entity);
            }
            else
            {
                existing.Endpoint = endpoint;
                existing.GameId = gameId;
                existing.Outcome = outcome;
                existing.UpdatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record feedback for message {MessageId}", messageId);
            throw;
        }
    }

    public async Task<AgentFeedbackStats> GetStatsAsync(
        string? endpoint = null,
        string? userId = null,
        string? gameId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken ct = default)
    {
        var query = _db.AgentFeedbacks.AsQueryable();

        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            query = query.Where(f => f.Endpoint == endpoint);
        }

        if (!string.IsNullOrWhiteSpace(userId))
        {
            query = query.Where(f => f.UserId == userId);
        }

        if (!string.IsNullOrWhiteSpace(gameId))
        {
            query = query.Where(f => f.GameId == gameId);
        }

        if (startDate.HasValue)
        {
            query = query.Where(f => f.CreatedAt >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(f => f.CreatedAt <= endDate.Value);
        }

        var totalFeedback = await query.CountAsync(ct);

        var outcomeCounts = await query
            .GroupBy(f => f.Outcome)
            .Select(g => new { Outcome = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var endpointOutcome = await query
            .GroupBy(f => new { f.Endpoint, f.Outcome })
            .Select(g => new { g.Key.Endpoint, g.Key.Outcome, Count = g.Count() })
            .ToListAsync(ct);

        var outcomeDict = outcomeCounts
            .ToDictionary(x => x.Outcome, x => x.Count, StringComparer.OrdinalIgnoreCase);

        var endpointDict = endpointOutcome
            .GroupBy(x => x.Endpoint)
            .ToDictionary(
                g => g.Key,
                g => g.ToDictionary(x => x.Outcome, x => x.Count, StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);

        return new AgentFeedbackStats
        {
            TotalFeedback = totalFeedback,
            OutcomeCounts = outcomeDict,
            EndpointOutcomeCounts = endpointDict
        };
    }
}

public record AgentFeedbackStats
{
    public int TotalFeedback { get; init; }
    public Dictionary<string, int> OutcomeCounts { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, Dictionary<string, int>> EndpointOutcomeCounts { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
