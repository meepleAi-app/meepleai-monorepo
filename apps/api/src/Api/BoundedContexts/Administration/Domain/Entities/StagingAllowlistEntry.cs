using Api.BoundedContexts.Administration.Domain.Events;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.Administration.Domain.Entities;

/// <summary>
/// Email allowlist entry for staging environment access (DevOps Wave 1).
/// </summary>
/// <remarks>
/// Soft-deleted; <see cref="IsDeleted"/> filter applied at query level.
/// Email is stored normalized to lowercase (responsibility of factory + EF config).
/// Replaces <c>STAGING_ALLOWED_EMAILS</c> env-var (#845 — see migration note in
/// <c>StagingAllowlistBootstrapSeeder</c>).
/// </remarks>
internal sealed class StagingAllowlistEntry : AggregateRoot<Guid>
{
    public string Email { get; private set; }
    public Guid? AddedByUserId { get; private set; }
    public DateTimeOffset AddedAt { get; private set; }
    public string? Note { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public Guid? DeletedByUserId { get; private set; }

#pragma warning disable CS8618
    private StagingAllowlistEntry() : base() { }
#pragma warning restore CS8618

    private StagingAllowlistEntry(Guid id, string email, Guid? addedByUserId, string? note) : base(id)
    {
        Email = email;
        AddedByUserId = addedByUserId;
        AddedAt = DateTimeOffset.UtcNow;
        Note = note;
        IsDeleted = false;
    }

    /// <summary>
    /// Creates a new active allowlist entry.
    /// </summary>
    /// <param name="email">Email already normalized to lowercase by caller (use <see cref="NormalizeEmail"/>).</param>
    /// <param name="addedByUserId">User who added the entry; <c>null</c> for system bootstrap seeds.</param>
    /// <param name="note">Optional context (max 500 chars enforced at EF level).</param>
    public static StagingAllowlistEntry Create(string email, Guid? addedByUserId, string? note)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        var normalized = NormalizeEmail(email);

        var entry = new StagingAllowlistEntry(Guid.NewGuid(), normalized, addedByUserId, note);
        entry.AddDomainEvent(new StagingAllowlistEntryAddedEvent(entry.Id, normalized, addedByUserId));
        return entry;
    }

    /// <summary>
    /// Marks the entry as soft-deleted. Emits <see cref="StagingAllowlistEntryRemovedEvent"/>
    /// so the in-memory cache in <c>StagingAccessGuard</c> invalidates.
    /// </summary>
    public void SoftDelete(Guid? removedByUserId)
    {
        if (IsDeleted)
        {
            return;
        }

        IsDeleted = true;
        DeletedAt = DateTimeOffset.UtcNow;
        DeletedByUserId = removedByUserId;
        AddDomainEvent(new StagingAllowlistEntryRemovedEvent(Id, Email, removedByUserId));
    }

    /// <summary>
    /// Canonical email normalization. Trim + lowercase invariant.
    /// Domain rule: equality of two entries is decided on the normalized form,
    /// preventing PK collisions between e.g. "User@Example.com" and "user@example.com".
    /// </summary>
    public static string NormalizeEmail(string email) =>
        email.Trim().ToLowerInvariant();

    /// <summary>
    /// Rehydrates a persisted entry without emitting domain events.
    /// Used by the repository when materializing from EF results.
    /// </summary>
    internal static StagingAllowlistEntry Reconstitute(
        Guid id,
        string email,
        Guid? addedByUserId,
        DateTimeOffset addedAt,
        string? note,
        bool isDeleted,
        DateTimeOffset? deletedAt,
        Guid? deletedByUserId)
    {
        return new StagingAllowlistEntry(id, email, addedByUserId, note)
        {
            AddedAt = addedAt,
            IsDeleted = isDeleted,
            DeletedAt = deletedAt,
            DeletedByUserId = deletedByUserId
        };
    }
}
