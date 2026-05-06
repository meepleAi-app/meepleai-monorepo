using System.Text.Json.Serialization;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetKbDocumentById;

/// <summary>
/// Full metadata DTO for a single KB document.
/// Returned by <c>GET /api/v1/kb-docs/{id}</c> (G4 goal).
///
/// Admin-only fields are decorated with <see cref="JsonIgnoreAttribute"/> so that
/// they are omitted from the JSON response when <c>null</c>.  The global
/// <c>ConfigureHttpJsonOptions</c> does NOT set <c>DefaultIgnoreCondition</c>, so
/// each admin-gated field must carry the annotation independently (Decision D4).
/// </summary>
internal sealed record KbDocumentDto(
    Guid Id,
    string Title,
    Guid? GameId,
    Guid? SharedGameId,
    string DocumentCategory,
    string ProcessingState,
    int TotalChunks,
    int PageCount,
    DateTime? IndexedAt,
    DateTime UploadedAt,
    string Language,
    string? VersionLabel,

    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? ProcessingError,

    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? RetryCount,

    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? FailedAtState
);
