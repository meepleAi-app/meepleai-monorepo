using System.Security.Cryptography;
using System.Text.Json;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.Exceptions;

namespace Api.BoundedContexts.GameManagement.Domain.Entities;

/// <summary>
/// Aggregate root representing a live board game session with real-time tracking.
/// Manages player participation, scoring, turn management, and session lifecycle.
/// </summary>
internal sealed class LiveGameSession : AggregateRoot<Guid>
{
    private const int SessionCodeLength = 6;
    private const int MaxGameNameLength = 255;
    private const int MaxNotesLength = 2000;
    private const int MaxPlayers = 20;
    private const int MaxTeams = 10;
    private const int MaxTurns = 9999;

    private readonly List<LiveSessionPlayer> _players = new();
    private readonly List<LiveSessionTeam> _teams = new();
    private readonly List<Guid> _turnOrder = new();
    private readonly List<RoundScore> _roundScores = new();
    private readonly List<TurnRecord> _turnRecords = new();

    #pragma warning disable CS8618
    private LiveGameSession() : base()
    #pragma warning restore CS8618
    {
    }

    // === Properties ===

    public string SessionCode { get; private set; }
    public Guid? GameId { get; private set; }
    public string GameName { get; private set; }
    public Guid? ToolkitId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public PlayRecordVisibility Visibility { get; private set; }
    public Guid? GroupId { get; private set; }
    public LiveSessionStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? PausedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public int CurrentTurnIndex { get; private set; }
    public int? CurrentPhaseIndex { get; internal set; }
    public SessionScoringConfig ScoringConfig { get; private set; }
    public JsonDocument? GameState { get; private set; }
    public string? Notes { get; private set; }
    public AgentSessionMode AgentMode { get; private set; }
    public Guid? ChatSessionId { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    // === Read-only collections ===

    public IReadOnlyList<LiveSessionPlayer> Players => _players.AsReadOnly();
    public IReadOnlyList<LiveSessionTeam> Teams => _teams.AsReadOnly();
    public IReadOnlyList<Guid> TurnOrder => _turnOrder.AsReadOnly();
    public IReadOnlyList<RoundScore> RoundScores => _roundScores.AsReadOnly();
    public IReadOnlyList<TurnRecord> TurnRecords => _turnRecords.AsReadOnly();

    // === Computed Properties ===

    public int PlayerCount => _players.Count(p => p.IsActive);
    public bool HasPlayers => _players.Any(p => p.IsActive);
    public bool IsActive => Status is LiveSessionStatus.InProgress or LiveSessionStatus.Paused or LiveSessionStatus.Setup;
    public LiveSessionPlayer? Host => _players.FirstOrDefault(p => p.Role == PlayerRole.Host && p.IsActive);

    // === Factory Methods ===

    /// <summary>
    /// Creates a new live game session linked to a catalog game.
    /// </summary>
    public static LiveGameSession Create(
        Guid id,
        Guid createdByUserId,
        string gameName,
        TimeProvider? timeProvider = null,
        Guid? gameId = null,
        PlayRecordVisibility visibility = PlayRecordVisibility.Private,
        Guid? groupId = null,
        SessionScoringConfig? scoringConfig = null,
        AgentSessionMode agentMode = AgentSessionMode.None)
    {
        if (id == Guid.Empty)
            throw new ValidationException("Session ID cannot be empty");

        if (createdByUserId == Guid.Empty)
            throw new ValidationException("Creator user ID cannot be empty");

        if (string.IsNullOrWhiteSpace(gameName))
            throw new ValidationException("Game name cannot be empty");

        var trimmedName = gameName.Trim();
        if (trimmedName.Length > MaxGameNameLength)
            throw new ValidationException($"Game name cannot exceed {MaxGameNameLength} characters");

        if (visibility == PlayRecordVisibility.Group && groupId == null)
            throw new ValidationException("Group ID is required for group visibility");

        var provider = timeProvider ?? TimeProvider.System;
        var now = provider.GetUtcNow().UtcDateTime;

        var session = new LiveGameSession
        {
            Id = id,
            SessionCode = GenerateSessionCode(),
            GameId = gameId,
            GameName = trimmedName,
            CreatedByUserId = createdByUserId,
            Visibility = visibility,
            GroupId = groupId,
            Status = LiveSessionStatus.Created,
            CreatedAt = now,
            UpdatedAt = now,
            CurrentTurnIndex = 0,
            ScoringConfig = scoringConfig ?? SessionScoringConfig.CreateDefault(),
            AgentMode = agentMode
        };

        session.AddDomainEvent(new LiveSessionCreatedEvent(id, createdByUserId, trimmedName, gameId));

        return session;
    }

    // === Player Management ===

    /// <summary>
    /// Adds a player to the session. The first player is automatically assigned Host role.
    /// </summary>
    public LiveSessionPlayer AddPlayer(
        Guid? userId,
        string displayName,
        PlayerColor color,
        TimeProvider? timeProvider = null,
        PlayerRole? role = null,
        string? avatarUrl = null)
    {
        if (Status == LiveSessionStatus.Completed)
            throw new ConflictException("Cannot add players to a completed session");

        if (_players.Count(p => p.IsActive) >= MaxPlayers)
            throw new DomainException($"Cannot add more than {MaxPlayers} players to a session");

        if (userId.HasValue && _players.Any(p => p.UserId == userId && p.IsActive))
            throw new DomainException($"User {userId} is already an active player in this session");

        var normalizedName = displayName.Trim().ToLowerInvariant();
        if (_players.Any(p => p.IsActive && string.Equals(p.DisplayName.Trim().ToLowerInvariant(), normalizedName, StringComparison.Ordinal)))
            throw new DomainException($"Player with display name '{displayName}' already exists in this session");

        if (_players.Any(p => p.IsActive && p.Color == color))
            throw new DomainException($"Color {color} is already taken by another player");

        var now = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        var assignedRole = role ?? (!HasPlayers ? PlayerRole.Host : PlayerRole.Player);
        var playerId = Guid.NewGuid();

        var player = new LiveSessionPlayer(
            playerId,
            Id,
            userId,
            displayName,
            color,
            assignedRole,
            now,
            avatarUrl);

        _players.Add(player);
        _turnOrder.Add(playerId);
        UpdatedAt = now;

        AddDomainEvent(new LiveSessionPlayerAddedEvent(Id, playerId, userId, displayName, assignedRole));

        return player;
    }

    /// <summary>
    /// Removes a player from the session.
    /// </summary>
    public void RemovePlayer(Guid playerId, TimeProvider? timeProvider = null)
    {
        var player = _players.FirstOrDefault(p => p.Id == playerId && p.IsActive)
            ?? throw new DomainException($"Active player {playerId} not found in this session");

        if (player.Role == PlayerRole.Host && _players.Count(p => p.IsActive) > 1)
            throw new ConflictException("Cannot remove the host while other players are active. Transfer host role first.");

        player.Deactivate();
        _turnOrder.Remove(playerId);

        var now = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        UpdatedAt = now;

        AddDomainEvent(new LiveSessionPlayerRemovedEvent(Id, playerId));
    }

    /// <summary>
    /// Sets the turn order for players.
    /// </summary>
    public void SetTurnOrder(List<Guid> playerIds, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(playerIds);

        var activePlayerIds = _players.Where(p => p.IsActive && p.Role != PlayerRole.Spectator)
            .Select(p => p.Id)
            .ToHashSet();

        foreach (var pid in playerIds)
        {
            if (!activePlayerIds.Contains(pid))
                throw new DomainException($"Player {pid} is not an active non-spectator player in this session");
        }

        _turnOrder.Clear();
        _turnOrder.AddRange(playerIds);

        var now = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        UpdatedAt = now;
    }

    // === Team Management ===

    /// <summary>
    /// Creates a new team in the session.
    /// </summary>
    public LiveSessionTeam CreateTeam(string name, string color, TimeProvider? timeProvider = null)
    {
        if (_teams.Count >= MaxTeams)
            throw new DomainException($"Cannot create more than {MaxTeams} teams in a session");

        var normalizedName = name.Trim().ToLowerInvariant();
        if (_teams.Any(t => string.Equals(t.Name.Trim().ToLowerInvariant(), normalizedName, StringComparison.Ordinal)))
            throw new DomainException($"Team with name '{name}' already exists in this session");

        var team = new LiveSessionTeam(Guid.NewGuid(), Id, name, color);
        _teams.Add(team);

        var now = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        UpdatedAt = now;

        return team;
    }

    /// <summary>
    /// Assigns a player to a team.
    /// </summary>
    public void AssignPlayerToTeam(Guid playerId, Guid teamId, TimeProvider? timeProvider = null)
    {
        var player = _players.FirstOrDefault(p => p.Id == playerId && p.IsActive)
            ?? throw new DomainException($"Active player {playerId} not found in this session");

        var team = _teams.FirstOrDefault(t => t.Id == teamId)
            ?? throw new DomainException($"Team {teamId} not found in this session");

        // Remove from old team if assigned
        if (player.TeamId.HasValue)
        {
            var oldTeam = _teams.FirstOrDefault(t => t.Id == player.TeamId.Value);
            oldTeam?.RemovePlayer(playerId);
        }

        team.AddPlayer(playerId);
        player.AssignToTeam(teamId);

        var now = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        UpdatedAt = now;
    }

    // === Lifecycle Management ===

    /// <summary>
    /// Transitions the session to Setup status.
    /// </summary>
    public void MoveToSetup(TimeProvider? timeProvider = null)
    {
        if (Status != LiveSessionStatus.Created)
            throw new ConflictException($"Cannot move to Setup from {Status} status. Must be Created.");

        Status = LiveSessionStatus.Setup;
        var now = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        UpdatedAt = now;
    }

    /// <summary>
    /// Starts the game session. Requires at least one active player.
    /// </summary>
    public void Start(TimeProvider? timeProvider = null)
    {
        if (Status != LiveSessionStatus.Setup && Status != LiveSessionStatus.Created)
            throw new ConflictException($"Cannot start session from {Status} status. Must be Created or Setup.");

        if (!HasPlayers)
            throw new ConflictException("Cannot start session without any active players");

        var now = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        Status = LiveSessionStatus.InProgress;
        StartedAt = now;
        CurrentTurnIndex = 1;
        UpdatedAt = now;

        AddDomainEvent(new LiveSessionStartedEvent(Id, now));
    }

    /// <summary>
    /// Pauses the session.
    /// </summary>
    public void Pause(TimeProvider? timeProvider = null)
    {
        if (Status != LiveSessionStatus.InProgress)
            throw new ConflictException($"Cannot pause session from {Status} status. Must be InProgress.");

        var now = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        Status = LiveSessionStatus.Paused;
        PausedAt = now;
        UpdatedAt = now;

        AddDomainEvent(new LiveSessionPausedEvent(Id, now));
    }

    /// <summary>
    /// Resumes a paused session.
    /// </summary>
    public void Resume(TimeProvider? timeProvider = null)
    {
        if (Status != LiveSessionStatus.Paused)
            throw new ConflictException($"Cannot resume session from {Status} status. Must be Paused.");

        var now = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        Status = LiveSessionStatus.InProgress;
        PausedAt = null;
        UpdatedAt = now;

        AddDomainEvent(new LiveSessionResumedEvent(Id, now));
    }

    /// <summary>
    /// Completes the session.
    /// </summary>
    public void Complete(TimeProvider? timeProvider = null)
    {
        if (Status != LiveSessionStatus.InProgress && Status != LiveSessionStatus.Paused)
            throw new ConflictException($"Cannot complete session from {Status} status. Must be InProgress or Paused.");

        var now = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        Status = LiveSessionStatus.Completed;
        CompletedAt = now;
        UpdatedAt = now;

        AddDomainEvent(new LiveSessionCompletedEvent(Id, now, CurrentTurnIndex));
    }

    // === Scoring ===

    /// <summary>
    /// Records a score for a player in a specific round and dimension.
    /// </summary>
    public void RecordScore(
        Guid playerId,
        int round,
        string dimension,
        int value,
        TimeProvider? timeProvider = null,
        string? unit = null)
    {
        if (Status != LiveSessionStatus.InProgress && Status != LiveSessionStatus.Paused)
            throw new ConflictException($"Cannot record scores in {Status} status. Must be InProgress or Paused.");

        _ = _players.FirstOrDefault(p => p.Id == playerId && p.IsActive)
            ?? throw new DomainException($"Active player {playerId} not found in this session");

        if (!ScoringConfig.HasDimension(dimension))
            throw new DomainException($"Dimension '{dimension}' is not enabled in this session's scoring config");

        var now = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        var score = new RoundScore(playerId, round, dimension, value, now, unit);

        // Replace existing score for same player/round/dimension
        _roundScores.RemoveAll(s =>
            s.PlayerId == playerId &&
            s.Round == round &&
            string.Equals(s.Dimension, dimension, StringComparison.OrdinalIgnoreCase));

        _roundScores.Add(score);
        RecalculatePlayerScores();
        UpdatedAt = now;

        AddDomainEvent(new LiveSessionScoreRecordedEvent(Id, playerId, round, dimension, value));
    }

    // === Turn Management ===

    /// <summary>
    /// Advances to the next turn.
    /// </summary>
    public void AdvanceTurn(TimeProvider? timeProvider = null)
    {
        if (Status != LiveSessionStatus.InProgress)
            throw new ConflictException($"Cannot advance turn in {Status} status. Must be InProgress.");

        if (CurrentTurnIndex >= MaxTurns)
            throw new DomainException($"Cannot exceed {MaxTurns} turns");

        var now = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;

        // Record the ending of current turn
        var currentTurnRecord = _turnRecords.LastOrDefault();
        if (currentTurnRecord != null && !currentTurnRecord.IsCompleted)
        {
            var completedRecord = new TurnRecord(
                currentTurnRecord.TurnIndex,
                currentTurnRecord.PlayerId,
                currentTurnRecord.StartedAt,
                currentTurnRecord.PhaseIndex,
                currentTurnRecord.PhaseName,
                now);

            _turnRecords.Remove(currentTurnRecord);
            _turnRecords.Add(completedRecord);
        }

        CurrentTurnIndex++;
        var nextPlayerId = GetCurrentTurnPlayerId();

        // Start new turn record
        _turnRecords.Add(new TurnRecord(CurrentTurnIndex, nextPlayerId, now));

        UpdatedAt = now;

        AddDomainEvent(new LiveSessionTurnAdvancedEvent(Id, CurrentTurnIndex, nextPlayerId));
    }

    // === State & Notes ===

    /// <summary>
    /// Updates the session notes.
    /// </summary>
    public void UpdateNotes(string? notes, TimeProvider? timeProvider = null)
    {
        if (notes != null)
        {
            var trimmed = notes.Trim();
            if (trimmed.Length > MaxNotesLength)
                throw new ValidationException($"Notes cannot exceed {MaxNotesLength} characters");
            notes = trimmed;
        }

        Notes = notes;
        var now = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        UpdatedAt = now;
    }

    /// <summary>
    /// Updates the game state (free-form JSON).
    /// </summary>
    public void UpdateGameState(JsonDocument? gameState, TimeProvider? timeProvider = null)
    {
        GameState = gameState;
        var now = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        UpdatedAt = now;
    }

    /// <summary>
    /// Links a toolkit to this session.
    /// </summary>
    public void LinkToolkit(Guid toolkitId, TimeProvider? timeProvider = null)
    {
        if (toolkitId == Guid.Empty)
            throw new ValidationException("Toolkit ID cannot be empty");

        ToolkitId = toolkitId;
        var now = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        UpdatedAt = now;
    }

    /// <summary>
    /// Sets the AI agent mode for the session.
    /// </summary>
    public void SetAgentMode(AgentSessionMode mode, Guid? chatSessionId = null, TimeProvider? timeProvider = null)
    {
        if (mode != AgentSessionMode.None && chatSessionId == null)
            throw new ValidationException("Chat session ID is required when enabling an AI agent");

        AgentMode = mode;
        ChatSessionId = mode == AgentSessionMode.None ? null : chatSessionId;

        var now = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        UpdatedAt = now;
    }

    // === Query Methods ===

    /// <summary>
    /// Gets a player by ID.
    /// </summary>
    public LiveSessionPlayer? GetPlayer(Guid playerId) =>
        _players.FirstOrDefault(p => p.Id == playerId);

    /// <summary>
    /// Gets the player whose turn it currently is.
    /// </summary>
    public Guid GetCurrentTurnPlayerId()
    {
        if (_turnOrder.Count == 0)
            return _players.FirstOrDefault(p => p.IsActive && p.Role != PlayerRole.Spectator)?.Id ?? Guid.Empty;

        var nonSpectators = _turnOrder.Where(pid =>
        {
            var p = _players.FirstOrDefault(pl => pl.Id == pid);
            return p != null && p.IsActive && p.Role != PlayerRole.Spectator;
        }).ToList();

        if (nonSpectators.Count == 0)
            return Guid.Empty;

        var turnIndex = (CurrentTurnIndex - 1) % nonSpectators.Count;
        return nonSpectators[turnIndex];
    }

    /// <summary>
    /// Gets a team by ID.
    /// </summary>
    public LiveSessionTeam? GetTeam(Guid teamId) =>
        _teams.FirstOrDefault(t => t.Id == teamId);

    // === Private Helpers ===

    private static string GenerateSessionCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // Excluded I/O/0/1 for readability
        Span<byte> bytes = stackalloc byte[SessionCodeLength];
        RandomNumberGenerator.Fill(bytes);

        var code = new char[SessionCodeLength];
        for (var i = 0; i < SessionCodeLength; i++)
            code[i] = chars[bytes[i] % chars.Length];

        return new string(code);
    }

    private void RecalculatePlayerScores()
    {
        var activePlayers = _players.Where(p => p.IsActive && p.Role != PlayerRole.Spectator).ToList();

        foreach (var player in activePlayers)
        {
            var totalScore = _roundScores
                .Where(s => s.PlayerId == player.Id)
                .Sum(s => s.Value);

            player.UpdateScore(totalScore, 0); // Rank calculated after all scores
        }

        // Calculate ranks (highest score = rank 1)
        var ranked = activePlayers
            .OrderByDescending(p => p.TotalScore)
            .ToList();

        for (var i = 0; i < ranked.Count; i++)
        {
            var rank = i + 1;
            // Handle ties: same score = same rank
            if (i > 0 && ranked[i].TotalScore == ranked[i - 1].TotalScore)
                rank = ranked[i - 1].CurrentRank;

            ranked[i].UpdateScore(ranked[i].TotalScore, rank);
        }
    }
}
