using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.Authentication.Domain.Entities;

/// <summary>
/// Aggregate root representing a public-waitlist signup for the Alpha program.
/// Spec §3.5 (2026-04-27-v2-migration-wave-a-2-join.md).
/// </summary>
internal sealed class WaitlistEntry : AggregateRoot<Guid>
{
    public string Email { get; private set; } = null!;
    public string? Name { get; private set; }
    public string GamePreferenceId { get; private set; } = null!;
    public string? GamePreferenceOther { get; private set; }
    public bool NewsletterOptIn { get; private set; }
    public int Position { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ContactedAt { get; private set; }

    // EF Core constructor
    private WaitlistEntry() { }
    private WaitlistEntry(Guid id) : base(id) { }

    /// <summary>
    /// Internal constructor for repository materialization (avoids reflection — S3011).
    /// </summary>
    internal static WaitlistEntry CreateForHydration(Guid id) => new(id);

    /// <summary>
    /// Factory method to create a new waitlist entry.
    /// </summary>
    public static WaitlistEntry Create(
        string email,
        string? name,
        string gamePreferenceId,
        string? gamePreferenceOther,
        bool newsletterOptIn,
        int position)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email cannot be null, empty, or whitespace.", nameof(email));
        if (string.IsNullOrWhiteSpace(gamePreferenceId))
            throw new ArgumentException("GamePreferenceId cannot be null, empty, or whitespace.", nameof(gamePreferenceId));
        if (position < 1)
            throw new ArgumentOutOfRangeException(nameof(position), position, "Position must be >= 1.");

        return new WaitlistEntry
        {
            Id = Guid.NewGuid(),
            Email = email.Trim().ToLowerInvariant(),
            Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim(),
            GamePreferenceId = gamePreferenceId.Trim(),
            GamePreferenceOther = string.IsNullOrWhiteSpace(gamePreferenceOther) ? null : gamePreferenceOther.Trim(),
            NewsletterOptIn = newsletterOptIn,
            Position = position,
            CreatedAt = DateTime.UtcNow,
            ContactedAt = null
        };
    }

    /// <summary>
    /// Marks this waitlist entry as contacted by the operations team.
    /// Idempotency: throws if already contacted (callers should verify ContactedAt first).
    /// </summary>
    public void MarkContacted()
    {
        if (ContactedAt is not null)
            throw new InvalidOperationException(
                $"WaitlistEntry {Id} has already been marked as contacted at {ContactedAt:O}.");

        ContactedAt = DateTime.UtcNow;
    }

    #region Persistence Hydration Methods (internal — S3011 fix)

    /// <summary>
    /// Restores waitlist entry state from persistence layer.
    /// Should only be called by WaitlistEntryRepository during entity materialization.
    /// </summary>
    internal void RestoreState(
        string email,
        string? name,
        string gamePreferenceId,
        string? gamePreferenceOther,
        bool newsletterOptIn,
        int position,
        DateTime createdAt,
        DateTime? contactedAt)
    {
        Email = email;
        Name = name;
        GamePreferenceId = gamePreferenceId;
        GamePreferenceOther = gamePreferenceOther;
        NewsletterOptIn = newsletterOptIn;
        Position = position;
        CreatedAt = createdAt;
        ContactedAt = contactedAt;
    }

    #endregion
}
