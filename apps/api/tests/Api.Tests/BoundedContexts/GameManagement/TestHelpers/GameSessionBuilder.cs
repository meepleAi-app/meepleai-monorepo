using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;

namespace Api.Tests.BoundedContexts.GameManagement.TestHelpers;

/// <summary>
/// Builder for creating GameSession test instances with sensible defaults.
/// </summary>
public class GameSessionBuilder
{
    private Guid _id = Guid.NewGuid();
    private Guid _gameId = Guid.NewGuid();
    private List<SessionPlayer> _players = new()
    {
        new SessionPlayer("Player 1"),
        new SessionPlayer("Player 2")
    };
    private string? _notes;
    private bool _shouldStart;
    private bool _shouldComplete;
    private string? _winnerName;

    public GameSessionBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public GameSessionBuilder WithGameId(Guid gameId)
    {
        _gameId = gameId;
        return this;
    }

    public GameSessionBuilder WithPlayer(string playerName)
    {
        _players.Add(new SessionPlayer(playerName));
        return this;
    }

    public GameSessionBuilder WithPlayers(params string[] playerNames)
    {
        _players.Clear();
        foreach (var name in playerNames)
        {
            _players.Add(new SessionPlayer(name));
        }
        return this;
    }

    public GameSessionBuilder WithPlayers(IEnumerable<SessionPlayer> players)
    {
        _players = players.ToList();
        return this;
    }

    public GameSessionBuilder WithNotes(string notes)
    {
        _notes = notes;
        return this;
    }

    /// <summary>
    /// Creates session and immediately starts it (InProgress status).
    /// </summary>
    public GameSessionBuilder ThatIsStarted()
    {
        _shouldStart = true;
        return this;
    }

    /// <summary>
    /// Creates session and completes it (Completed status).
    /// </summary>
    public GameSessionBuilder ThatIsCompleted(string? winnerName = null)
    {
        _shouldComplete = true;
        _winnerName = winnerName ?? "Player 1";
        return this;
    }

    /// <summary>
    /// Creates a session with 4 players (typical board game scenario).
    /// </summary>
    public GameSessionBuilder WithFourPlayers()
    {
        return WithPlayers("Alice", "Bob", "Charlie", "Diana");
    }

    /// <summary>
    /// Creates a session with 2 players (minimum).
    /// </summary>
    public GameSessionBuilder WithTwoPlayers()
    {
        return WithPlayers("Player 1", "Player 2");
    }

    /// <summary>
    /// Builds the GameSession instance.
    /// </summary>
    public GameSession Build()
    {
        var session = new GameSession(_id, _gameId, _players);

        if (_notes != null)
        {
            session.AddNotes(_notes);
        }

        if (_shouldStart)
        {
            session.Start();
        }

        if (_shouldComplete)
        {
            if (!_shouldStart)
            {
                session.Start(); // Must start before completing
            }
            session.Complete(_winnerName);
        }

        return session;
    }

    /// <summary>
    /// Implicit conversion to GameSession for convenience.
    /// </summary>
    public static implicit operator GameSession(GameSessionBuilder builder) => builder.Build();
}
