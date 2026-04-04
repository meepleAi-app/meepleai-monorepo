using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

/// <summary>
/// Repository for model compatibility matrix and change log entries.
/// Issue #5496: Part of Epic #5490 - Model Versioning &amp; Availability Monitoring.
/// </summary>
internal sealed class ModelCompatibilityRepository : RepositoryBase, IModelCompatibilityRepository
{
    private readonly ILogger<ModelCompatibilityRepository> _logger;

    public ModelCompatibilityRepository(
        MeepleAiDbContext dbContext,
        IDomainEventCollector eventCollector,
        ILogger<ModelCompatibilityRepository> logger)
        : base(dbContext, eventCollector)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<ModelCompatibilityEntry?> GetByModelIdAsync(
        string modelId,
        CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Set<ModelCompatibilityEntryEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.ModelId == modelId, cancellationToken)
            .ConfigureAwait(false);

        if (entity == null)
        {
            _logger.LogDebug("No compatibility entry found for model {ModelId}", modelId);
            return null;
        }

        return MapToEntry(entity);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ModelCompatibilityEntry>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var entries = await DbContext.Set<ModelCompatibilityEntryEntity>()
            .AsNoTracking()
            .OrderBy(e => e.Provider)
            .ThenBy(e => e.ModelId)
            .Select(e => new ModelCompatibilityEntry(
                e.Id,
                e.ModelId,
                e.DisplayName,
                e.Provider,
                e.Alternatives,
                e.ContextWindow,
                e.Strengths,
                e.IsCurrentlyAvailable,
                e.IsDeprecated,
                e.LastVerifiedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        _logger.LogDebug("Retrieved {Count} model compatibility entries", entries.Count);
        return entries;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ModelCompatibilityEntry>> GetAvailableByProviderAsync(
        string provider,
        CancellationToken cancellationToken = default)
    {
        var entries = await DbContext.Set<ModelCompatibilityEntryEntity>()
            .AsNoTracking()
            .Where(e => e.Provider == provider && e.IsCurrentlyAvailable && !e.IsDeprecated)
            .OrderBy(e => e.ModelId)
            .Select(e => new ModelCompatibilityEntry(
                e.Id,
                e.ModelId,
                e.DisplayName,
                e.Provider,
                e.Alternatives,
                e.ContextWindow,
                e.Strengths,
                e.IsCurrentlyAvailable,
                e.IsDeprecated,
                e.LastVerifiedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        _logger.LogDebug("Retrieved {Count} available models for provider {Provider}", entries.Count, provider);
        return entries;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ModelCompatibilityEntry>> GetUnavailableModelsAsync(
        CancellationToken cancellationToken = default)
    {
        var entries = await DbContext.Set<ModelCompatibilityEntryEntity>()
            .AsNoTracking()
            .Where(e => !e.IsCurrentlyAvailable || e.IsDeprecated)
            .OrderByDescending(e => e.UpdatedAt)
            .Select(e => new ModelCompatibilityEntry(
                e.Id,
                e.ModelId,
                e.DisplayName,
                e.Provider,
                e.Alternatives,
                e.ContextWindow,
                e.Strengths,
                e.IsCurrentlyAvailable,
                e.IsDeprecated,
                e.LastVerifiedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        _logger.LogDebug("Retrieved {Count} unavailable/deprecated models", entries.Count);
        return entries;
    }

    /// <inheritdoc/>
    public async Task UpdateAvailabilityAsync(
        string modelId,
        bool isAvailable,
        bool isDeprecated,
        CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Set<ModelCompatibilityEntryEntity>()
            .FirstOrDefaultAsync(e => e.ModelId == modelId, cancellationToken)
            .ConfigureAwait(false);

        if (entity == null)
        {
            _logger.LogWarning("Cannot update availability for unknown model {ModelId}", modelId);
            return;
        }

        entity.IsCurrentlyAvailable = isAvailable;
        entity.IsDeprecated = isDeprecated;
        entity.LastVerifiedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;

        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Updated model {ModelId} availability: available={IsAvailable}, deprecated={IsDeprecated}",
            modelId, isAvailable, isDeprecated);
    }

    /// <inheritdoc/>
    public async Task UpsertAsync(
        ModelCompatibilityEntry entry,
        CancellationToken cancellationToken = default)
    {
        var existing = await DbContext.Set<ModelCompatibilityEntryEntity>()
            .FirstOrDefaultAsync(e => e.ModelId == entry.ModelId, cancellationToken)
            .ConfigureAwait(false);

        if (existing != null)
        {
            existing.DisplayName = entry.DisplayName;
            existing.Provider = entry.Provider;
            existing.Alternatives = entry.Alternatives;
            existing.ContextWindow = entry.ContextWindow;
            existing.Strengths = entry.Strengths;
            existing.IsCurrentlyAvailable = entry.IsCurrentlyAvailable;
            existing.IsDeprecated = entry.IsDeprecated;
            existing.LastVerifiedAt = entry.LastVerifiedAt;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            var entity = new ModelCompatibilityEntryEntity
            {
                Id = entry.Id,
                ModelId = entry.ModelId,
                DisplayName = entry.DisplayName,
                Provider = entry.Provider,
                Alternatives = entry.Alternatives,
                ContextWindow = entry.ContextWindow,
                Strengths = entry.Strengths,
                IsCurrentlyAvailable = entry.IsCurrentlyAvailable,
                IsDeprecated = entry.IsDeprecated,
                LastVerifiedAt = entry.LastVerifiedAt,
            };
            DbContext.Set<ModelCompatibilityEntryEntity>().Add(entity);
        }

        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Upserted model compatibility entry for {ModelId} ({Action})",
            entry.ModelId, existing != null ? "updated" : "created");
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ModelChangeLogEntry>> GetChangeHistoryAsync(
        string? modelId = null,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var query = DbContext.Set<ModelChangeLogEntity>()
            .AsNoTracking()
            .AsQueryable();

        if (modelId != null)
        {
            query = query.Where(e => e.ModelId == modelId);
        }

        var entries = await query
            .OrderByDescending(e => e.OccurredAt)
            .Take(limit)
            .Select(e => new ModelChangeLogEntry(
                e.Id,
                e.ModelId,
                e.ChangeType,
                e.PreviousModelId,
                e.NewModelId,
                e.AffectedStrategy,
                e.Reason,
                e.IsAutomatic,
                e.ChangedByUserId,
                e.OccurredAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        _logger.LogDebug("Retrieved {Count} model change log entries", entries.Count);
        return entries;
    }

    /// <inheritdoc/>
    public async Task LogChangeAsync(
        ModelChangeLogEntry entry,
        CancellationToken cancellationToken = default)
    {
        var entity = new ModelChangeLogEntity
        {
            Id = entry.Id,
            ModelId = entry.ModelId,
            ChangeType = entry.ChangeType,
            PreviousModelId = entry.PreviousModelId,
            NewModelId = entry.NewModelId,
            AffectedStrategy = entry.AffectedStrategy,
            Reason = entry.Reason,
            IsAutomatic = entry.IsAutomatic,
            ChangedByUserId = entry.ChangedByUserId,
            OccurredAt = entry.OccurredAt,
        };

        DbContext.Set<ModelChangeLogEntity>().Add(entity);
        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Logged model change: {ChangeType} for {ModelId} (strategy: {Strategy})",
            entry.ChangeType, entry.ModelId, entry.AffectedStrategy ?? "N/A");
    }

    private static ModelCompatibilityEntry MapToEntry(ModelCompatibilityEntryEntity entity)
    {
        return new ModelCompatibilityEntry(
            entity.Id,
            entity.ModelId,
            entity.DisplayName,
            entity.Provider,
            entity.Alternatives,
            entity.ContextWindow,
            entity.Strengths,
            entity.IsCurrentlyAvailable,
            entity.IsDeprecated,
            entity.LastVerifiedAt);
    }
}
