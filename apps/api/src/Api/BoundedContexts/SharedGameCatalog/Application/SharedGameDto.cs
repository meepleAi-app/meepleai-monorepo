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
/// Issue #593 (Wave A.3a): Extended with aggregate counts and flags for V2 /shared-games mockup.
/// New fields are defaulted to preserve backwards compatibility with existing callers that
/// don't need aggregates (e.g. internal admin queries, legacy API consumers).
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
    DateTime? ModifiedAt,
    bool IsRagPublic = false,
    bool HasKnowledgeBase = false,
    int ToolkitsCount = 0,
    int AgentsCount = 0,
    int KbsCount = 0,
    int NewThisWeekCount = 0,
    int ContributorsCount = 0,
    bool IsTopRated = false,
    bool IsNew = false);

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
/// Preview DTO for a published toolkit linked to a SharedGame via Game intermediate.
/// Issue #603 (Wave A.4): nested in SharedGameDetailDto for /shared-games/[id] V2 mockup.
/// Excludes default toolkits (Toolkit.IsDefault == true) per BR-02 Issue #5144.
/// Shape mirrors what `Toolkit` aggregate exposes today; mockup fields without
/// entity backing (Version, DownloadCount, rating) are deferred to future waves
/// once the underlying domain model adopts them — frontend renders sensible
/// defaults in the meantime (mirrors A.3a's ContributorsCount approximation).
/// </summary>
public sealed record PublishedToolkitPreviewDto(
    Guid Id,
    string Name,
    Guid OwnerId,
    string OwnerName,
    DateTime LastUpdatedAt);

/// <summary>
/// Preview DTO for a published agent definition linked to a SharedGame via Game intermediate.
/// Issue #603 (Wave A.4): nested in SharedGameDetailDto for /shared-games/[id] V2 mockup.
/// `InvocationCount` is the real popularity proxy from `AgentDefinition.InvocationCount`
/// (sourced from runtime telemetry). Owner/rating fields are deferred — `AgentDefinition`
/// has no `CreatedBy` column today, and rating system isn't implemented.
/// </summary>
public sealed record PublishedAgentPreviewDto(
    Guid Id,
    string Name,
    int InvocationCount,
    DateTime LastUpdatedAt);

/// <summary>
/// Preview DTO for an indexed knowledge-base document linked directly to a SharedGame.
/// Issue #603 (Wave A.4): nested in SharedGameDetailDto for /shared-games/[id] V2 mockup.
/// VectorDocument has direct SharedGameId FK (Issue #5185), no Game intermediate join.
/// `TotalChunks` is exposed as a coarse "size" indicator until PdfDocument metadata
/// (filename/title/page count) is wired through to KB queries.
/// </summary>
public sealed record PublishedKbPreviewDto(
    Guid Id,
    string Language,
    int TotalChunks,
    DateTime IndexedAt);

/// <summary>
/// Data transfer object for detailed shared game information.
/// Issue #2373 Phase 4: Extended with FAQs, Errata, Designers, Publishers, Categories, Mechanics.
/// Issue #603 (Wave A.4): Extended with published toolkits/agents/KB previews + aggregate
/// counts/flags for V2 /shared-games/[id] mockup. New fields are defaulted to preserve
/// backwards compatibility with existing callers (admin endpoints, legacy consumers).
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
    IReadOnlyList<GameMechanicSimpleDto> Mechanics,
    // === A.4 extension (defaulted) ===
    IReadOnlyList<PublishedToolkitPreviewDto>? Toolkits = null,
    IReadOnlyList<PublishedAgentPreviewDto>? Agents = null,
    IReadOnlyList<PublishedKbPreviewDto>? Kbs = null,
    int ToolkitsCount = 0,
    int AgentsCount = 0,
    int KbsCount = 0,
    int ContributorsCount = 0,
    bool HasKnowledgeBase = false,
    bool IsTopRated = false,
    bool IsNew = false);

/// <summary>
/// Data transfer object for approval queue items.
/// Provides game info and approval metadata for admin review.
/// Issue #3533: Admin API Endpoints - Approval Queue Management
/// Issue #4199: Extended with user display name and email
/// </summary>
public sealed record ApprovalQueueItemDto(
    Guid GameId,
    string Title,
    Guid SubmittedBy,
    string SubmittedByName,
    string SubmittedByEmail,
    DateTime SubmittedAt,
    int DaysPending,
    int PdfCount);