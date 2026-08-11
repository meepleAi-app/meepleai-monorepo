using System;

namespace Api.Infrastructure.Entities.GameManagement;

/// <summary>
/// Persistence POCO for a photo attached to a GameNight recap gallery.
/// Standalone table (NOT an owned collection on the GameNightEvent aggregate):
/// the aggregate repository persists via a detached full-remap Update which cannot
/// insert new PK-set children (0-row UPDATE), so photos use their own tracked
/// repository. Issue #2724.
/// </summary>
public class GameNightPhotoEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameNightId { get; set; }
    public string BlobUrl { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public long FileSizeBytes { get; set; }
    public string Sha256Hash { get; set; } = string.Empty;
    public string? OcrText { get; set; }
    public double? OcrConfidence { get; set; }
    public string? Caption { get; set; }
    public Guid UploadedByUserId { get; set; }
    public DateTime UploadedAt { get; set; }
}
