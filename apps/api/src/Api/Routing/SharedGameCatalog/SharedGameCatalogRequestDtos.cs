using Api.BoundedContexts.SharedGameCatalog.Application;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.SharedKernel.Domain.Covers;

namespace Api.Routing;

/// <summary>
/// Request body for the cover-picker upsert (epic #3470). The UI context is a route
/// segment; the body carries the pinned source and the crop focal point (defaults
/// to center). The acting admin comes from the session, never the body.
/// </summary>
internal record AssignCoverRequest(
    CoverAssignmentSource Source,
    double FocalX = 0.5,
    double FocalY = 0.5);

/// <summary>
/// Request body for the manual cover set (epic #3470 Slice 3a). The admin supplies an HTTPS image
/// URL plus the attested license (must be whitelisted) and optional attribution. The acting admin
/// comes from the session, never the body.
/// </summary>
internal record SetManualCoverRequest(
    string SourceUrl,
    string License,
    string? Attribution = null);

/// <summary>
/// Optional request body for the manual cover revoke (#3495 H6). Carries the takedown reason, which
/// is recorded in the <c>cover.manual.revoked</c> audit event. The body is optional so the existing
/// no-body DELETE contract keeps working (reason is then null).
/// </summary>
internal record RevokeManualCoverRequest(
    string? Reason = null);

// ========================================
// REQUEST DTOS
// ========================================

/// <summary>
/// Request body for <c>POST /mechanic-cards/{cardId}/feedback</c> (#533 ME-M3.1). The card id comes
/// from the route and the user id from the session, so the body only carries the claim + verdict.
/// </summary>
internal record SubmitMechanicCardFeedbackRequest(
    Guid ClaimId,
    bool IsPositive,
    string? ErrorType,
    string? Description,
    string? SuggestedCitation);

/// <summary>
/// Request DTO for creating a shared game.
/// </summary>
internal record CreateSharedGameRequest(
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
    int? BggId,
    List<string>? Categories = null,
    List<string>? Mechanics = null,
    List<string>? Designers = null,
    List<string>? Publishers = null);

/// <summary>
/// Request DTO for updating a shared game.
/// </summary>
internal record UpdateSharedGameRequest(
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
    GameRulesDto? Rules);

/// <summary>
/// Request DTO for importing a game from BoardGameGeek.
/// </summary>
internal record ImportFromBggRequest(int BggId);

/// <summary>
/// Request DTO for updating an existing game from BoardGameGeek data.
/// Issue: Admin Add Shared Game from BGG flow - "Propose Update" functionality
/// </summary>
/// <param name="BggId">BoardGameGeek game ID to fetch fresh data from</param>
/// <param name="FieldsToUpdate">List of field names to update. If null/empty, updates all fields.</param>
internal record UpdateFromBggRequest(int BggId, List<string>? FieldsToUpdate);

/// <summary>
/// Request DTO for bulk importing games.
/// </summary>
internal record BulkImportRequest(List<BulkGameImportDto> Games);

/// <summary>
/// Request DTO for deleting a game (Editor provides reason).
/// </summary>
internal record DeleteGameRequest(string? Reason);

/// <summary>
/// Request DTO for rejecting a delete request.
/// </summary>
internal record RejectDeleteRequest(string Reason);

/// <summary>
/// Request DTO for rejecting a publication approval.
/// Issue #2514: Approval workflow implementation
/// </summary>
internal record RejectPublicationRequest(string Reason);

/// <summary>
/// Request DTO for batch approving multiple games.
/// Issue #3350: Batch approval/rejection for games
/// </summary>
internal record BatchApproveGamesRequest(
    IReadOnlyList<Guid> GameIds,
    string? Note);

/// <summary>
/// Response DTO for batch approve operation.
/// Issue #3350: Batch approval/rejection for games
/// </summary>
internal record BatchApproveGamesResponse(
    int SuccessCount,
    int FailureCount,
    IReadOnlyList<string> Errors);

/// <summary>
/// Request DTO for batch rejecting multiple games.
/// Issue #3350: Batch approval/rejection for games
/// </summary>
internal record BatchRejectGamesRequest(
    IReadOnlyList<Guid> GameIds,
    string Reason);

/// <summary>
/// Response DTO for batch reject operation.
/// Issue #3350: Batch approval/rejection for games
/// </summary>
internal record BatchRejectGamesResponse(
    int SuccessCount,
    int FailureCount,
    IReadOnlyList<string> Errors);

/// <summary>
/// Request DTO for approving a document for RAG processing.
/// Issue #3533: Admin approval workflow
/// </summary>
internal record ApproveDocumentRequest(string? Notes);

/// <summary>
/// Request DTO for adding a quick question manually.
/// </summary>
internal record AddQuickQuestionRequest(string Text, string Emoji, QuestionCategory Category, int DisplayOrder);

/// <summary>
/// Request DTO for updating a quick question.
/// </summary>
internal record UpdateQuickQuestionRequest(string Text, string Emoji, QuestionCategory Category, int DisplayOrder);

/// <summary>
/// Request DTO for adding a FAQ.
/// </summary>
internal record AddFaqRequest(string Question, string Answer, int Order);

/// <summary>
/// Request DTO for updating a FAQ.
/// </summary>
internal record UpdateFaqRequest(string Question, string Answer, int Order);

/// <summary>
/// Request DTO for adding an errata.
/// </summary>
internal record AddErrataRequest(string Description, string PageReference, DateTime PublishedDate);

/// <summary>
/// Request DTO for updating an errata.
/// </summary>
internal record UpdateErrataRequest(string Description, string PageReference, DateTime PublishedDate);

/// <summary>
/// Request DTO for adding a document to a game.
/// </summary>
internal record AddDocumentRequest(
    Guid PdfDocumentId,
    SharedGameDocumentType DocumentType,
    string Version,
    List<string>? Tags,
    bool SetAsActive);

/// <summary>
/// Request DTO for generating a game state template using AI.
/// Issue #2400
/// </summary>
internal record GenerateStateTemplateRequest(
    string Name,
    bool SetAsActive = false);

/// <summary>
/// Request DTO for updating a game state template.
/// Issue #2400
/// </summary>
internal record UpdateStateTemplateRequest(
    string? Name,
    string SchemaJson,
    string Version);

/// <summary>
/// Request DTO for creating a share request.
/// Issue #2733
/// </summary>
internal record CreateShareRequestRequest(
    Guid SourceGameId,
    string? Notes,
    List<Guid>? AttachedDocumentIds);

/// <summary>
/// Request DTO for updating share request documents.
/// Issue #2733
/// </summary>
internal record UpdateShareRequestDocumentsRequest(
    List<Guid>? DocumentIds);

/// <summary>
/// Request DTO for approving a share request with optional modifications.
/// Issue #2734
/// </summary>
internal record ApproveShareRequestRequest(
    Guid? TargetSharedGameId,
    string? AdminNotes);

/// <summary>
/// Request DTO for rejecting a share request.
/// Issue #2734
/// </summary>
internal record RejectShareRequestRequest(
    string Reason);

/// <summary>
/// Request DTO for bulk approving share requests.
/// Issue #2893
/// </summary>
internal record BulkApproveShareRequestsRequest(
    IReadOnlyList<Guid> ShareRequestIds,
    Guid? TargetSharedGameId,
    string? AdminNotes);

/// <summary>
/// Request DTO for bulk rejecting share requests.
/// Issue #2893
/// </summary>
internal record BulkRejectShareRequestsRequest(
    IReadOnlyList<Guid> ShareRequestIds,
    string Reason);

/// <summary>
/// Request DTO for requesting changes to a share request.
/// Issue #2734
/// </summary>
internal record RequestShareRequestChangesRequest(
    string Feedback);

/// <summary>
/// Request DTO for toggling badge display visibility.
/// Issue #2736
/// </summary>
internal record ToggleBadgeDisplayRequest(
    bool IsDisplayed);

/// <summary>
/// Request DTO for approving a game proposal with enhanced actions.
/// Issue #3667: Phase 6 - Admin Review Enhancements.
/// </summary>
internal record ApproveGameProposalRequest(
    ProposalApprovalAction ApprovalAction,
    Guid? TargetSharedGameId,
    string? AdminNotes);

/// <summary>
/// Request DTO for recording a game analytics event.
/// Issue #3918: Catalog Trending Analytics Service
/// </summary>
internal record RecordGameEventRequest(
    Guid GameId,
    GameEventType EventType,
    Guid? UserId);

// Note: CreateGameFromPdfRequest is defined in
// Api.BoundedContexts.SharedGameCatalog.Application.DTOs.CreateGameFromPdfRequest
