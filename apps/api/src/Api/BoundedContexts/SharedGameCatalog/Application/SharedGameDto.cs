using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;

namespace Api.BoundedContexts.SharedGameCatalog.Application;

/// <summary>
/// Data transfer object for delete request information.
/// </summary>
internal sealed record DeleteRequestDto(
    Guid Id,
    Guid SharedGameId,
    string GameTitle,
    Guid RequestedBy,
    string Reason,
    DateTime CreatedAt);

/// <summary>
/// Data transfer object for shared game basic information.
/// </summary>
public sealed record SharedGameDto(
    Guid Id,
    int? BggId,
    string Title,
    int YearPublished,
    string Description,
    int MinPlayers,
    int MaxPlayers,
    int PlayingTimeMinutes,
    int MinAge,
    decimal? ComplexityRating,
    decimal? AverageRating,
    string ImageUrl,
    string ThumbnailUrl,
    GameStatus Status,
    DateTime CreatedAt,
    DateTime? ModifiedAt);

/// <summary>
/// Data transfer object for game rules.
/// </summary>
public sealed record GameRulesDto(
    string Content,
    string Language);

/// <summary>
/// Data transfer object for game FAQ.
/// Issue #2681: Added GameId, Upvotes, UpdatedAt for public API
/// </summary>
public sealed record GameFaqDto(
    Guid Id,
    Guid GameId,
    string Question,
    string Answer,
    int Order,
    int Upvotes,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>
/// Response DTO for paginated FAQ results.
/// Issue #2681: Public FAQs endpoints
/// </summary>
public sealed record GetGameFaqsResultDto(
    IReadOnlyCollection<GameFaqDto> Faqs,
    int TotalCount);

/// <summary>
/// Response DTO for upvote operation.
/// Issue #2681: Public FAQs endpoints
/// </summary>
public sealed record UpvoteFaqResultDto(
    Guid Id,
    int UpvoteCount);

/// <summary>
/// Data transfer object for game errata.
/// </summary>
public sealed record GameErrataDto(
    Guid Id,
    string Description,
    string PageReference,
    DateTime PublishedDate,
    DateTime CreatedAt);

/// <summary>
/// Data transfer object for game designer.
/// </summary>
public sealed record GameDesignerDto(
    Guid Id,
    string Name);

/// <summary>
/// Data transfer object for game publisher.
/// </summary>
public sealed record GamePublisherDto(
    Guid Id,
    string Name);

/// <summary>
/// Data transfer object for game category (simple).
/// </summary>
public sealed record GameCategorySimpleDto(
    Guid Id,
    string Name,
    string Slug);

/// <summary>
/// Data transfer object for game mechanic (simple).
/// </summary>
public sealed record GameMechanicSimpleDto(
    Guid Id,
    string Name,
    string Slug);

/// <summary>
/// Data transfer object for detailed shared game information.
/// Issue #2373 Phase 4: Extended with FAQs, Errata, Designers, Publishers, Categories, Mechanics.
/// </summary>
public sealed record SharedGameDetailDto(
    Guid Id,
    int? BggId,
    string Title,
    int YearPublished,
    string Description,
    int MinPlayers,
    int MaxPlayers,
    int PlayingTimeMinutes,
    int MinAge,
    decimal? ComplexityRating,
    decimal? AverageRating,
    string ImageUrl,
    string ThumbnailUrl,
    GameRulesDto? Rules,
    GameStatus Status,
    Guid CreatedBy,
    Guid? ModifiedBy,
    DateTime CreatedAt,
    DateTime? ModifiedAt,
    IReadOnlyList<GameFaqDto> Faqs,
    IReadOnlyList<GameErrataDto> Erratas,
    IReadOnlyList<GameDesignerDto> Designers,
    IReadOnlyList<GamePublisherDto> Publishers,
    IReadOnlyList<GameCategorySimpleDto> Categories,
    IReadOnlyList<GameMechanicSimpleDto> Mechanics);

/// <summary>
/// Data transfer object for approval queue items.
/// Provides game info and approval metadata for admin review.
/// Issue #3533: Admin API Endpoints - Approval Queue Management
/// </summary>
public sealed record ApprovalQueueItemDto(
    Guid GameId,
    string Title,
    Guid SubmittedBy,
    DateTime SubmittedAt,
    int DaysPending,
    int PdfCount);