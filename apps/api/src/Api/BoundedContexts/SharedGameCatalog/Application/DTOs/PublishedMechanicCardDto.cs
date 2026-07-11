namespace Api.BoundedContexts.SharedGameCatalog.Application.DTOs;

/// <summary>
/// Public read projection of a published <see cref="Domain.Aggregates.MechanicCard"/> for the
/// login-gated card page <c>/games/{id}/card</c> (#528 ME-M1.6). Built by deserializing the immutable
/// <c>mechanic_cards.content</c> snapshot (<see cref="Domain.ValueObjects.MechanicCardContent"/>) and
/// GROUPING the flat claim list into the 6 sections in enum order. Intentionally omits the admin-only
/// guardrail validation telemetry (down-projected passed/failed is admin review nuance, not public).
/// A suppressed / absent card never reaches here — the handler returns null → 404.
/// </summary>
public sealed record PublishedMechanicCardDto(
    Guid CardId,
    Guid SharedGameId,
    string Title,
    int Version,
    DateTime PublishedAt,
    string GameName,
    string? Publisher,
    string Language,
    IReadOnlyList<PublishedMechanicCardSectionDto> Sections,
    // #530 — extra context for the APA-style "copy citation" output. SourceAnalysisId comes from the
    // content snapshot; PublicationYear (SharedGame) and DocumentName (source PDF Title/FileName) are
    // resolved at read time. Any may be null when unknown — the FE substitutes [n.d.] / a fallback.
    Guid SourceAnalysisId,
    int? PublicationYear,
    string? DocumentName);

/// <summary>One of the 6 mechanic sections (Summary/Mechanics/Victory/Resources/Phases/Faq).</summary>
public sealed record PublishedMechanicCardSectionDto(
    string Section,
    IReadOnlyList<PublishedMechanicCardClaimDto> Claims);

/// <summary>A single reviewed claim with its verbatim citations.</summary>
public sealed record PublishedMechanicCardClaimDto(
    Guid Id,
    string Claim,
    IReadOnlyList<PublishedMechanicCardCitationDto> Citations);

/// <summary>A verbatim source citation: the origin PDF, its page, and the quoted text.</summary>
public sealed record PublishedMechanicCardCitationDto(
    Guid PdfId,
    int PdfPage,
    string Quote);
