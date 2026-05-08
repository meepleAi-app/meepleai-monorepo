namespace Api.BoundedContexts.SessionTracking.Application.DTOs;

public sealed record GamebookSegmentDto(int ParagraphNumber, string SourceText, string? BoundingBox);

public sealed record GamebookPhotoArtifactDto(
    Guid Id,
    Guid CampaignId,
    string PageType,
    string Status,
    string? OcrFullText,
    IReadOnlyList<GamebookSegmentDto> Segments,
    string? FailureReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt);
