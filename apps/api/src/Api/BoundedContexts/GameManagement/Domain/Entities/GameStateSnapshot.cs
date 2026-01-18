using System.Text.Json;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.GameManagement.Domain.Entities;

/// <summary>
/// Represents a snapshot of game state at a specific point in time.
/// Used for undo/redo functionality and game state history tracking.
/// </summary>
internal sealed class GameStateSnapshot : Entity<Guid>
{
    public Guid SessionStateId { get; private set; }
    public JsonDocument State { get; private set; }
    public int TurnNumber { get; private set; }
    public string Description { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public string CreatedBy { get; private set; }

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
#pragma warning disable CS8618
    private GameStateSnapshot() : base()
#pragma warning restore CS8618
    {
    }

    /// <summary>
    /// Internal constructor for repository reconstruction from persistence.
    /// </summary>
    internal GameStateSnapshot(
        Guid id,
        Guid sessionStateId,
        JsonDocument state,
        int turnNumber,
        string description,
        DateTime createdAt,
        string createdBy) : base(id)
    {
        SessionStateId = sessionStateId;
        State = state;
        TurnNumber = turnNumber;
        Description = description;
        CreatedAt = createdAt;
        CreatedBy = createdBy;
    }

    /// <summary>
    /// Creates a new game state snapshot.
    /// </summary>
    internal static GameStateSnapshot Create(
        Guid id,
        Guid sessionStateId,
        JsonDocument state,
        int turnNumber,
        string description,
        string createdBy)
    {
        if (sessionStateId == Guid.Empty)
            throw new ArgumentException("SessionStateId cannot be empty", nameof(sessionStateId));

        ArgumentNullException.ThrowIfNull(state);

        if (turnNumber < 0)
            throw new ArgumentException("Turn number cannot be negative", nameof(turnNumber));

        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description cannot be empty", nameof(description));

        if (string.IsNullOrWhiteSpace(createdBy))
            throw new ArgumentException("CreatedBy cannot be empty", nameof(createdBy));

        if (description.Length > 500)
            throw new ArgumentException("Description cannot exceed 500 characters", nameof(description));

        return new GameStateSnapshot
        {
            Id = id,
            SessionStateId = sessionStateId,
            State = state,
            TurnNumber = turnNumber,
            Description = description.Trim(),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy.Trim()
        };
    }

    /// <summary>
    /// Returns snapshot state as JSON string.
    /// </summary>
    public string GetStateAsString()
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);
        State.WriteTo(writer);
        writer.Flush();
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }
}
