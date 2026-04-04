using Api.BoundedContexts.AgentMemory.Domain.Models;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.AgentMemory.Domain.Entities;

/// <summary>
/// Aggregate root tracking a player's game statistics. Can represent a registered user or a guest,
/// with support for guest-to-user claim.
/// </summary>
internal sealed class PlayerMemory : AggregateRoot<Guid>
{
    public Guid? UserId { get; private set; }
    public string? GuestName { get; private set; }
    public Guid? GroupId { get; private set; }

    private readonly List<PlayerGameStats> _gameStats = new();
    public IReadOnlyList<PlayerGameStats> GameStats => _gameStats.AsReadOnly();

    public DateTime? ClaimedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    /// <summary>EF Core constructor.</summary>
    private PlayerMemory() { }

    public static PlayerMemory CreateForUser(Guid userId, Guid? groupId = null)
    {
        if (userId == Guid.Empty) throw new ArgumentException("UserId cannot be empty.", nameof(userId));

        return new PlayerMemory
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            GroupId = groupId,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static PlayerMemory CreateForGuest(string guestName, Guid? groupId = null)
    {
        if (string.IsNullOrWhiteSpace(guestName))
            throw new ArgumentException("Guest name cannot be empty.", nameof(guestName));

        return new PlayerMemory
        {
            Id = Guid.NewGuid(),
            GuestName = guestName,
            GroupId = groupId,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Claims a guest player memory for a registered user.
    /// </summary>
    public void ClaimByUser(Guid userId)
    {
        if (userId == Guid.Empty) throw new ArgumentException("UserId cannot be empty.", nameof(userId));
        if (UserId != null) throw new InvalidOperationException("Already claimed by a user.");

        UserId = userId;
        ClaimedAt = DateTime.UtcNow;
    }

    public void UpdateGameStats(Guid gameId, bool won, int? score = null)
    {
        if (gameId == Guid.Empty)
            throw new ArgumentException("GameId cannot be empty.", nameof(gameId));

        var existing = _gameStats.Find(s => s.GameId == gameId);
        if (existing != null)
        {
            existing.RecordPlay(won, score);
        }
        else
        {
            _gameStats.Add(PlayerGameStats.Create(gameId, won, score));
        }
    }
}
