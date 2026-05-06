using System.Text.Json.Serialization;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetKbChunkById;

/// <summary>
/// Full-content DTO for a single text chunk, including prev/next navigation IDs
/// and a hierarchical heading breadcrumb (headingPath).
/// Returned by <c>GET /api/v1/kb-docs/{id}/chunks/{chunkId}</c> (G2 goal).
///
/// Admin-only fields are decorated with <see cref="JsonIgnoreAttribute"/> so that
/// they are omitted from the JSON response when <c>null</c>.  The global
/// <c>ConfigureHttpJsonOptions</c> does NOT set <c>DefaultIgnoreCondition</c>, so
/// each admin-gated field must carry the annotation independently (Decision D4).
/// </summary>
internal sealed record KbChunkDetailDto(
    Guid ChunkId,
    string Content,
    int? PageNumber,
    int Position,
    short Level,
    IReadOnlyList<string> HeadingPath,
    Guid? PrevChunkId,
    Guid? NextChunkId,

    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    Guid? VectorId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? CharacterCount,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? ElementType,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? EmbeddingStatus,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    Guid? ParentChunkId
);
