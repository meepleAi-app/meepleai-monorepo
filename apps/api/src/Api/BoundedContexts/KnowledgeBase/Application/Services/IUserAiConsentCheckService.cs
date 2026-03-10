namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Cross-cutting service to check user AI consent status (Issue #5513).
/// Used by KnowledgeBase handlers to enforce opt-out before LLM generation.
/// </summary>
public interface IUserAiConsentCheckService
{
    /// <summary>
    /// Returns true if the user has consented to AI processing.
    /// Users with no consent record default to false (GDPR safe default).
    /// </summary>
    Task<bool> IsAiProcessingAllowedAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true if the user has consented to external AI providers.
    /// </summary>
    Task<bool> IsExternalProviderAllowedAsync(Guid userId, CancellationToken cancellationToken = default);
}
