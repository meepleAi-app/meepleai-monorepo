using System.Text.Json;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.GameManagement.Domain.Entities;

/// <summary>
/// Tracks the complete state of a game session, conforming to GameStateTemplate schema.
/// Supports snapshots for undo/history functionality and optimistic locking for concurrent updates.
/// </summary>
internal sealed class GameSessionState : AggregateRoot<Guid>
{
    public Guid GameSessionId { get; private set; }
    public Guid TemplateId { get; private set; }
    public JsonDocument CurrentState { get; private set; }
    public int Version { get; private set; }
    public DateTime LastUpdatedAt { get; private set; }
    public string LastUpdatedBy { get; private set; }

    private readonly List<GameStateSnapshot> _snapshots = new();
    public IReadOnlyList<GameStateSnapshot> Snapshots => _snapshots.AsReadOnly();

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
#pragma warning disable CS8618
    private GameSessionState() : base()
#pragma warning restore CS8618
    {
    }

    /// <summary>
    /// Internal constructor for repository reconstruction from persistence.
    /// </summary>
    internal GameSessionState(
        Guid id,
        Guid gameSessionId,
        Guid templateId,
        JsonDocument currentState,
        int version,
        DateTime lastUpdatedAt,
        string lastUpdatedBy,
        List<GameStateSnapshot> snapshots) : base(id)
    {
        GameSessionId = gameSessionId;
        TemplateId = templateId;
        CurrentState = currentState;
        Version = version;
        LastUpdatedAt = lastUpdatedAt;
        LastUpdatedBy = lastUpdatedBy;
        _snapshots = snapshots;
    }

    /// <summary>
    /// Creates a new game session state initialized from template defaults.
    /// </summary>
    public static GameSessionState Create(
        Guid id,
        Guid gameSessionId,
        Guid templateId,
        JsonDocument initialState,
        string createdBy)
    {
        if (gameSessionId == Guid.Empty)
            throw new ArgumentException("GameSessionId cannot be empty", nameof(gameSessionId));

        if (templateId == Guid.Empty)
            throw new ArgumentException("TemplateId cannot be empty", nameof(templateId));

        ArgumentNullException.ThrowIfNull(initialState);

        if (string.IsNullOrWhiteSpace(createdBy))
            throw new ArgumentException("CreatedBy cannot be empty", nameof(createdBy));

        var state = new GameSessionState
        {
            Id = id,
            GameSessionId = gameSessionId,
            TemplateId = templateId,
            CurrentState = initialState,
            Version = 1,
            LastUpdatedAt = DateTime.UtcNow,
            LastUpdatedBy = createdBy.Trim()
        };

        state.AddDomainEvent(new GameStateInitializedEvent(id, gameSessionId, templateId));

        return state;
    }

    /// <summary>
    /// Updates the current state with new data (supports JSON Patch or full replacement).
    /// Increments version for optimistic locking.
    /// </summary>
    public void UpdateState(JsonDocument newState, string updatedBy)
    {
        ArgumentNullException.ThrowIfNull(newState);

        if (string.IsNullOrWhiteSpace(updatedBy))
            throw new ArgumentException("UpdatedBy cannot be empty", nameof(updatedBy));

        CurrentState = newState;
        Version++;
        LastUpdatedAt = DateTime.UtcNow;
        LastUpdatedBy = updatedBy.Trim();

        AddDomainEvent(new GameStateUpdatedEvent(Id, GameSessionId, Version, DateTime.UtcNow));
    }

    /// <summary>
    /// Creates a snapshot of the current state for undo/history functionality.
    /// </summary>
    public GameStateSnapshot CreateSnapshot(int turnNumber, string description, string createdBy)
    {
        if (turnNumber < 0)
            throw new ArgumentException("Turn number cannot be negative", nameof(turnNumber));

        if (string.IsNullOrWhiteSpace(createdBy))
            throw new ArgumentException("CreatedBy cannot be empty", nameof(createdBy));

        // Check for duplicate turn numbers
        if (_snapshots.Any(s => s.TurnNumber == turnNumber))
            throw new InvalidOperationException($"Snapshot for turn {turnNumber} already exists");

        var snapshot = GameStateSnapshot.Create(
            id: Guid.NewGuid(),
            sessionStateId: Id,
            state: CurrentState,
            turnNumber: turnNumber,
            description: description ?? $"Snapshot at turn {turnNumber}",
            createdBy: createdBy
        );

        _snapshots.Add(snapshot);

        AddDomainEvent(new GameStateSnapshotCreatedEvent(Id, snapshot.Id, turnNumber));

        return snapshot;
    }

    /// <summary>
    /// Restores state from a snapshot and creates a new snapshot of the current state before restoring.
    /// </summary>
    public void RestoreFromSnapshot(Guid snapshotId, string restoredBy)
    {
        if (string.IsNullOrWhiteSpace(restoredBy))
            throw new ArgumentException("RestoredBy cannot be empty", nameof(restoredBy));

        var snapshot = _snapshots.FirstOrDefault(s => s.Id == snapshotId);
        if (snapshot == null)
            throw new NotFoundException("GameStateSnapshot", snapshotId.ToString());

        // Create backup snapshot before restoring
        CreateSnapshot(
            turnNumber: snapshot.TurnNumber + 1000, // Offset to avoid conflicts
            description: $"Pre-restore backup from turn {snapshot.TurnNumber}",
            createdBy: restoredBy
        );

        // Restore state
        CurrentState = snapshot.State;
        Version++;
        LastUpdatedAt = DateTime.UtcNow;
        LastUpdatedBy = restoredBy.Trim();

        AddDomainEvent(new GameStateRestoredEvent(Id, snapshotId, snapshot.TurnNumber));
    }

    /// <summary>
    /// Gets snapshot by turn number.
    /// </summary>
    public GameStateSnapshot? GetSnapshotByTurn(int turnNumber)
    {
        return _snapshots.FirstOrDefault(s => s.TurnNumber == turnNumber);
    }

    /// <summary>
    /// Gets the most recent snapshot.
    /// </summary>
    public GameStateSnapshot? GetLatestSnapshot()
    {
        return _snapshots.OrderByDescending(s => s.CreatedAt).FirstOrDefault();
    }

    /// <summary>
    /// Returns state as JSON string for serialization.
    /// </summary>
    public string GetStateAsString()
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);
        CurrentState.WriteTo(writer);
        writer.Flush();
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }
}
