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
}
