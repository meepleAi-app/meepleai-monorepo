using System.Text.Json.Serialization;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetKbChunks;

/// <summary>
/// Summary DTO for a single text chunk in a paginated list.
/// Returned as items within <see cref="KbChunkListDto"/>.
///
/// Admin-only fields are decorated with <see cref="JsonIgnoreAttribute"/> so that
/// they are omitted from the JSON response when <c>null</c>.  The global
/// <c>ConfigureHttpJsonOptions</c> does NOT set <c>DefaultIgnoreCondition</c>, so
/// each admin-gated field must carry the annotation independently (Decision D4).
/// </summary>
internal sealed record KbChunkSummaryDto(
    Guid ChunkId,
    int? PageNumber,
    int Position,
    short Level,
    IReadOnlyList<string> HeadingPath,
    string Snippet,

    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    Guid? VectorId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? CharacterCount,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? ElementType,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? EmbeddingStatus
);
