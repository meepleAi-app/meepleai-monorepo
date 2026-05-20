using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.ValueObjects;

namespace Api.BoundedContexts.SessionTracking.Domain.Entities;

/// <summary>
/// Represents a photo uploaded for OCR + translation within a gamebook campaign session.
/// State machine: Uploaded → Segmented → Translated | Failed.
/// Retention: 24 hours (ExpiresAt enforced by cleanup job in later iteration).
/// Iter 1.B — Libro Game Nanolith dogfood demo.
/// C4 (2026-05-19): <see cref="GameBookId"/> added so photos are anchored to a specific
/// book; downstream consumers use the linked <c>GameBook</c> for role classification.
/// C5 (2026-05-19): Removed <c>PageType</c>; consumers classify roles via the linked
/// <c>GameBook</c> instead of an inline enum.
/// </summary>
public sealed class GamebookPhotoArtifact
{
    public Guid Id { get; private set; }
    public Guid CampaignId { get; private set; }
    public Guid GameBookId { get; private set; }
    public string S3Key { get; private set; } = default!;
    public PhotoArtifactStatus Status { get; private set; }
    public string? OcrFullText { get; private set; }
    public IReadOnlyList<GamebookSegment> Segments { get; private set; } = Array.Empty<GamebookSegment>();
    public string? FailureReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }

    // EF parameterless constructor
    private GamebookPhotoArtifact() { }

    public static GamebookPhotoArtifact Create(Guid campaignId, Guid gameBookId, string s3Key)
    {
        if (campaignId == Guid.Empty)
            throw new ArgumentException("campaignId required", nameof(campaignId));
        if (gameBookId == Guid.Empty)
            throw new ArgumentException("gameBookId required", nameof(gameBookId));
        if (string.IsNullOrWhiteSpace(s3Key))
            throw new ArgumentException("s3Key required", nameof(s3Key));

        var now = DateTimeOffset.UtcNow;
        return new GamebookPhotoArtifact
        {
            Id = Guid.NewGuid(),
            CampaignId = campaignId,
            GameBookId = gameBookId,
            S3Key = s3Key.Trim(),
            Status = PhotoArtifactStatus.Uploaded,
            CreatedAt = now,
            ExpiresAt = now.AddHours(24),
        };
    }

    /// <summary>
    /// Records OCR segments and transitions status from Uploaded to Segmented.
    /// </summary>
    public void RecordSegments(IEnumerable<GamebookSegment> segments, string ocrFullText)
    {
        if (Status != PhotoArtifactStatus.Uploaded)
            throw new InvalidOperationException(
                $"RecordSegments can only be called when Status is Uploaded. Current status: {Status}");

        Segments = segments != null ? segments.ToList().AsReadOnly() : (IReadOnlyList<GamebookSegment>)Array.Empty<GamebookSegment>();
        OcrFullText = ocrFullText;
        Status = PhotoArtifactStatus.Segmented;
    }

    /// <summary>
    /// Transitions status from Segmented to Translated.
    /// </summary>
    public void MarkTranslated()
    {
        if (Status != PhotoArtifactStatus.Segmented)
            throw new InvalidOperationException(
                $"MarkTranslated can only be called when Status is Segmented. Current status: {Status}");

        Status = PhotoArtifactStatus.Translated;
    }

    /// <summary>
    /// Marks the artifact as failed from any state.
    /// </summary>
    public void MarkFailed(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("reason required", nameof(reason));

        Status = PhotoArtifactStatus.Failed;
        FailureReason = reason.Trim();
    }
}
