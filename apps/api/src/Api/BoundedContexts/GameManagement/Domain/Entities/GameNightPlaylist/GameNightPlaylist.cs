using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.GameManagement.Domain.Entities.GameNightPlaylist;

/// <summary>
/// Aggregate root representing a game night playlist — an ordered list of games planned for a game night.
/// Supports sharing via unique token and soft-delete.
/// Issue #5582: Game Night Playlist backend CRUD with sharing.
/// </summary>
#pragma warning disable MA0049
internal sealed class GameNightPlaylist : AggregateRoot<Guid>
#pragma warning restore MA0049
{
    private readonly List<PlaylistGame> _games = new();

    public string Name { get; private set; }
    public DateTime? ScheduledDate { get; private set; }
    public Guid CreatorUserId { get; private set; }
    public string? ShareToken { get; private set; }
    public bool IsShared { get; private set; }
    public IReadOnlyList<PlaylistGame> Games => _games.AsReadOnly();
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
#pragma warning disable CS8618
    private GameNightPlaylist() : base()
#pragma warning restore CS8618
    {
    }

    /// <summary>
    /// Creates a new game night playlist.
    /// </summary>
    internal GameNightPlaylist(
        Guid id,
        string name,
        Guid creatorUserId,
        DateTime? scheduledDate = null) : base(id)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Playlist name cannot be empty", nameof(name));
        if (name.Length > 200)
            throw new ArgumentException("Playlist name cannot exceed 200 characters", nameof(name));
        if (creatorUserId == Guid.Empty)
            throw new ArgumentException("CreatorUserId cannot be empty", nameof(creatorUserId));

        Name = name.Trim();
        CreatorUserId = creatorUserId;
        ScheduledDate = scheduledDate;
        IsShared = false;
        IsDeleted = false;
        CreatedAt = TimeProvider.System.GetUtcNow().UtcDateTime;
        UpdatedAt = TimeProvider.System.GetUtcNow().UtcDateTime;

        AddDomainEvent(new PlaylistCreatedEvent(id, name, creatorUserId));
    }

    /// <summary>
    /// Factory method to create a new playlist.
    /// </summary>
    public static GameNightPlaylist Create(string name, Guid creatorUserId, DateTime? scheduledDate = null)
    {
        return new GameNightPlaylist(Guid.NewGuid(), name, creatorUserId, scheduledDate);
    }

    /// <summary>
    /// Updates the playlist name.
    /// </summary>
    public void UpdateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Playlist name cannot be empty", nameof(name));
        if (name.Length > 200)
            throw new ArgumentException("Playlist name cannot exceed 200 characters", nameof(name));

        Name = name.Trim();
        UpdatedAt = TimeProvider.System.GetUtcNow().UtcDateTime;
    }

    /// <summary>
    /// Updates the scheduled date for the game night.
    /// </summary>
    public void UpdateScheduledDate(DateTime? date)
    {
        ScheduledDate = date;
        UpdatedAt = TimeProvider.System.GetUtcNow().UtcDateTime;
    }

    /// <summary>
    /// Adds a game to the playlist at a given position.
    /// </summary>
    public void AddGame(Guid sharedGameId, int position)
    {
        if (sharedGameId == Guid.Empty)
            throw new ArgumentException("SharedGameId cannot be empty", nameof(sharedGameId));
        if (position < 1)
            throw new ArgumentException("Position must be >= 1", nameof(position));

        if (_games.Any(g => g.SharedGameId == sharedGameId))
            throw new ConflictException($"Game {sharedGameId} is already in the playlist");

        // Shift existing games at or after position
        for (var i = 0; i < _games.Count; i++)
        {
            if (_games[i].Position >= position)
            {
                _games[i] = _games[i] with { Position = _games[i].Position + 1 };
            }
        }

        _games.Add(new PlaylistGame
        {
            SharedGameId = sharedGameId,
            Position = position,
            AddedAt = TimeProvider.System.GetUtcNow().UtcDateTime
        });

        UpdatedAt = TimeProvider.System.GetUtcNow().UtcDateTime;
    }

    /// <summary>
    /// Removes a game from the playlist and reorders remaining games.
    /// </summary>
    public void RemoveGame(Guid sharedGameId)
    {
        if (sharedGameId == Guid.Empty)
            throw new ArgumentException("SharedGameId cannot be empty", nameof(sharedGameId));

        var game = _games.FirstOrDefault(g => g.SharedGameId == sharedGameId)
            ?? throw new NotFoundException("PlaylistGame", sharedGameId.ToString());

        var removedPosition = game.Position;
        _games.Remove(game);

        // Reorder: close the gap
        for (var i = 0; i < _games.Count; i++)
        {
            if (_games[i].Position > removedPosition)
            {
                _games[i] = _games[i] with { Position = _games[i].Position - 1 };
            }
        }

        UpdatedAt = TimeProvider.System.GetUtcNow().UtcDateTime;
    }

    /// <summary>
    /// Reorders games according to the provided ordered list of game IDs.
    /// All current game IDs must be present in the list (no additions, no removals).
    /// </summary>
    public void ReorderGames(List<Guid> orderedGameIds)
    {
        ArgumentNullException.ThrowIfNull(orderedGameIds);

        if (orderedGameIds.Count != _games.Count)
            throw new ArgumentException("Ordered list must contain exactly the same games as the playlist", nameof(orderedGameIds));

        var currentIds = _games.Select(g => g.SharedGameId).ToHashSet();
        var newIds = orderedGameIds.ToHashSet();

        if (!currentIds.SetEquals(newIds))
            throw new ArgumentException("Ordered list must contain exactly the same games as the playlist", nameof(orderedGameIds));

        for (var i = 0; i < _games.Count; i++)
        {
            var gameId = _games[i].SharedGameId;
            var newPosition = orderedGameIds.IndexOf(gameId) + 1; // 1-based
            _games[i] = _games[i] with { Position = newPosition };
        }

        UpdatedAt = TimeProvider.System.GetUtcNow().UtcDateTime;
    }

    /// <summary>
    /// Generates a unique share token for public access.
    /// </summary>
    public string GenerateShareToken()
    {
        ShareToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .Replace("/", "_")
            .Replace("+", "-")
            .TrimEnd('=');
        IsShared = true;
        UpdatedAt = TimeProvider.System.GetUtcNow().UtcDateTime;
        return ShareToken;
    }

    /// <summary>
    /// Revokes the share token, disabling public access.
    /// </summary>
    public void RevokeShareToken()
    {
        ShareToken = null;
        IsShared = false;
        UpdatedAt = TimeProvider.System.GetUtcNow().UtcDateTime;
    }

    /// <summary>
    /// Soft-deletes the playlist.
    /// </summary>
    public void SoftDelete()
    {
        if (IsDeleted)
            throw new ConflictException("Playlist is already deleted");

        IsDeleted = true;
        DeletedAt = TimeProvider.System.GetUtcNow().UtcDateTime;
        UpdatedAt = TimeProvider.System.GetUtcNow().UtcDateTime;
    }

    /// <summary>
    /// Internal method to restore games list from persistence.
    /// </summary>
    internal void RestoreGames(IEnumerable<PlaylistGame> games)
    {
        _games.Clear();
        _games.AddRange(games);
    }
}
