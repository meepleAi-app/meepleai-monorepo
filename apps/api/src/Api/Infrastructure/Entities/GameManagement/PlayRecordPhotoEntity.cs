using System;

namespace Api.Infrastructure.Entities.GameManagement;

/// <summary>
/// Persistence POCO for PlayRecordPhoto.
/// Maps domain PlayRecordPhoto child entity to the play_record_photos table.
/// #2436 PR-B (ADR-067).
/// </summary>
public class PlayRecordPhotoEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlayRecordId { get; set; }
    public string BlobUrl { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public long FileSizeBytes { get; set; }
    public string Sha256Hash { get; set; } = string.Empty;
    public string? OcrText { get; set; }
    public double? OcrConfidence { get; set; }
    public string? Caption { get; set; }
    public Guid UploadedByUserId { get; set; }
    public DateTime UploadedAt { get; set; }

    // Navigation property
    public PlayRecordEntity? PlayRecord { get; set; }
}
