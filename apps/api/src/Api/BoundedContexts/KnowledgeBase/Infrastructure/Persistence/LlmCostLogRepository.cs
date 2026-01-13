using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

/// <summary>
/// Repository implementation for LLM cost log persistence
/// ISSUE-960: BGAI-018 - Cost tracking database operations
/// </summary>
public class LlmCostLogRepository : ILlmCostLogRepository
{
    private readonly MeepleAiDbContext _context;
    private readonly ILogger<LlmCostLogRepository> _logger;

    public LlmCostLogRepository(
        MeepleAiDbContext context,
        ILogger<LlmCostLogRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task LogCostAsync(
        Guid? userId,
        string userRole,
        LlmCostCalculation cost,
        string endpoint,
        bool success,
        string? errorMessage,
        int latencyMs,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cost);
        var entity = new LlmCostLogEntity
        {
            UserId = userId,
            UserRole = userRole,
            ModelId = cost.ModelId,
            Provider = cost.Provider,
            PromptTokens = cost.PromptTokens,
            CompletionTokens = cost.CompletionTokens,
            TotalTokens = cost.TotalTokens,
            InputCost = cost.InputCost,
            OutputCost = cost.OutputCost,
            TotalCost = cost.TotalCost,
            Endpoint = endpoint,
            Success = success,
            ErrorMessage = errorMessage,
            LatencyMs = latencyMs,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CreatedAt = DateTime.UtcNow,
            RequestDate = DateOnly.FromDateTime(DateTime.UtcNow)
        };

        _context.LlmCostLogs.Add(entity);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogDebug(
            "Logged LLM cost: {Provider}/{Model} - {Tokens} tokens = ${Cost:F6} (user: {UserId}, role: {Role})",
            cost.Provider, cost.ModelId, cost.TotalTokens, cost.TotalCost, userId, userRole);
    }

    /// <inheritdoc/>
    public async Task<decimal> GetTotalCostAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        return await _context.LlmCostLogs
            .Where(x => x.RequestDate >= startDate && x.RequestDate <= endDate)
            .SumAsync(x => x.TotalCost, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IDictionary<string, decimal>> GetCostsByProviderAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        return await _context.LlmCostLogs
            .Where(x => x.RequestDate >= startDate && x.RequestDate <= endDate)
            .GroupBy(x => x.Provider)
            .Select(g => new { Provider = g.Key, TotalCost = g.Sum(x => x.TotalCost) })
            .ToDictionaryAsync(x => x.Provider, x => x.TotalCost, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IDictionary<string, decimal>> GetCostsByRoleAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        return await _context.LlmCostLogs
            .Where(x => x.RequestDate >= startDate && x.RequestDate <= endDate)
            .GroupBy(x => x.UserRole)
            .Select(g => new { Role = g.Key, TotalCost = g.Sum(x => x.TotalCost) })
            .ToDictionaryAsync(x => x.Role, x => x.TotalCost, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<decimal> GetUserCostAsync(
        Guid userId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        return await _context.LlmCostLogs
            .Where(x => x.UserId == userId && x.RequestDate >= startDate && x.RequestDate <= endDate)
            .SumAsync(x => x.TotalCost, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<decimal> GetDailyCostAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        return await _context.LlmCostLogs
            .Where(x => x.RequestDate == date)
            .SumAsync(x => x.TotalCost, cancellationToken).ConfigureAwait(false);
    }
}

