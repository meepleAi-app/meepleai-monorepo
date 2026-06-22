namespace Api.BoundedContexts.SessionTracking.Application.DTOs;

public sealed record TranslatedParagraphDto(
    Guid Id,
    int ParagraphNumber,
    string SourceTextEn,
    string TranslatedTextIt,
    IReadOnlyList<string> AppliedGlossaryTerms,
    DateTimeOffset CreatedAt);
