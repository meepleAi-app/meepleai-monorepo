using Api.BoundedContexts.SessionTracking.Domain.Enums;

namespace Api.BoundedContexts.SessionTracking.Domain.Entities;

/// <summary>
/// A per-campaign EN→IT glossary entry used to apply consistent term translations.
/// Can be auto-bootstrapped from existing translations or created/edited manually.
/// Iter 1.B — Libro Game Nanolith dogfood demo.
/// C6 (2026-05-19): <see cref="FirstSeenBookId"/> records the book where a term
/// first appeared (nullable — may be unknown when bootstrapped or created manually).
/// </summary>
public sealed class GamebookGlossaryEntry
{
    public Guid Id { get; private set; }
    public Guid CampaignId { get; private set; }
    public string TermEn { get; private set; } = default!;
    public string TermIt { get; private set; } = default!;
    public GlossarySource Source { get; private set; }
    public Guid? FirstSeenBookId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    // EF parameterless constructor
    private GamebookGlossaryEntry() { }

    public static GamebookGlossaryEntry Create(
        Guid campaignId,
        string termEn,
        string termIt,
        GlossarySource source,
        Guid createdBy,
        Guid? firstSeenBookId = null)
    {
        if (campaignId == Guid.Empty)
            throw new ArgumentException("campaignId required", nameof(campaignId));
        if (string.IsNullOrWhiteSpace(termEn))
            throw new ArgumentException("termEn required", nameof(termEn));
        if (string.IsNullOrWhiteSpace(termIt))
            throw new ArgumentException("termIt required", nameof(termIt));
        if (createdBy == Guid.Empty)
            throw new ArgumentException("createdBy required", nameof(createdBy));
        if (firstSeenBookId.HasValue && firstSeenBookId.Value == Guid.Empty)
            throw new ArgumentException("firstSeenBookId cannot be Guid.Empty when set", nameof(firstSeenBookId));

        var now = DateTimeOffset.UtcNow;
        return new GamebookGlossaryEntry
        {
            Id = Guid.NewGuid(),
            CampaignId = campaignId,
            TermEn = termEn.Trim(),
            TermIt = termIt.Trim(),
            Source = source,
            FirstSeenBookId = firstSeenBookId,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = createdBy,
        };
    }

    /// <summary>
    /// Updates the Italian translation, flipping Source to Manual and stamping the audit fields.
    /// </summary>
    public void UpdateTermIt(string newValue, Guid editedBy)
    {
        if (string.IsNullOrWhiteSpace(newValue))
            throw new ArgumentException("newValue required", nameof(newValue));
        if (editedBy == Guid.Empty)
            throw new ArgumentException("editedBy required", nameof(editedBy));

        TermIt = newValue.Trim();
        Source = GlossarySource.Manual;
        UpdatedAt = DateTimeOffset.UtcNow;
        UpdatedBy = editedBy;
    }
}
