using Api.SharedKernel.Domain.Entities;
using Api.SharedKernel.Domain.Exceptions;

namespace Api.BoundedContexts.GameManagement.Domain.Entities;

/// <summary>
/// Entity representing a team within a live game session.
/// Teams are optional groupings of players for team-based games.
/// </summary>
internal sealed class LiveSessionTeam : Entity<Guid>
{
    private const int MaxNameLength = 50;
    private const int MaxColorLength = 7; // #RRGGBB

    private readonly List<Guid> _playerIds = new();

    #pragma warning disable CS8618
    private LiveSessionTeam() : base()
    #pragma warning restore CS8618
    {
    }

    internal LiveSessionTeam(
        Guid id,
        Guid liveGameSessionId,
        string name,
        string color) : base(id)
    {
        if (id == Guid.Empty)
            throw new ValidationException("Team ID cannot be empty");

        if (liveGameSessionId == Guid.Empty)
            throw new ValidationException("Session ID cannot be empty");

        if (string.IsNullOrWhiteSpace(name))
            throw new ValidationException("Team name cannot be empty");

        var trimmedName = name.Trim();
        if (trimmedName.Length > MaxNameLength)
            throw new ValidationException($"Team name cannot exceed {MaxNameLength} characters");

        if (string.IsNullOrWhiteSpace(color))
            throw new ValidationException("Team color cannot be empty");

        var trimmedColor = color.Trim();
        if (trimmedColor.Length > MaxColorLength)
            throw new ValidationException($"Team color cannot exceed {MaxColorLength} characters");

        LiveGameSessionId = liveGameSessionId;
        Name = trimmedName;
        Color = trimmedColor;
        TeamScore = 0;
        CurrentRank = 0;
    }

    public Guid LiveGameSessionId { get; private set; }
    public string Name { get; private set; }
    public string Color { get; private set; }
    public IReadOnlyList<Guid> PlayerIds => _playerIds.AsReadOnly();
    public int TeamScore { get; private set; }
    public int CurrentRank { get; private set; }

    internal void AddPlayer(Guid playerId)
    {
        if (playerId == Guid.Empty)
            throw new ValidationException("Player ID cannot be empty");

        if (_playerIds.Contains(playerId))
            throw new DomainException($"Player {playerId} is already on team '{Name}'");

        _playerIds.Add(playerId);
    }

    internal void RemovePlayer(Guid playerId)
    {
        if (!_playerIds.Remove(playerId))
            throw new DomainException($"Player {playerId} is not on team '{Name}'");
    }

    internal void UpdateScore(int teamScore, int rank)
    {
        TeamScore = teamScore;
        CurrentRank = rank;
    }

    internal void UpdateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ValidationException("Team name cannot be empty");

        var trimmed = name.Trim();
        if (trimmed.Length > MaxNameLength)
            throw new ValidationException($"Team name cannot exceed {MaxNameLength} characters");

        Name = trimmed;
    }
}
