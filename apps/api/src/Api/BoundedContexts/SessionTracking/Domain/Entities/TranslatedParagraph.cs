using Api.BoundedContexts.SessionTracking.Domain.Enums;

namespace Api.BoundedContexts.SessionTracking.Domain.Entities;

/// <summary>
/// Immutable history record of a translated gamebook paragraph.
/// Once created, no mutations are allowed — append-only pattern.
/// Iter 1.B — Libro Game Nanolith dogfood demo.
/// C3 (2026-05-19): <see cref="GameBookId"/> added so the paragraph is anchored
/// to a specific book; uniqueness is now per (campaign, book, paragraph).
/// </summary>
public sealed class TranslatedParagraph
{
    public Guid Id { get; private set; }
    public Guid CampaignId { get; private set; }
    public Guid GameBookId { get; private set; }
    public Guid PhotoArtifactId { get; private set; }
    public int ParagraphNumber { get; private set; }
    public GamebookPageType PageType { get; private set; }
    public string SourceTextEn { get; private set; } = default!;
    public string TranslatedTextIt { get; private set; } = default!;
    // Issue #886: typed as string[] (not IReadOnlyList<string>) because Npgsql maps string[]
    // natively to Postgres text[]. EF Core 8+ infers IReadOnlyList<string> as a primitive
    // collection and inserts an element converter that conflicts with any user HasConversion.
    // Treat the array as immutable: the private setter prevents reassignment, but callers
    // holding the reference must not mutate elements in place. DTO projections correctly
    // widen back to IReadOnlyList<string> at the boundary.
    public string[] AppliedGlossaryTerms { get; private set; } = Array.Empty<string>();
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }

    // EF parameterless constructor
    private TranslatedParagraph() { }

    public static TranslatedParagraph Create(
        Guid campaignId,
        Guid gameBookId,
        Guid photoArtifactId,
        int paragraphNumber,
        GamebookPageType pageType,
        string sourceEn,
        string translatedIt,
        IEnumerable<string> appliedTerms,
        Guid createdBy)
    {
        if (campaignId == Guid.Empty)
            throw new ArgumentException("campaignId required", nameof(campaignId));
        if (gameBookId == Guid.Empty)
            throw new ArgumentException("gameBookId required", nameof(gameBookId));
        if (photoArtifactId == Guid.Empty)
            throw new ArgumentException("photoArtifactId required", nameof(photoArtifactId));
        if (paragraphNumber < 0)
            throw new ArgumentException("paragraphNumber must be >= 0", nameof(paragraphNumber));
        if (string.IsNullOrWhiteSpace(sourceEn))
            throw new ArgumentException("sourceEn required", nameof(sourceEn));
        if (string.IsNullOrWhiteSpace(translatedIt))
            throw new ArgumentException("translatedIt required", nameof(translatedIt));
        if (createdBy == Guid.Empty)
            throw new ArgumentException("createdBy required", nameof(createdBy));

        return new TranslatedParagraph
        {
            Id = Guid.NewGuid(),
            CampaignId = campaignId,
            GameBookId = gameBookId,
            PhotoArtifactId = photoArtifactId,
            ParagraphNumber = paragraphNumber,
            PageType = pageType,
            SourceTextEn = sourceEn.Trim(),
            TranslatedTextIt = translatedIt.Trim(),
            AppliedGlossaryTerms = appliedTerms != null ? appliedTerms.ToArray() : Array.Empty<string>(),
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = createdBy,
        };
    }
}
