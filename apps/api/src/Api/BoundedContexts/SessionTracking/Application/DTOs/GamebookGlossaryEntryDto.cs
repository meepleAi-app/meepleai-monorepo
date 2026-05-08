namespace Api.BoundedContexts.SessionTracking.Application.DTOs;

public sealed record GamebookGlossaryEntryDto(
    Guid Id,
    string TermEn,
    string TermIt,
    string Source,
    DateTimeOffset UpdatedAt);
