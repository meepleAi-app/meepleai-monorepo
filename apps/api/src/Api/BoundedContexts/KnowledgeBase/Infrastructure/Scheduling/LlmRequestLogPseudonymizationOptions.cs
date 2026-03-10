namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Scheduling;

/// <summary>
/// Configuration for LLM request log pseudonymization.
/// Issue #5511: GDPR log pseudonymization settings.
/// </summary>
public sealed class LlmRequestLogPseudonymizationOptions
{
    public const string SectionName = "Gdpr:LogPseudonymization";

    /// <summary>
    /// Salt used for SHA-256 hashing of UserId.
    /// Should be a random string, kept secret and consistent across runs
    /// to ensure the same UserId always produces the same hash (analytics continuity).
    /// </summary>
    public string Salt { get; set; } = "meepleai-gdpr-default-salt-change-in-production";
}
