using Api.BoundedContexts.SessionTracking.Domain.Enums;

namespace Api.BoundedContexts.SessionTracking.Domain.Entities;

/// <summary>
/// Immutable history record of a translated gamebook paragraph.
/// Once created, no mutations are allowed — append-only pattern.
/// Iter 1.B — Libro Game Nanolith dogfood demo.
/// </summary>
public sealed class TranslatedParagraph
{
    public Guid Id { get; private set; }
    public Guid CampaignId { get; private set; }
    public Guid PhotoArtifactId { get; private set; }
    public int ParagraphNumber { get; private set; }
    public GamebookPageType PageType { get; private set; }
    public string SourceTextEn { get; private set; } = default!;
    public string TranslatedTextIt { get; private set; } = default!;
    public IReadOnlyList<string> AppliedGlossaryTerms { get; private set; } = Array.Empty<string>();
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }

    // EF parameterless constructor
    private TranslatedParagraph() { }

    public static TranslatedParagraph Create(
        Guid campaignId,
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
            PhotoArtifactId = photoArtifactId,
            ParagraphNumber = paragraphNumber,
            PageType = pageType,
            SourceTextEn = sourceEn.Trim(),
            TranslatedTextIt = translatedIt.Trim(),
            AppliedGlossaryTerms = appliedTerms != null ? appliedTerms.ToList().AsReadOnly() : (IReadOnlyList<string>)Array.Empty<string>(),
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = createdBy,
        };
    }
}
