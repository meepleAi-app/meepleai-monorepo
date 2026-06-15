using Api.BoundedContexts.SharedGameCatalog.Application.Exceptions;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Entities;

/// <summary>
/// Aggregate root for a single non-EN translation of a shared game's user-facing
/// strings (title + description). Persisted in <c>shared_game_translations</c>.
/// Issue #2339 — sub-PR 1/3.
/// </summary>
/// <remarks>
/// Invariants:
/// <list type="bullet">
///   <item>Locale must NOT equal <see cref="Locale.CanonicalEn"/> — EN copy is on shared_games.</item>
///   <item>Title is required, trimmed, ≤ 500 chars.</item>
///   <item>Description is optional, trimmed if present.</item>
///   <item>Updates and deletes are forbidden once <see cref="IsDeleted"/> is true (until <see cref="Restore"/>).</item>
///   <item>Optimistic concurrency is enforced via PostgreSQL <c>xmin</c>; see DEC-C4 in the plan.</item>
/// </list>
/// </remarks>
public sealed class SharedGameTranslation
{
    public Guid Id { get; private set; }
    public Guid SharedGameId { get; private set; }
    public Locale Locale { get; private set; }
    public string Title { get; private set; }
    public string? Description { get; private set; }
    public TranslationSource Source { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public Guid? DeletedBy { get; private set; }

    /// <summary>
    /// PostgreSQL <c>xmin</c> system column projected as an optimistic concurrency token.
    /// Read-only outside the persistence layer; admin write paths call
    /// <see cref="SetXminForConcurrencyCheck"/> with the client-supplied value before
    /// saving so EF Core can detect lost updates (ADR-060 / DEC-C4).
    /// </summary>
    public uint Xmin { get; private set; }

    private SharedGameTranslation()
    {
        // EF / Rehydrate use this constructor; nullability appeasement.
        Title = null!;
        Locale = null!;
    }

    public static SharedGameTranslation Create(
        Guid sharedGameId,
        Locale locale,
        string title,
        string? description,
        TranslationSource source,
        Guid? createdBy,
        DateTimeOffset now)
    {
        if (sharedGameId == Guid.Empty)
        {
            throw new ArgumentException("SharedGameId required", nameof(sharedGameId));
        }

        ArgumentNullException.ThrowIfNull(locale);

        if (locale.Equals(Locale.CanonicalEn))
        {
            throw new InvalidLocaleException(
                "Canonical EN title is stored on shared_games.title — cannot create translation for 'en'");
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title required", nameof(title));
        }

        var trimmedTitle = title.Trim();
        if (trimmedTitle.Length > 500)
        {
            throw new ArgumentException("Title max 500 chars", nameof(title));
        }

        return new SharedGameTranslation
        {
            Id = Guid.NewGuid(),
            SharedGameId = sharedGameId,
            Locale = locale,
            Title = trimmedTitle,
            Description = description?.Trim(),
            Source = source,
            CreatedAt = now,
            CreatedBy = createdBy,
            IsDeleted = false
        };
    }

    public void UpdateTitle(string newTitle, Guid? updatedBy, DateTimeOffset now)
    {
        if (IsDeleted)
        {
            throw new InvalidOperationException("Cannot update a soft-deleted translation");
        }

        if (string.IsNullOrWhiteSpace(newTitle))
        {
            throw new ArgumentException("Title required", nameof(newTitle));
        }

        var trimmed = newTitle.Trim();
        if (trimmed.Length > 500)
        {
            throw new ArgumentException("Title max 500 chars", nameof(newTitle));
        }

        Title = trimmed;
        UpdatedAt = now;
        UpdatedBy = updatedBy;
    }

    public void UpdateDescription(string? newDescription, Guid? updatedBy, DateTimeOffset now)
    {
        if (IsDeleted)
        {
            throw new InvalidOperationException("Cannot update a soft-deleted translation");
        }

        Description = newDescription?.Trim();
        UpdatedAt = now;
        UpdatedBy = updatedBy;
    }

    public void SoftDelete(Guid? deletedBy, DateTimeOffset now)
    {
        if (IsDeleted)
        {
            return; // idempotent
        }

        IsDeleted = true;
        DeletedAt = now;
        DeletedBy = deletedBy;
    }

    public void Restore(Guid? restoredBy, DateTimeOffset now)
    {
        if (!IsDeleted)
        {
            return; // idempotent
        }

        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;
        UpdatedAt = now;
        UpdatedBy = restoredBy;
    }

    /// <summary>
    /// Sets the <c>xmin</c> token received from the client (typically as part of a PUT body)
    /// for optimistic concurrency checking by EF Core. <c>internal</c> by design: only
    /// command handlers in this bounded context (and unit tests via
    /// <c>InternalsVisibleTo("Api.Tests")</c>) should invoke it. Resolves plan review
    /// finding C4 (avoid reflection trick).
    /// </summary>
    internal void SetXminForConcurrencyCheck(uint xmin) => Xmin = xmin;
}
