using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.Entities;
using Api.SharedKernel.Domain.Exceptions;

namespace Api.BoundedContexts.GameManagement.Domain.Entities;

/// <summary>
/// PlayRecord aggregate root representing a historical record of a board game play session.
/// Distinct from GameSession (real-time AI agent play state) - this is for historical logging.
/// </summary>
internal sealed class PlayRecord : AggregateRoot<Guid>
{
    // Game Association (Optional)
    public Guid? GameId { get; private set; }
    public string GameName { get; private set; }

    // Ownership & Permissions
    public Guid CreatedByUserId { get; private set; }
    public PlayRecordVisibility Visibility { get; private set; }
    public Guid? GroupId { get; private set; }

    // Session Metadata
    public DateTime SessionDate { get; private set; }
    public DateTime? StartTime { get; private set; }
    public DateTime? EndTime { get; private set; }
    public TimeSpan? Duration { get; private set; }
    public PlayRecordStatus Status { get; private set; }
    public string? Notes { get; private set; }
    public string? Location { get; private set; }

    // Players & Scoring
    private readonly List<RecordPlayer> _players = new();
    public IReadOnlyList<RecordPlayer> Players => _players.AsReadOnly();

    // Scoring Configuration
    public SessionScoringConfig ScoringConfig { get; private set; }

    // Audit
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
#pragma warning disable CS8618
    private PlayRecord() : base()
#pragma warning restore CS8618
    {
    }

    /// <summary>
    /// Creates a play record for a game in the catalog.
    /// </summary>
    public static PlayRecord CreateWithGame(
        Guid id,
        Guid gameId,
        string gameName,
        Guid userId,
        DateTime sessionDate,
        PlayRecordVisibility visibility,
        TimeProvider? timeProvider = null,
        Guid? groupId = null,
        SessionScoringConfig? scoringConfig = null)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Id cannot be empty", nameof(id));

        if (gameId == Guid.Empty)
            throw new ArgumentException("GameId cannot be empty", nameof(gameId));

        if (string.IsNullOrWhiteSpace(gameName))
            throw new ValidationException("Game name cannot be empty");

        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty", nameof(userId));

        var provider = timeProvider ?? TimeProvider.System;
        var now = provider.GetUtcNow().UtcDateTime;

        if (sessionDate > now)
            throw new ValidationException("Session date cannot be in the future");

        if (visibility == PlayRecordVisibility.Group && groupId == null)
            throw new ValidationException("GroupId is required for group visibility");

        var record = new PlayRecord
        {
            Id = id,
            GameId = gameId,
            GameName = gameName.Trim(),
            CreatedByUserId = userId,
            Visibility = visibility,
            GroupId = groupId,
            SessionDate = sessionDate,
            Status = PlayRecordStatus.Planned,
            ScoringConfig = scoringConfig ?? SessionScoringConfig.CreateDefault(),
            CreatedAt = now,
            UpdatedAt = now
        };

        record.AddDomainEvent(new PlayRecordCreatedEvent(record.Id, userId, gameId, gameName));
        return record;
    }

    /// <summary>
    /// Creates a free-form play record for a game not in the catalog.
    /// </summary>
    public static PlayRecord CreateFreeForm(
        Guid id,
        string gameName,
        Guid userId,
        DateTime sessionDate,
        PlayRecordVisibility visibility,
        SessionScoringConfig scoringConfig,
        TimeProvider? timeProvider = null,
        Guid? groupId = null)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Id cannot be empty", nameof(id));

        if (string.IsNullOrWhiteSpace(gameName))
            throw new ValidationException("Game name cannot be empty");

        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty", nameof(userId));

        var provider = timeProvider ?? TimeProvider.System;
        var now = provider.GetUtcNow().UtcDateTime;

        if (sessionDate > now)
            throw new ValidationException("Session date cannot be in the future");

        ArgumentNullException.ThrowIfNull(scoringConfig);

        if (visibility == PlayRecordVisibility.Group && groupId == null)
            throw new ValidationException("GroupId is required for group visibility");

        var record = new PlayRecord
        {
            Id = id,
            GameId = null,
            GameName = gameName.Trim(),
            CreatedByUserId = userId,
            Visibility = visibility,
            GroupId = groupId,
            SessionDate = sessionDate,
            Status = PlayRecordStatus.Planned,
            ScoringConfig = scoringConfig,
            CreatedAt = now,
            UpdatedAt = now
        };

        record.AddDomainEvent(new PlayRecordCreatedEvent(record.Id, userId, null, gameName));
        return record;
    }

    /// <summary>
    /// Adds a player to the play record.
    /// Can be a registered user or an external guest.
    /// </summary>
    public void AddPlayer(Guid? userId, string displayName, TimeProvider? timeProvider = null)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ValidationException("Player display name cannot be empty");

        // Check for duplicate registered user
        if (userId.HasValue && _players.Any(p => p.UserId == userId))
            throw new DomainException($"User {userId} is already a player in this record");

        // Check for duplicate display name (case-insensitive)
        var normalizedName = displayName.Trim().ToLowerInvariant();
        if (_players.Any(p => string.Equals(p.DisplayName.ToLowerInvariant(), normalizedName, StringComparison.Ordinal)))
            throw new DomainException($"Player with display name '{displayName}' already exists in this record");

        if (_players.Count >= 100)
            throw new DomainException("Cannot add more than 100 players to a record");

        var player = new RecordPlayer(
            Guid.NewGuid(),
            Id,
            userId,
            displayName);

        _players.Add(player);
        UpdatedAt = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;

        AddDomainEvent(new PlayerAddedToRecordEvent(Id, player.Id, userId, displayName));
    }

    /// <summary>
    /// Records a score for a player.
    /// </summary>
    public void RecordScore(Guid playerId, RecordScore score, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(score);

        var player = _players.FirstOrDefault(p => p.Id == playerId)
            ?? throw new DomainException($"Player {playerId} not found in record");

        // Validate dimension is in scoring config
        if (!ScoringConfig.HasDimension(score.Dimension))
            throw new ValidationException($"Scoring dimension '{score.Dimension}' is not enabled for this record");

        player.RecordScore(score);
        UpdatedAt = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;

        AddDomainEvent(new RecordScoreRecordedEvent(Id, playerId, score.Dimension, score.Value));
    }

    /// <summary>
    /// Starts the play record (moves from Planned to InProgress).
    /// </summary>
    public void Start(TimeProvider? timeProvider = null)
    {
        if (Status != PlayRecordStatus.Planned)
            throw new ConflictException($"Cannot start record in {Status} status. Must be Planned.");

        var now = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        Status = PlayRecordStatus.InProgress;
        StartTime = now;
        UpdatedAt = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;

        AddDomainEvent(new PlayRecordStartedEvent(Id, StartTime.Value));
    }

    /// <summary>
    /// Completes the play record with optional manual duration.
    /// Duration is calculated from StartTime/EndTime if not provided.
    /// </summary>
    public void Complete(TimeSpan? manualDuration = null, TimeProvider? timeProvider = null)
    {
        if (Status == PlayRecordStatus.Completed)
            throw new ConflictException("Record is already completed");

        var now = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        Status = PlayRecordStatus.Completed;
        EndTime = now;

        // Calculate or use manual duration
        if (manualDuration.HasValue)
        {
            if (manualDuration.Value < TimeSpan.Zero)
                throw new ValidationException("Duration cannot be negative");

            if (manualDuration.Value > TimeSpan.FromDays(30))
                throw new ValidationException("Duration cannot exceed 30 days");

            Duration = manualDuration.Value;
        }
        else if (StartTime.HasValue)
        {
            Duration = EndTime.Value - StartTime.Value;
        }
        else
        {
            Duration = TimeSpan.Zero;
        }

        UpdatedAt = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;

        AddDomainEvent(new PlayRecordCompletedEvent(Id, Duration.Value));
    }

    /// <summary>
    /// Updates record details. Allowed even after completion for corrections.
    /// </summary>
    public void UpdateDetails(
        DateTime? sessionDate = null,
        string? notes = null,
        string? location = null,
        TimeProvider? timeProvider = null)
    {
        var now = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;

        if (sessionDate.HasValue)
        {
            if (sessionDate.Value > now)
                throw new ValidationException("Session date cannot be in the future");

            SessionDate = sessionDate.Value;
        }

        if (notes != null)
        {
            var trimmed = notes.Trim();
            if (trimmed.Length > 2000)
                throw new ValidationException("Notes cannot exceed 2000 characters");

            Notes = trimmed;
        }

        if (location != null)
        {
            var trimmed = location.Trim();
            if (trimmed.Length > 255)
                throw new ValidationException("Location cannot exceed 255 characters");

            Location = trimmed;
        }

        UpdatedAt = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;

        AddDomainEvent(new PlayRecordUpdatedEvent(Id));
    }

    /// <summary>
    /// Archives the completed record.
    /// </summary>
    public void Archive(TimeProvider? timeProvider = null)
    {
        if (Status != PlayRecordStatus.Completed)
            throw new ConflictException("Only completed records can be archived");

        Status = PlayRecordStatus.Archived;
        UpdatedAt = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
    }

    /// <summary>
    /// Gets the number of players in the record.
    /// </summary>
    public int PlayerCount => _players.Count;

    /// <summary>
    /// Checks if the record has any players.
    /// </summary>
    public bool HasPlayers => _players.Count > 0;

    /// <summary>
    /// Gets player by ID.
    /// </summary>
    public RecordPlayer? GetPlayer(Guid playerId) =>
        _players.FirstOrDefault(p => p.Id == playerId);
}
