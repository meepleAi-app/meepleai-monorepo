namespace Api.Infrastructure.Entities.GameManagement;

/// <summary>
/// EF Core persistence entity for WhiteboardState.
/// Issue #4971: WhiteboardState Entity + Endpoints + SSE.
/// </summary>
public class WhiteboardStateEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }

    /// <summary>JSON array of strokes: [{id, dataJson}]</summary>
    public string StrokesJson { get; set; } = "[]";

    /// <summary>Opaque JSON for the structured layer.</summary>
    public string StructuredJson { get; set; } = "{}";

    public Guid LastModifiedBy { get; set; }
    public DateTime LastModifiedAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
