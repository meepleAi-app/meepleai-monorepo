namespace Api.BoundedContexts.SessionTracking.Application.DTOs;

public sealed record GamebookGlossaryEntryDto(
    Guid Id,
    string TermEn,
    string TermIt,
    string Source,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<GlossaryContextDto> Contexts);

/// <summary>
/// A single provenance context (book + optional paragraph ref + optional definition)
/// for a glossary term. #2638 / SI-7. Serialized to camelCase by the API JSON options.
/// </summary>
public sealed record GlossaryContextDto(Guid BookId, string? ParagraphRef, string? Definition);
