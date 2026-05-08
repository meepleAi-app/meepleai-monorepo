namespace Api.BoundedContexts.SessionTracking.Application.DTOs;

public sealed record TranslateChunk(
    string Delta,
    bool IsComplete,
    Guid? ParagraphId = null,
    IReadOnlyList<string>? AppliedTerms = null);
