using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.SessionTracking.Domain.Entities;

public sealed class GamebookCampaignSession
{
    public Guid Id { get; private set; }
    public GameRef GameRef { get; private set; } = default!; // A0.2 (#1320): replaces bare Guid GameId
    public Guid OwnerUserId { get; private set; }
    public string Title { get; private set; } = default!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public Guid? UpdatedBy { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    // SI-8 (#2639): manual terminal close ("Completa"/"Abbandona" in the play-evening
    // 3-way selector). Per D4 (reuse-Session), the campaign does NOT duplicate the
    // Session timestamp-trio; the play-evening lifecycle lives on Session/GameNight.
    // These two nullable columns are the single "manual Complete flag": a null
    // Outcome means the campaign is still open/resumable ("Archivia" leaves it null).
    public GamebookCampaignOutcome? Outcome { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>True once the campaign has been terminally closed (Completa/Abbandona).</summary>
    public bool IsClosed => Outcome.HasValue;

    // EF parameterless constructor
    private GamebookCampaignSession() { }

    public static GamebookCampaignSession Create(GameRef gameRef, Guid ownerUserId, string title)
    {
        ArgumentNullException.ThrowIfNull(gameRef);
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("title required", nameof(title));
        if (ownerUserId == Guid.Empty)
            throw new ArgumentException("ownerUserId required", nameof(ownerUserId));

        var now = DateTimeOffset.UtcNow;
        return new GamebookCampaignSession
        {
            Id = Guid.NewGuid(),
            GameRef = gameRef,
            OwnerUserId = ownerUserId,
            Title = title.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = ownerUserId,
        };
    }

    // C2 (Gamebook multi-book generalization, spec 2026-05-19): UpdateProgress() and
    // the Progress VO were removed. Per-book progress is now tracked in the
    // SessionBookProgress entity (Task C1) — one row per (campaign, book) pair.
    // Mutate progress via ISessionBookProgressRepository instead of this aggregate.

    public void Touch(Guid updatedBy)
    {
        UpdatedAt = DateTimeOffset.UtcNow;
        UpdatedBy = updatedBy;
    }

    public void Rename(string newTitle, Guid updatedBy)
    {
        if (string.IsNullOrWhiteSpace(newTitle))
            throw new ArgumentException("title required", nameof(newTitle));

        Title = newTitle.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
        UpdatedBy = updatedBy;
    }

    public void SoftDelete(Guid deletedBy)
    {
        IsDeleted = true;
        DeletedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DeletedAt.Value;
        UpdatedBy = deletedBy;
    }

    /// <summary>
    /// SI-8 (#2639): terminally closes the campaign with a manual outcome
    /// (<see cref="GamebookCampaignOutcome.Completed"/> or
    /// <see cref="GamebookCampaignOutcome.Abandoned"/>), stamping
    /// <see cref="CompletedAt"/>. Idempotency guard: throws if already closed.
    /// The campaign is NOT soft-deleted — it stays queryable so the resume picker
    /// can list it under a "completed" section (it just filters it out of the
    /// resumable set). "Archivia" (resumable) never calls this.
    /// </summary>
    /// <exception cref="ConflictException">Thrown when the campaign is already closed.</exception>
    public void Close(GamebookCampaignOutcome outcome, Guid closedBy)
    {
        if (Outcome.HasValue)
            throw new ConflictException(
                $"Campaign {Id} is already closed (outcome={Outcome}). Cannot close again.");

        var now = DateTimeOffset.UtcNow;
        Outcome = outcome;
        CompletedAt = now;
        UpdatedAt = now;
        UpdatedBy = closedBy;
    }
}
