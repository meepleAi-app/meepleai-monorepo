namespace Api.BoundedContexts.SessionTracking.Application.DTOs;

public sealed record TranslatedParagraphDto(
    Guid Id,
    int ParagraphNumber,
    string PageType,
    string SourceTextEn,
    string TranslatedTextIt,
    IReadOnlyList<string> AppliedGlossaryTerms,
    DateTimeOffset CreatedAt);
