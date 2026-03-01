using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.SharedKernel.Domain.Entities;
using Api.SharedKernel.Domain.Exceptions;

namespace Api.BoundedContexts.GameManagement.Domain.Entities;

/// <summary>
/// Entity representing a player participating in a live game session.
/// Supports both registered users and guest players.
/// </summary>
internal sealed class LiveSessionPlayer : Entity<Guid>
{
    private const int MaxDisplayNameLength = 100;
    private const int MaxAvatarUrlLength = 500;

#pragma warning disable CS8618
    private LiveSessionPlayer() : base()
#pragma warning restore CS8618
    {
    }

    internal LiveSessionPlayer(
        Guid id,
        Guid liveGameSessionId,
        Guid? userId,
        string displayName,
        PlayerColor color,
        PlayerRole role,
        DateTime joinedAt,
        string? avatarUrl = null,
        Guid? teamId = null) : base(id)
    {
        if (id == Guid.Empty)
            throw new ValidationException("Player ID cannot be empty");

        if (liveGameSessionId == Guid.Empty)
            throw new ValidationException("Session ID cannot be empty");

        if (string.IsNullOrWhiteSpace(displayName))
            throw new ValidationException("Player display name cannot be empty");

        var trimmedName = displayName.Trim();
        if (trimmedName.Length > MaxDisplayNameLength)
            throw new ValidationException($"Player display name cannot exceed {MaxDisplayNameLength} characters");

        if (avatarUrl != null)
        {
            var trimmedUrl = avatarUrl.Trim();
            if (trimmedUrl.Length > MaxAvatarUrlLength)
                throw new ValidationException($"Avatar URL cannot exceed {MaxAvatarUrlLength} characters");
            avatarUrl = trimmedUrl;
        }

        LiveGameSessionId = liveGameSessionId;
        UserId = userId;
        DisplayName = trimmedName;
        AvatarUrl = avatarUrl;
        Color = color;
        Role = role;
        TeamId = teamId;
        TotalScore = 0;
        CurrentRank = 0;
        JoinedAt = joinedAt;
        IsActive = true;
    }

    public Guid LiveGameSessionId { get; private set; }
    public Guid? UserId { get; private set; }
    public string DisplayName { get; private set; }
    public string? AvatarUrl { get; private set; }
    public PlayerColor Color { get; private set; }
    public PlayerRole Role { get; private set; }
    public Guid? TeamId { get; private set; }
    public int TotalScore { get; private set; }
    public int CurrentRank { get; private set; }
    public DateTime JoinedAt { get; private set; }
    public bool IsActive { get; private set; }

    internal void UpdateScore(int totalScore, int rank)
    {
        TotalScore = totalScore;
        CurrentRank = rank;
    }

    internal void AssignToTeam(Guid? teamId)
    {
        TeamId = teamId;
    }

    internal void ChangeRole(PlayerRole newRole)
    {
        Role = newRole;
    }

    internal void Deactivate()
    {
        IsActive = false;
    }

    internal void Activate()
    {
        IsActive = true;
    }

    internal void UpdateDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ValidationException("Player display name cannot be empty");

        var trimmed = displayName.Trim();
        if (trimmed.Length > MaxDisplayNameLength)
            throw new ValidationException($"Player display name cannot exceed {MaxDisplayNameLength} characters");

        DisplayName = trimmed;
    }

    internal void UpdateColor(PlayerColor color)
    {
        Color = color;
    }
}
