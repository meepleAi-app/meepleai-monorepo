namespace Api.Infrastructure.Entities.GameManagement;

/// <summary>
/// Infrastructure entity for SessionInvite.
/// E3-1: Session invite links/PINs for game night flow.
/// </summary>
public class SessionInviteEntity
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string Pin { get; set; } = default!;
    public string LinkToken { get; set; } = default!;
    public int MaxUses { get; set; }
    public int CurrentUses { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }

    // Navigation
    public LiveGameSessionEntity LiveGameSession { get; set; } = default!;
}
