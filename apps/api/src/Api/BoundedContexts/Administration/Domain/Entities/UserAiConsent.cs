namespace Api.BoundedContexts.Administration.Domain.Entities;

/// <summary>
/// Tracks user consent for AI data processing under GDPR (Issue #5512)
/// </summary>
public sealed class UserAiConsent
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public bool ConsentedToAiProcessing { get; private set; }
    public bool ConsentedToExternalProviders { get; private set; }
    public DateTime ConsentedAt { get; private set; }
    public string ConsentVersion { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    // EF Core
    private UserAiConsent() { }

    private UserAiConsent(Guid userId, bool consentedToAiProcessing, bool consentedToExternalProviders, string consentVersion)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        ConsentedToAiProcessing = consentedToAiProcessing;
        ConsentedToExternalProviders = consentedToExternalProviders;
        ConsentedAt = DateTime.UtcNow;
        ConsentVersion = consentVersion;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public static UserAiConsent Create(Guid userId, bool consentedToAiProcessing, bool consentedToExternalProviders, string consentVersion)
    {
        if (userId == Guid.Empty) throw new ArgumentException("User ID cannot be empty", nameof(userId));
        if (string.IsNullOrWhiteSpace(consentVersion)) throw new ArgumentException("Consent version is required", nameof(consentVersion));

        return new UserAiConsent(userId, consentedToAiProcessing, consentedToExternalProviders, consentVersion);
    }

    /// <summary>
    /// Update consent preferences (records new consent timestamp)
    /// </summary>
    public void UpdateConsent(bool consentedToAiProcessing, bool consentedToExternalProviders, string consentVersion)
    {
        if (string.IsNullOrWhiteSpace(consentVersion)) throw new ArgumentException("Consent version is required", nameof(consentVersion));

        ConsentedToAiProcessing = consentedToAiProcessing;
        ConsentedToExternalProviders = consentedToExternalProviders;
        ConsentVersion = consentVersion;
        ConsentedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Withdraw all AI consent
    /// </summary>
    public void WithdrawConsent()
    {
        ConsentedToAiProcessing = false;
        ConsentedToExternalProviders = false;
        UpdatedAt = DateTime.UtcNow;
    }
}
