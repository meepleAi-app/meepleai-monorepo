namespace Api.BoundedContexts.KnowledgeBase.Domain.Repositories;

/// <summary>
/// Repository for model compatibility matrix entries.
/// Issue #5496: Part of Epic #5490 - Model Versioning &amp; Availability Monitoring.
/// </summary>
public interface IModelCompatibilityRepository
{
    /// <summary>
    /// Gets a compatibility entry by model ID.
    /// </summary>
    Task<ModelCompatibilityEntry?> GetByModelIdAsync(string modelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all compatibility entries.
    /// </summary>
    Task<IReadOnlyList<ModelCompatibilityEntry>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all currently available models for a provider.
    /// </summary>
    Task<IReadOnlyList<ModelCompatibilityEntry>> GetAvailableByProviderAsync(string provider, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all models that are deprecated or unavailable.
    /// </summary>
    Task<IReadOnlyList<ModelCompatibilityEntry>> GetUnavailableModelsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the availability status of a model.
    /// </summary>
    Task UpdateAvailabilityAsync(string modelId, bool isAvailable, bool isDeprecated, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds or updates a model compatibility entry (upsert by ModelId).
    /// </summary>
    Task UpsertAsync(ModelCompatibilityEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the change history for a specific model.
    /// </summary>
    Task<IReadOnlyList<ModelChangeLogEntry>> GetChangeHistoryAsync(string? modelId = null, int limit = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs a model change event.
    /// </summary>
    Task LogChangeAsync(ModelChangeLogEntry entry, CancellationToken cancellationToken = default);
}

/// <summary>
/// Read model for model compatibility entry.
/// </summary>
public record ModelCompatibilityEntry(
    Guid Id,
    string ModelId,
    string DisplayName,
    string Provider,
    string[] Alternatives,
    int ContextWindow,
    string[] Strengths,
    bool IsCurrentlyAvailable,
    bool IsDeprecated,
    DateTime? LastVerifiedAt);

/// <summary>
/// Read model for model change log entry.
/// </summary>
public record ModelChangeLogEntry(
    Guid Id,
    string ModelId,
    string ChangeType,
    string? PreviousModelId,
    string? NewModelId,
    string? AffectedStrategy,
    string Reason,
    bool IsAutomatic,
    Guid? ChangedByUserId,
    DateTime OccurredAt);
