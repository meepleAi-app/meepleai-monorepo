using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.GameManagement.Domain.Entities.WhiteboardState;

/// <summary>
/// Represents the collaborative whiteboard state for a live game session.
/// Supports freehand strokes (delta-based) and a structured layer (tokens, grid).
/// Issue #4971: WhiteboardState Entity + Endpoints + SSE.
/// </summary>
#pragma warning disable MA0049 // Type name matches containing namespace - intentional DDD folder structure
internal sealed class WhiteboardState : Entity<Guid>
#pragma warning restore MA0049
{
    private readonly List<WhiteboardStroke> _strokes = new();

    public Guid SessionId { get; private set; }

    /// <summary>Ordered list of freehand strokes.</summary>
    public IReadOnlyList<WhiteboardStroke> Strokes => _strokes.AsReadOnly();

    /// <summary>Opaque JSON representing structured layer (token positions, grid config). Max 100 KB.</summary>
    public string StructuredJson { get; private set; } = "{}";

    /// <summary>ID of the user who last modified the whiteboard.</summary>
    public Guid LastModifiedBy { get; private set; }

    public DateTime LastModifiedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // Private parameterless constructor for EF Core / Restore()
#pragma warning disable CS8618
    private WhiteboardState() : base() { }
#pragma warning restore CS8618

    /// <summary>Creates a new, empty WhiteboardState for a session.</summary>
    public WhiteboardState(Guid id, Guid sessionId, Guid createdByUserId) : base(id)
    {
        if (sessionId == Guid.Empty)
            throw new ArgumentException("SessionId cannot be empty.", nameof(sessionId));
        if (createdByUserId == Guid.Empty)
            throw new ArgumentException("CreatedByUserId cannot be empty.", nameof(createdByUserId));

        SessionId = sessionId;
        LastModifiedBy = createdByUserId;
        LastModifiedAt = DateTime.UtcNow;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>Restores a WhiteboardState from persistence without re-triggering creation logic.</summary>
    internal static WhiteboardState Restore(
        Guid id,
        Guid sessionId,
        IEnumerable<WhiteboardStroke> strokes,
        string structuredJson,
        Guid lastModifiedBy,
        DateTime lastModifiedAt,
        DateTime createdAt)
    {
        var ws = new WhiteboardState
        {
            Id = id,
            SessionId = sessionId,
            StructuredJson = structuredJson,
            LastModifiedBy = lastModifiedBy,
            LastModifiedAt = lastModifiedAt,
            CreatedAt = createdAt,
        };
        ws._strokes.AddRange(strokes);
        return ws;
    }

    /// <summary>
    /// Adds a new freehand stroke. Throws <see cref="InvalidOperationException"/> if strokeId already exists.
    /// </summary>
    /// <returns>The added stroke.</returns>
    public WhiteboardStroke AddStroke(string strokeId, string dataJson, Guid modifiedBy)
    {
        if (string.IsNullOrWhiteSpace(strokeId))
            throw new ArgumentException("Stroke ID cannot be empty.", nameof(strokeId));
        if (_strokes.Any(s => string.Equals(s.Id, strokeId, StringComparison.Ordinal)))
            throw new InvalidOperationException($"Stroke '{strokeId}' already exists.");

        var stroke = new WhiteboardStroke(strokeId, dataJson ?? "{}");
        _strokes.Add(stroke);
        LastModifiedBy = modifiedBy;
        LastModifiedAt = DateTime.UtcNow;
        return stroke;
    }

    /// <summary>
    /// Removes a freehand stroke by ID. Returns the removed stroke ID.
    /// Throws <see cref="InvalidOperationException"/> if stroke not found.
    /// </summary>
    public string RemoveStroke(string strokeId, Guid modifiedBy)
    {
        if (string.IsNullOrWhiteSpace(strokeId))
            throw new ArgumentException("Stroke ID cannot be empty.", nameof(strokeId));

        var stroke = _strokes.FirstOrDefault(s => string.Equals(s.Id, strokeId, StringComparison.Ordinal));
        if (stroke == null)
            throw new InvalidOperationException($"Stroke '{strokeId}' not found.");

        _strokes.Remove(stroke);
        LastModifiedBy = modifiedBy;
        LastModifiedAt = DateTime.UtcNow;
        return strokeId;
    }

    /// <summary>
    /// Replaces the structured layer. Validates max 100 KB.
    /// </summary>
    public void UpdateStructured(string structuredJson, Guid modifiedBy)
    {
        const int MaxBytes = 100 * 1024; // 100 KB
        if (structuredJson?.Length > MaxBytes)
            throw new ArgumentException($"StructuredJson exceeds maximum size of 100 KB.", nameof(structuredJson));

        StructuredJson = structuredJson ?? "{}";
        LastModifiedBy = modifiedBy;
        LastModifiedAt = DateTime.UtcNow;
    }

    /// <summary>Clears all strokes and resets the structured layer.</summary>
    public void Clear(Guid modifiedBy)
    {
        _strokes.Clear();
        StructuredJson = "{}";
        LastModifiedBy = modifiedBy;
        LastModifiedAt = DateTime.UtcNow;
    }
}
