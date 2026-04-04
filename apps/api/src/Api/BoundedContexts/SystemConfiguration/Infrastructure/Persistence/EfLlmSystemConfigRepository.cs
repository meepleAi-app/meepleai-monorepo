using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Api.Infrastructure.Entities.SystemConfiguration;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SystemConfiguration.Infrastructure.Persistence;

/// <summary>
/// Issue #5498: EF Core repository for LLM system configuration.
/// Single-row pattern — uses FirstOrDefault for reads, upsert for writes.
/// </summary>
public sealed class EfLlmSystemConfigRepository : RepositoryBase, ILlmSystemConfigRepository
{

    public EfLlmSystemConfigRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<LlmSystemConfig?> GetCurrentAsync(CancellationToken ct = default)
    {
        var entity = await DbContext.LlmSystemConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task UpsertAsync(LlmSystemConfig config, CancellationToken ct = default)
    {
        var existing = await DbContext.LlmSystemConfigs
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (existing != null)
        {
            existing.CircuitBreakerFailureThreshold = config.CircuitBreakerFailureThreshold;
            existing.CircuitBreakerOpenDurationSeconds = config.CircuitBreakerOpenDurationSeconds;
            existing.CircuitBreakerSuccessThreshold = config.CircuitBreakerSuccessThreshold;
            existing.DailyBudgetUsd = config.DailyBudgetUsd;
            existing.MonthlyBudgetUsd = config.MonthlyBudgetUsd;
            existing.FallbackChainJson = config.FallbackChainJson;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedByUserId = config.UpdatedByUserId;
        }
        else
        {
            DbContext.LlmSystemConfigs.Add(MapToEntity(config));
        }

        await DbContext.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private static LlmSystemConfig MapToDomain(LlmSystemConfigEntity entity)
    {
        // Use reflection to set private properties (same pattern as EfAiModelConfigurationRepository)
        var config = (LlmSystemConfig)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(LlmSystemConfig));

        typeof(LlmSystemConfig).GetProperty(nameof(LlmSystemConfig.Id))!.SetValue(config, entity.Id);
        typeof(LlmSystemConfig).GetProperty(nameof(LlmSystemConfig.CircuitBreakerFailureThreshold))!.SetValue(config, entity.CircuitBreakerFailureThreshold);
        typeof(LlmSystemConfig).GetProperty(nameof(LlmSystemConfig.CircuitBreakerOpenDurationSeconds))!.SetValue(config, entity.CircuitBreakerOpenDurationSeconds);
        typeof(LlmSystemConfig).GetProperty(nameof(LlmSystemConfig.CircuitBreakerSuccessThreshold))!.SetValue(config, entity.CircuitBreakerSuccessThreshold);
        typeof(LlmSystemConfig).GetProperty(nameof(LlmSystemConfig.DailyBudgetUsd))!.SetValue(config, entity.DailyBudgetUsd);
        typeof(LlmSystemConfig).GetProperty(nameof(LlmSystemConfig.MonthlyBudgetUsd))!.SetValue(config, entity.MonthlyBudgetUsd);
        typeof(LlmSystemConfig).GetProperty(nameof(LlmSystemConfig.FallbackChainJson))!.SetValue(config, entity.FallbackChainJson);
        typeof(LlmSystemConfig).GetProperty(nameof(LlmSystemConfig.CreatedAt))!.SetValue(config, entity.CreatedAt);
        typeof(LlmSystemConfig).GetProperty(nameof(LlmSystemConfig.UpdatedAt))!.SetValue(config, entity.UpdatedAt);
        typeof(LlmSystemConfig).GetProperty(nameof(LlmSystemConfig.UpdatedByUserId))!.SetValue(config, entity.UpdatedByUserId);

        return config;
    }

    private static LlmSystemConfigEntity MapToEntity(LlmSystemConfig config)
    {
        return new LlmSystemConfigEntity
        {
            Id = config.Id,
            CircuitBreakerFailureThreshold = config.CircuitBreakerFailureThreshold,
            CircuitBreakerOpenDurationSeconds = config.CircuitBreakerOpenDurationSeconds,
            CircuitBreakerSuccessThreshold = config.CircuitBreakerSuccessThreshold,
            DailyBudgetUsd = config.DailyBudgetUsd,
            MonthlyBudgetUsd = config.MonthlyBudgetUsd,
            FallbackChainJson = config.FallbackChainJson,
            CreatedAt = config.CreatedAt,
            UpdatedAt = config.UpdatedAt,
            UpdatedByUserId = config.UpdatedByUserId
        };
    }
}
