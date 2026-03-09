namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Issue #5476: Manages emergency operational overrides for the LLM subsystem.
/// Overrides are stored in Redis with TTL for auto-revert.
/// </summary>
internal interface IEmergencyOverrideService
{
    /// <summary>
    /// Activate an emergency override action.
    /// </summary>
    Task ActivateOverrideAsync(
        string action,
        int durationMinutes,
        string reason,
        Guid adminUserId,
        string? targetProvider = null,
        CancellationToken ct = default);

    /// <summary>
    /// Deactivate an active override (early manual revert).
    /// </summary>
    Task DeactivateOverrideAsync(string action, Guid adminUserId, CancellationToken ct = default);

    /// <summary>
    /// Check if a specific override is currently active.
    /// </summary>
    Task<bool> IsOverrideActiveAsync(string action, CancellationToken ct = default);

    /// <summary>
    /// Check if force-ollama-only mode is active (blocks all OpenRouter traffic).
    /// Fast path used by HybridLlmService on every request.
    /// </summary>
    Task<bool> IsForceOllamaOnlyAsync(CancellationToken ct = default);

    /// <summary>
    /// Get all currently active overrides.
    /// </summary>
    Task<IReadOnlyList<ActiveOverrideInfo>> GetActiveOverridesAsync(CancellationToken ct = default);
}

/// <summary>
/// Represents an active emergency override.
/// </summary>
internal sealed record ActiveOverrideInfo(
    string Action,
    string Reason,
    Guid AdminUserId,
    string? TargetProvider,
    DateTime ActivatedAt,
    DateTime ExpiresAt,
    int RemainingMinutes);
