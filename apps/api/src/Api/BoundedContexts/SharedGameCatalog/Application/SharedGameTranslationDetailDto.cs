namespace Api.BoundedContexts.SharedGameCatalog.Application;

/// <summary>
/// Admin-facing detail DTO for a single translation row, including audit metadata
/// and the PostgreSQL <c>xmin</c> token required for optimistic concurrency on PUT.
/// Issue #2339 — sub-PR 1/3.
/// </summary>
/// <remarks>
/// <see cref="Source"/> holds the kebab-case persisted form to keep the wire shape
/// stable across enum extensions (see ADR pattern for catalog enums). Clients submit
/// <see cref="Xmin"/> back as part of <c>UpdateGameTranslationCommand</c> so EF Core
/// can detect lost updates.
/// </remarks>
public sealed record SharedGameTranslationDetailDto(
    Guid Id,
    Guid GameId,
    string Locale,
    string Title,
    string? Description,
    string Source,
    DateTimeOffset CreatedAt,
    Guid? CreatedBy,
    DateTimeOffset? UpdatedAt,
    Guid? UpdatedBy,
    uint Xmin);
