using System;
using Api.SharedKernel.Domain.Entities;
using Api.SharedKernel.Domain.Exceptions;

namespace Api.BoundedContexts.GameManagement.Domain.Entities;

/// <summary>
/// A photo attached to a <see cref="PlayRecord"/> (scoreboard capture, party shot).
/// Child entity of the PlayRecord aggregate. #2436 PR-B (ADR-067).
/// </summary>
internal sealed class PlayRecordPhoto : Entity<Guid>
{
    public Guid PlayRecordId { get; private set; }
    public string BlobUrl { get; private set; }
    public string? ThumbnailUrl { get; private set; }
    public long FileSizeBytes { get; private set; }
    public string Sha256Hash { get; private set; }
    public string? OcrText { get; private set; }
    public double? OcrConfidence { get; private set; }
    public string? Caption { get; private set; }
    public Guid UploadedByUserId { get; private set; }
    public DateTime UploadedAt { get; private set; }

#pragma warning disable CS8618
    private PlayRecordPhoto() : base() { }
#pragma warning restore CS8618

    internal PlayRecordPhoto(
        Guid id,
        Guid playRecordId,
        string blobUrl,
        string? thumbnailUrl,
        long fileSizeBytes,
        string sha256Hash,
        string? ocrText,
        double? ocrConfidence,
        string? caption,
        Guid uploadedByUserId,
        DateTime uploadedAt) : base(id)
    {
        if (playRecordId == Guid.Empty)
            throw new ArgumentException("PlayRecordId cannot be empty", nameof(playRecordId));
        if (string.IsNullOrWhiteSpace(blobUrl))
            throw new ValidationException("BlobUrl cannot be empty");
        if (string.IsNullOrWhiteSpace(sha256Hash))
            throw new ValidationException("Sha256Hash cannot be empty");
        if (caption is { Length: > 500 })
            throw new ValidationException("Caption cannot exceed 500 characters");

        PlayRecordId = playRecordId;
        BlobUrl = blobUrl;
        ThumbnailUrl = thumbnailUrl;
        FileSizeBytes = fileSizeBytes;
        Sha256Hash = sha256Hash;
        OcrText = ocrText;
        OcrConfidence = ocrConfidence;
        Caption = caption?.Trim();
        UploadedByUserId = uploadedByUserId;
        UploadedAt = uploadedAt;
    }
}
