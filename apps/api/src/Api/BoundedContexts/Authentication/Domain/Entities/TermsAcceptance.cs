using Api.BoundedContexts.Authentication.Domain.Enums;

namespace Api.BoundedContexts.Authentication.Domain.Entities;

/// <summary>
/// Append-only record of a user's acceptance of a specific Terms of Service version
/// (#2954 F1). One row per acceptance event — never updated in place — so the history
/// of which version was accepted when is preserved for legal defensibility.
/// </summary>
public sealed class TermsAcceptance
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string TermsVersion { get; private set; } = string.Empty;
    public DateTime AcceptedAt { get; private set; }
    public TermsAcceptanceContext Context { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // EF Core
    private TermsAcceptance() { }

    private TermsAcceptance(
        Guid userId, string termsVersion, TermsAcceptanceContext context, string? ipAddress, string? userAgent)
    {
        var now = DateTime.UtcNow;
        Id = Guid.NewGuid();
        UserId = userId;
        TermsVersion = termsVersion;
        AcceptedAt = now;
        Context = context;
        IpAddress = ipAddress;
        UserAgent = userAgent;
        CreatedAt = now;
    }

    public static TermsAcceptance Create(
        Guid userId,
        string termsVersion,
        TermsAcceptanceContext context,
        string? ipAddress = null,
        string? userAgent = null)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID cannot be empty", nameof(userId));
        if (string.IsNullOrWhiteSpace(termsVersion))
            throw new ArgumentException("Terms version is required", nameof(termsVersion));

        return new TermsAcceptance(userId, termsVersion, context, ipAddress, userAgent);
    }
}
