using Api.BoundedContexts.SessionTracking.Domain.ValueObjects;

namespace Api.BoundedContexts.SessionTracking.Domain.Entities;

public sealed class GamebookCampaignSession
{
    public Guid Id { get; private set; }
    public Guid GameId { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public string Title { get; private set; } = default!;
    public GamebookProgress Progress { get; private set; } = default!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public Guid? UpdatedBy { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    // EF parameterless constructor
    private GamebookCampaignSession() { }

    public static GamebookCampaignSession Create(Guid gameId, Guid ownerUserId, string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("title required", nameof(title));
        if (gameId == Guid.Empty)
            throw new ArgumentException("gameId required", nameof(gameId));
        if (ownerUserId == Guid.Empty)
            throw new ArgumentException("ownerUserId required", nameof(ownerUserId));

        var now = DateTimeOffset.UtcNow;
        return new GamebookCampaignSession
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            OwnerUserId = ownerUserId,
            Title = title.Trim(),
            Progress = GamebookProgress.Empty(),
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = ownerUserId,
        };
    }

    public void UpdateProgress(int currentParagraph)
    {
        Progress = GamebookProgress.Create(currentParagraph, Progress.History);
        UpdatedAt = DateTimeOffset.UtcNow;
        UpdatedBy = OwnerUserId;
    }

    public void SoftDelete(Guid deletedBy)
    {
        IsDeleted = true;
        DeletedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DeletedAt.Value;
        UpdatedBy = deletedBy;
    }
}
