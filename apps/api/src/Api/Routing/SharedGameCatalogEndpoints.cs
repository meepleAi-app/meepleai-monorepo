using Api.BoundedContexts.SharedGameCatalog.Application;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands.RecordGameEvent;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetCatalogTrending;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetGameContributors;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetPendingShareRequests;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetShareRequestDetails;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetUserContributions;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetUserContributionStats;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetUserShareRequests;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetAllBadges;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetUserBadges;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetBadgeLeaderboard;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetMyActiveReviews;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.CheckPrivateGameDuplicates;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands.ToggleBadgeDisplay;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands.ApproveGameProposal;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Exceptions;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Extensions;
using Api.Middleware.Exceptions;
using Api.Models;
using Api.BoundedContexts.Administration.Application.Commands;
using Api.SharedKernel.Domain.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// SharedGameCatalog endpoints for public game search and admin management.
/// Issue #2371 Phase 2
/// </summary>
internal static class SharedGameCatalogEndpoints
{
    public static RouteGroupBuilder MapSharedGameCatalogEndpoints(this RouteGroupBuilder group)
    {
        MapPublicEndpoints(group);
        MapAdminEndpoints(group);
        MapAdminShareRequestEndpoints(group);
        MapUserShareRequestEndpoints(group);
        MapContributorEndpoints(group);
        MapBadgeEndpoints(group);
        MapTrendingEndpoints(group);
        MapWizardEndpoints(group); // Issue #4139: PDF Wizard endpoints

        return group;
    }

    // ========================================
    // PUBLIC ENDPOINTS (Unauthenticated)
    // ========================================

    private static void MapPublicEndpoints(RouteGroupBuilder group)
    {
        // Search shared games with filtering and full-text search
        group.MapGet("/shared-games", HandleSearchGames)
            .AllowAnonymous()
            .RequireRateLimiting("SharedGamesPublic")
            .WithName("SearchSharedGames")
            .WithSummary("Search shared games catalog")
            .WithDescription("Search games with full-text search, category/mechanic filters, player count, and playing time filters. Returns published games only for public access.")
            .Produces<PagedResult<SharedGameDto>>();

        // Get game details by ID
        group.MapGet("/shared-games/{id:guid}", HandleGetGameById)
            .AllowAnonymous()
            .RequireRateLimiting("SharedGamesPublic")
            .WithName("GetSharedGameById")
            .WithSummary("Get shared game details")
            .WithDescription("Get detailed information about a shared game including designers, publishers, categories, mechanics, FAQs, and errata. Returns only published games for public access.")
            .Produces<SharedGameDetailDto>()
            .Produces(StatusCodes.Status404NotFound);

        // Get all game categories
        group.MapGet("/shared-games/categories", HandleGetCategories)
            .AllowAnonymous()
            .RequireRateLimiting("SharedGamesPublic")
            .WithName("GetGameCategories")
            .WithSummary("Get all game categories")
            .WithDescription("Returns all available game categories for filtering.")
            .Produces<List<GameCategoryDto>>();

        // Get all game mechanics
        group.MapGet("/shared-games/mechanics", HandleGetMechanics)
            .AllowAnonymous()
            .RequireRateLimiting("SharedGamesPublic")
            .WithName("GetGameMechanics")
            .WithSummary("Get all game mechanics")
            .WithDescription("Returns all available game mechanics for filtering.")
            .Produces<List<GameMechanicDto>>();

        // Get FAQs for a game with pagination - Issue #2681
        group.MapGet("/games/{gameId:guid}/faqs", HandleGetGameFaqs)
            .AllowAnonymous()
            .RequireRateLimiting("SharedGamesPublic")
            .WithName("GetGameFaqs")
            .WithSummary("Get FAQs for a game")
            .WithDescription("Returns FAQs for a published game with pagination. Ordered by display order then by upvote count (descending).")
            .Produces<GetGameFaqsResultDto>()
            .Produces(StatusCodes.Status404NotFound);

        // Upvote a FAQ - Issue #2681
        group.MapPost("/faqs/{faqId:guid}/upvote", HandleUpvoteFaq)
            .AllowAnonymous()
            .RequireRateLimiting("FaqUpvote")
            .WithName("UpvoteFaq")
            .WithSummary("Upvote a FAQ")
            .WithDescription("Increments the upvote count for a FAQ. Rate limited to prevent abuse.")
            .Produces<UpvoteFaqResultDto>()
            .Produces(StatusCodes.Status404NotFound);
    }

    // ========================================
    // ADMIN/EDITOR ENDPOINTS (Protected)
    // ========================================

#pragma warning disable MA0051 // Method is too long - endpoint registration methods are inherently long
    private static void MapAdminEndpoints(RouteGroupBuilder group)
    {
        // Get all shared games for admin management (Issue #2773)
        group.MapGet("/admin/shared-games", HandleListAllGames)
            .RequireAuthorization("AdminOrEditorPolicy")
            .RequireRateLimiting("SharedGamesAdmin")
            .WithName("ListAllSharedGames")
            .WithSummary("Get all shared games (Admin/Editor)")
            .WithDescription("Returns all shared games with optional status filter for admin management.")
            .Produces<PagedResult<SharedGameDto>>();

        // Create new shared game
        group.MapPost("/admin/shared-games", HandleCreateGame)
            .RequireAuthorization("AdminOrEditorPolicy")
            .RequireRateLimiting("SharedGamesAdmin")
            .WithName("CreateSharedGame")
            .WithSummary("Create new shared game (Admin/Editor)")
            .Produces<Guid>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        // Update existing shared game
        group.MapPut("/admin/shared-games/{id:guid}", HandleUpdateGame)
            .RequireAuthorization("AdminOrEditorPolicy")
            .RequireRateLimiting("SharedGamesAdmin")
            .WithName("UpdateSharedGame")
            .WithSummary("Update shared game (Admin/Editor)")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        // Submit game for approval (Draft → PendingApproval) - Issue #2514
        group.MapPost("/admin/shared-games/{id:guid}/submit-for-approval", HandleSubmitForApproval)
            .RequireAuthorization("AdminOrEditorPolicy")
            .WithName("SubmitSharedGameForApproval")
            .WithSummary("Submit shared game for approval (Admin/Editor)")
            .WithDescription("Submits a draft game for admin approval. Transitions from Draft to PendingApproval status.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        // Approve publication (PendingApproval → Published) - Issue #2514
        group.MapPost("/admin/shared-games/{id:guid}/approve-publication", HandleApprovePublication)
            .RequireAuthorization("AdminOnlyPolicy")
            .WithName("ApproveSharedGamePublication")
            .WithSummary("Approve game publication (Admin only)")
            .WithDescription("Approves a pending game for publication. Transitions from PendingApproval to Published status.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        // Reject publication (PendingApproval → Draft) - Issue #2514
        group.MapPost("/admin/shared-games/{id:guid}/reject-publication", HandleRejectPublication)
            .RequireAuthorization("AdminOnlyPolicy")
            .WithName("RejectSharedGamePublication")
            .WithSummary("Reject game publication (Admin only)")
            .WithDescription("Rejects a pending game and sends it back to Draft status with a reason.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        // Batch approve publications - Issue #3350
        group.MapPost("/admin/shared-games/batch-approve", HandleBatchApprovePublications)
            .RequireAuthorization("AdminOnlyPolicy")
            .WithName("BatchApproveSharedGames")
            .WithSummary("Batch approve multiple games (Admin only)")
            .WithDescription("Approves multiple games for publication in a single transaction. Returns success/failure counts.")
            .Produces<BatchApproveGamesResponse>()
            .Produces(StatusCodes.Status400BadRequest);

        // Batch reject publications - Issue #3350
        group.MapPost("/admin/shared-games/batch-reject", HandleBatchRejectPublications)
            .RequireAuthorization("AdminOnlyPolicy")
            .WithName("BatchRejectSharedGames")
            .WithSummary("Batch reject multiple games (Admin only)")
            .WithDescription("Rejects multiple games in a single transaction with a common reason. Returns success/failure counts.")
            .Produces<BatchRejectGamesResponse>()
            .Produces(StatusCodes.Status400BadRequest);

        // Get pending approval games - Issue #2514
        group.MapGet("/admin/shared-games/pending-approvals", HandleGetPendingApprovals)
            .RequireAuthorization("AdminOnlyPolicy")
            .WithName("GetPendingApprovalGames")
            .WithSummary("Get games pending approval (Admin only)")
            .WithDescription("Returns all games in PendingApproval status awaiting admin review.")
            .Produces<PagedResult<SharedGameDto>>();

        // Get approval queue with filters - Issue #3533
        group.MapGet("/admin/shared-games/approval-queue", HandleGetApprovalQueue)
            .RequireAuthorization("AdminOnlyPolicy")
            .WithName("GetApprovalQueue")
            .WithSummary("Get filtered approval queue (Admin only)")
            .WithDescription("Returns games pending approval with urgency, submitter, and PDF filters.")
            .Produces<IReadOnlyList<ApprovalQueueItemDto>>();

        // Approve document for RAG processing - Issue #3533
        group.MapPost("/admin/shared-games/{id:guid}/documents/{docId:guid}/approve", HandleApproveDocument)
            .RequireAuthorization("AdminOnlyPolicy")
            .WithName("ApproveDocumentForRag")
            .WithSummary("Approve document for RAG processing (Admin only)")
            .WithDescription("Approves a document and triggers RAG embedding workflow.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        // Archive game (Published → Archived)
        group.MapPost("/admin/shared-games/{id:guid}/archive", HandleArchiveGame)
            .RequireAuthorization("AdminOnlyPolicy")
            .WithName("ArchiveSharedGame")
            .WithSummary("Archive shared game (Admin only)")
            .Produces(StatusCodes.Status204NoContent);

        // Import game from BoardGameGeek
        group.MapPost("/admin/shared-games/import-bgg", HandleImportFromBgg)
            .RequireAuthorization("AdminOrEditorPolicy")
            .WithName("ImportGameFromBgg")
            .WithSummary("Import game from BoardGameGeek (Admin/Editor)")
            .Produces<Guid>(StatusCodes.Status201Created);

        // BGG Search - searches BoardGameGeek for games (autocomplete)
        group.MapGet("/admin/shared-games/bgg/search", HandleBggSearch)
            .RequireAuthorization("AdminOrEditorPolicy")
            .RequireRateLimiting("SharedGamesAdmin")
            .WithName("SearchBggGames")
            .WithSummary("Search BoardGameGeek for games (Admin/Editor)")
            .WithDescription("Search BGG API for board games by name. Used for autocomplete in add-from-BGG flow.")
            .Produces<List<BggSearchResultDto>>();

        // BGG Duplicate Check - check if game exists and return diff data
        group.MapGet("/admin/shared-games/bgg/check-duplicate/{bggId:int}", HandleCheckBggDuplicate)
            .RequireAuthorization("AdminOrEditorPolicy")
            .RequireRateLimiting("SharedGamesAdmin")
            .WithName("CheckBggDuplicate")
            .WithSummary("Check if BGG game already exists (Admin/Editor)")
            .WithDescription("Checks if a game with given BGG ID exists. Returns both existing game data and fresh BGG data for diff comparison.")
            .Produces<BggDuplicateCheckResult>();

        // Update existing game from BGG with selective field updates
        group.MapPut("/admin/shared-games/{id:guid}/update-from-bgg", HandleUpdateFromBgg)
            .RequireAuthorization("AdminOrEditorPolicy")
            .RequireRateLimiting("SharedGamesAdmin")
            .WithName("UpdateGameFromBgg")
            .WithSummary("Update existing game from BGG data (Admin/Editor)")
            .WithDescription("Updates an existing game with fresh data from BGG. Supports selective field updates via fieldsToUpdate parameter.")
            .Produces<Guid>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        // Bulk import games
        group.MapPost("/admin/shared-games/bulk-import", HandleBulkImport)
            .RequireAuthorization("AdminOnlyPolicy")
            .WithName("BulkImportGames")
            .WithSummary("Bulk import games (Admin only)")
            .Produces<BulkImportResultDto>();

        // Delete game (Admin direct delete, Editor creates request)
        group.MapDelete("/admin/shared-games/{id:guid}", HandleDeleteGame)
            .RequireAuthorization("AdminOrEditorPolicy")
            .WithName("DeleteSharedGame")
            .WithSummary("Delete shared game (Admin/Editor)")
            .WithDescription("Admin: deletes directly. Editor: creates delete request for admin approval.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<Guid>(StatusCodes.Status202Accepted); // Editor case returns requestId

        // Get pending delete requests (Admin only)
        group.MapGet("/admin/shared-games/pending-deletes", HandleGetPendingDeletes)
            .RequireAuthorization("AdminOnlyPolicy")
            .WithName("GetPendingDeleteRequests")
            .WithSummary("Get pending delete requests (Admin only)")
            .Produces<PagedResult<DeleteRequestDto>>();

        // Approve delete request (Admin only)
        group.MapPost("/admin/shared-games/approve-delete/{requestId:guid}", HandleApproveDelete)
            .RequireAuthorization("AdminOnlyPolicy")
            .WithName("ApproveDeleteRequest")
            .WithSummary("Approve delete request (Admin only)")
            .Produces(StatusCodes.Status204NoContent);

        // Reject delete request (Admin only)
        group.MapPost("/admin/shared-games/reject-delete/{requestId:guid}", HandleRejectDelete)
            .RequireAuthorization("AdminOnlyPolicy")
            .WithName("RejectDeleteRequest")
            .WithSummary("Reject delete request (Admin only)")
            .Produces(StatusCodes.Status204NoContent);

        // Quick Questions Management
        group.MapGet("/games/{id:guid}/quick-questions", HandleGetQuickQuestions)
            .WithName("GetQuickQuestions")
            .WithSummary("Get quick questions for game")
            .Produces<IReadOnlyCollection<QuickQuestionDto>>(StatusCodes.Status200OK);

        group.MapPost("/admin/shared-games/{id:guid}/quick-questions/generate", HandleGenerateQuickQuestions)
            .RequireAuthorization("AdminOrEditorPolicy")
            .WithName("GenerateQuickQuestions")
            .WithSummary("Generate quick questions using AI (Admin/Editor)")
            .Produces<GenerateQuickQuestionsResultDto>(StatusCodes.Status201Created);

        group.MapPost("/admin/shared-games/{id:guid}/quick-questions", HandleAddManualQuickQuestion)
            .RequireAuthorization("AdminOrEditorPolicy")
            .WithName("AddManualQuickQuestion")
            .WithSummary("Manually add quick question (Admin/Editor)")
            .Produces<Guid>(StatusCodes.Status201Created);

        group.MapPut("/admin/quick-questions/{questionId:guid}", HandleUpdateQuickQuestion)
            .RequireAuthorization("AdminOrEditorPolicy")
            .WithName("UpdateQuickQuestion")
            .WithSummary("Update quick question (Admin/Editor)")
            .Produces<QuickQuestionDto>(StatusCodes.Status200OK);

        group.MapDelete("/admin/quick-questions/{questionId:guid}", HandleDeleteQuickQuestion)
            .RequireAuthorization("AdminOrEditorPolicy")
            .WithName("DeleteQuickQuestion")
            .WithSummary("Delete quick question (Admin/Editor)")
            .Produces(StatusCodes.Status204NoContent);

        // FAQ Management
        group.MapPost("/admin/shared-games/{id:guid}/faq", HandleAddFaq)
            .RequireAuthorization("AdminOrEditorPolicy")
            .WithName("AddGameFaq")
            .WithSummary("Add FAQ to game (Admin/Editor)")
            .Produces<Guid>(StatusCodes.Status201Created);

        group.MapPut("/admin/shared-games/{id:guid}/faq/{faqId:guid}", HandleUpdateFaq)
            .RequireAuthorization("AdminOrEditorPolicy")
            .WithName("UpdateGameFaq")
            .WithSummary("Update FAQ (Admin/Editor)")
            .Produces(StatusCodes.Status204NoContent);

        group.MapDelete("/admin/shared-games/{id:guid}/faq/{faqId:guid}", HandleDeleteFaq)
            .RequireAuthorization("AdminOrEditorPolicy")
            .WithName("DeleteGameFaq")
            .WithSummary("Delete FAQ (Admin/Editor)")
            .Produces(StatusCodes.Status204NoContent);

        // Errata Management
        group.MapPost("/admin/shared-games/{id:guid}/errata", HandleAddErrata)
            .RequireAuthorization("AdminOrEditorPolicy")
            .WithName("AddGameErrata")
            .WithSummary("Add errata to game (Admin/Editor)")
            .Produces<Guid>(StatusCodes.Status201Created);

        group.MapPut("/admin/shared-games/{id:guid}/errata/{errataId:guid}", HandleUpdateErrata)
            .RequireAuthorization("AdminOrEditorPolicy")
            .WithName("UpdateGameErrata")
            .WithSummary("Update errata (Admin/Editor)")
            .Produces(StatusCodes.Status204NoContent);

        group.MapDelete("/admin/shared-games/{id:guid}/errata/{errataId:guid}", HandleDeleteErrata)
            .RequireAuthorization("AdminOrEditorPolicy")
            .WithName("DeleteGameErrata")
            .WithSummary("Delete errata (Admin/Editor)")
            .Produces(StatusCodes.Status204NoContent);

        // Document Management (Issue #2391 Sprint 1)
        group.MapGet("/admin/shared-games/{id:guid}/documents", HandleGetDocuments)
            .RequireAuthorization("AdminOrEditorPolicy")
            .WithName("GetGameDocuments")
            .WithSummary("Get all documents for a game (Admin/Editor)")
            .Produces<List<SharedGameDocumentDto>>();

        group.MapGet("/admin/shared-games/{id:guid}/documents/active", HandleGetActiveDocuments)
            .RequireAuthorization("AdminOrEditorPolicy")
            .WithName("GetActiveGameDocuments")
            .WithSummary("Get active documents for a game (Admin/Editor)")
            .Produces<List<SharedGameDocumentDto>>();

        group.MapPost("/admin/shared-games/{id:guid}/documents", HandleAddDocument)
            .RequireAuthorization("AdminOrEditorPolicy")
            .WithName("AddGameDocument")
            .WithSummary("Add document to game (Admin/Editor)")
            .Produces<Guid>(StatusCodes.Status201Created);

        group.MapPost("/admin/shared-games/{id:guid}/documents/{documentId:guid}/set-active", HandleSetActiveDocument)
            .RequireAuthorization("AdminOrEditorPolicy")
            .WithName("SetActiveGameDocument")
            .WithSummary("Set document version as active (Admin/Editor)")
            .Produces(StatusCodes.Status204NoContent);

        group.MapDelete("/admin/shared-games/{id:guid}/documents/{documentId:guid}", HandleRemoveDocument)
            .RequireAuthorization("AdminOrEditorPolicy")
            .WithName("RemoveGameDocument")
            .WithSummary("Remove document from game (Admin/Editor)")
            .Produces(StatusCodes.Status204NoContent);

        // Agent Linking (Issue #4228)
        group.MapPost("/admin/shared-games/{id:guid}/link-agent/{agentId:guid}", HandleLinkAgent)
            .RequireAuthorization("AdminOrEditorPolicy")
            .WithName("LinkAgentToSharedGame")
            .WithSummary("Link AI agent to shared game (Admin/Editor)")
            .WithDescription("Links an AI agent definition to a shared game for personalized assistance.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        group.MapDelete("/admin/shared-games/{id:guid}/unlink-agent", HandleUnlinkAgent)
            .RequireAuthorization("AdminOrEditorPolicy")
            .WithName("UnlinkAgentFromSharedGame")
            .WithSummary("Unlink AI agent from shared game (Admin/Editor)")
            .WithDescription("Removes the AI agent link from a shared game.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        // Game State Template Management (Issue #2400)
        group.MapGet("/admin/shared-games/{id:guid}/state-template", HandleGetActiveStateTemplate)
            .RequireAuthorization("AdminOrEditorPolicy")
            .WithName("GetActiveGameStateTemplate")
            .WithSummary("Get active game state template (Admin/Editor)")
            .WithDescription("Returns the currently active game state template for tracking game state. Returns null if no active template exists.")
            .Produces<GameStateTemplateDto>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/admin/shared-games/{id:guid}/state-template/versions", HandleGetStateTemplateVersions)
            .RequireAuthorization("AdminOrEditorPolicy")
            .WithName("GetGameStateTemplateVersions")
            .WithSummary("Get all game state template versions (Admin/Editor)")
            .WithDescription("Returns all versions of game state templates for a game, ordered by version descending.")
            .Produces<IReadOnlyList<GameStateTemplateDto>>();

        group.MapPost("/admin/shared-games/{id:guid}/state-template/generate", HandleGenerateStateTemplate)
            .RequireAuthorization("AdminOrEditorPolicy")
            .WithName("GenerateGameStateTemplate")
            .WithSummary("Generate game state template using AI (Admin/Editor)")
            .WithDescription("Uses AI to analyze the game's rulebook and generate a JSON Schema for tracking game state. Requires an active rulebook document.")
            .Produces<GameStateTemplateDto>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapPut("/admin/shared-games/{id:guid}/state-template/{templateId:guid}", HandleUpdateStateTemplate)
            .RequireAuthorization("AdminOrEditorPolicy")
            .WithName("UpdateGameStateTemplate")
            .WithSummary("Update game state template (Admin/Editor)")
            .WithDescription("Updates the JSON Schema of an existing template. Creates a new version.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/admin/shared-games/{id:guid}/state-template/{templateId:guid}/activate", HandleActivateStateTemplate)
            .RequireAuthorization("AdminOrEditorPolicy")
            .WithName("ActivateGameStateTemplate")
            .WithSummary("Activate game state template version (Admin/Editor)")
            .WithDescription("Sets the specified template version as active. Deactivates all other versions for this game.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);
    }

    // ========================================
    // ADMIN SHARE REQUEST ENDPOINTS (Admin only)
    // Issue #2734
    // ========================================

    private static void MapAdminShareRequestEndpoints(RouteGroupBuilder group)
    {
        // List pending share requests for admin dashboard
        group.MapGet("/admin/share-requests", HandleGetPendingShareRequests)
            .RequireAuthorization("AdminOnlyPolicy")
            .RequireRateLimiting("ShareRequestAdmin")
            .WithName("GetPendingShareRequests")
            .WithSummary("List pending share requests (Admin only)")
            .WithDescription("Returns paginated share requests for admin review with filtering by status and contribution type. Supports full-text search on game title and user notes.")
            .Produces<PagedResult<AdminShareRequestDto>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        // Get share request details for admin review
        group.MapGet("/admin/share-requests/{id:guid}", HandleGetShareRequestForReview)
            .RequireAuthorization("AdminOnlyPolicy")
            .RequireRateLimiting("ShareRequestAdmin")
            .WithName("GetShareRequestForReview")
            .WithSummary("Get share request details for review (Admin only)")
            .WithDescription("Returns detailed information about a share request including game data, contributor profile, attached documents, review history, and lock status.")
            .Produces<ShareRequestDetailsDto>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        // Approve share request
        group.MapPost("/admin/share-requests/{id:guid}/approve", HandleApproveShareRequest)
            .RequireAuthorization("AdminOnlyPolicy")
            .RequireRateLimiting("ShareRequestAdmin")
            .WithName("ApproveShareRequest")
            .WithSummary("Approve share request (Admin only)")
            .WithDescription("Approves a share request and publishes the game to the shared catalog. Admin must have active review lock on the request. Optionally allows title/description modifications and document selection.")
            .Produces<ApproveShareRequestResponse>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        // Reject share request
        group.MapPost("/admin/share-requests/{id:guid}/reject", HandleRejectShareRequest)
            .RequireAuthorization("AdminOnlyPolicy")
            .RequireRateLimiting("ShareRequestAdmin")
            .WithName("RejectShareRequest")
            .WithSummary("Reject share request (Admin only)")
            .WithDescription("Rejects a share request with a required reason. Admin must have active review lock on the request. Notifies the user via email.")
            .Produces<RejectShareRequestResponse>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        // Request changes to share request
        group.MapPost("/admin/share-requests/{id:guid}/request-changes", HandleRequestShareRequestChanges)
            .RequireAuthorization("AdminOnlyPolicy")
            .RequireRateLimiting("ShareRequestAdmin")
            .WithName("RequestShareRequestChanges")
            .WithSummary("Request changes to share request (Admin only)")
            .WithDescription("Requests changes to a share request with detailed feedback. Admin must have active review lock on the request. Transitions request to ChangesRequested status and notifies the user.")
            .Produces<RequestShareRequestChangesResponse>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        // Start review - acquire lock
        group.MapPost("/admin/share-requests/{id:guid}/start-review", HandleStartReview)
            .RequireAuthorization("AdminOnlyPolicy")
            .RequireRateLimiting("ShareRequestAdmin")
            .WithName("StartReview")
            .WithSummary("Start review on share request (Admin only)")
            .WithDescription("Acquires an exclusive review lock for the admin. Only one admin can review a request at a time. Lock duration is configurable via SystemConfiguration key 'ReviewLock:DefaultDurationMinutes' (default: 30 minutes).")
            .Produces<StartReviewResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        // Release review - free lock without decision
        group.MapPost("/admin/share-requests/{id:guid}/release", HandleReleaseReview)
            .RequireAuthorization("AdminOnlyPolicy")
            .RequireRateLimiting("ShareRequestAdmin")
            .WithName("ReleaseReview")
            .WithSummary("Release review lock (Admin only)")
            .WithDescription("Manually releases the review lock without making a decision. Returns the request to its previous state (Pending or ChangesRequested). Only the reviewing admin can release their own lock.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        // Get my active reviews
        group.MapGet("/admin/share-requests/my-reviews", HandleGetMyActiveReviews)
            .RequireAuthorization("AdminOnlyPolicy")
            .RequireRateLimiting("ShareRequestAdmin")
            .WithName("GetMyActiveReviews")
            .WithSummary("Get my active reviews (Admin only)")
            .WithDescription("Returns all share requests currently being reviewed by the authenticated admin with lock status and time remaining.")
            .Produces<IReadOnlyCollection<ActiveReviewDto>>()
            .Produces(StatusCodes.Status401Unauthorized);

        // Issue #2893: Bulk approve share requests
        group.MapPost("/editor/share-requests/bulk-approve", HandleBulkApproveShareRequests)
            .RequireAuthorization("EditorOnlyPolicy")
            .RequireRateLimiting("ShareRequestAdmin")
            .WithName("BulkApproveShareRequests")
            .WithSummary("Bulk approve share requests (Editor only)")
            .WithDescription("Approves multiple share requests in a single all-or-nothing transaction. Max 20 per batch. If any approval fails, entire batch rolls back.")
            .Produces<BulkOperationResult>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        // Issue #2893: Bulk reject share requests
        group.MapPost("/editor/share-requests/bulk-reject", HandleBulkRejectShareRequests)
            .RequireAuthorization("EditorOnlyPolicy")
            .RequireRateLimiting("ShareRequestAdmin")
            .WithName("BulkRejectShareRequests")
            .WithSummary("Bulk reject share requests (Editor only)")
            .WithDescription("Rejects multiple share requests in a single all-or-nothing transaction with shared reason. Max 20 per batch. If any rejection fails, entire batch rolls back.")
            .Produces<BulkOperationResult>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        // Issue #3667: Approve game proposal with enhanced actions
        group.MapPost("/admin/share-requests/{id:guid}/approve-game-proposal", HandleApproveGameProposal)
            .RequireAuthorization("AdminOnlyPolicy")
            .RequireRateLimiting("ShareRequestAdmin")
            .WithName("ApproveGameProposal")
            .WithSummary("Approve game proposal with enhanced actions (Admin only)")
            .WithDescription("Approves a NewGameProposal share request with three possible actions: ApproveAsNew (create new game), MergeKnowledgeBase (add PDFs to existing game), or ApproveAsVariant (create variant game). Admin must have active review lock on the request.")
            .Produces<ApproveShareRequestResponse>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        // Issue #3667: Check private game for duplicates
        group.MapGet("/admin/private-games/{id:guid}/check-duplicates", HandleCheckPrivateGameDuplicates)
            .RequireAuthorization("AdminOnlyPolicy")
            .RequireRateLimiting("ShareRequestAdmin")
            .WithName("CheckPrivateGameDuplicates")
            .WithSummary("Check private game for duplicates (Admin only)")
            .WithDescription("Checks if a private game has duplicates in the shared catalog using exact matching (BggId) and fuzzy matching (title similarity). Returns recommended approval action.")
            .Produces<DuplicateCheckResultDto>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
    }

    // ========================================
    // PUBLIC HANDLERS
    // ========================================

    private static async Task<IResult> HandleSearchGames(
        IMediator mediator,
        [FromQuery] string? search,
        [FromQuery] Guid[]? categoryIds,
        [FromQuery] Guid[]? mechanicIds,
        [FromQuery] int? minPlayers,
        [FromQuery] int? maxPlayers,
        [FromQuery] int? maxPlayingTime,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string sortBy = "Title",
        [FromQuery] bool sortDescending = false,
        CancellationToken ct = default)
    {
        var query = new SearchSharedGamesQuery(
            search,
            categoryIds?.ToList(),
            mechanicIds?.ToList(),
            minPlayers,
            maxPlayers,
            maxPlayingTime,
            Status: null, // Public always gets Published only (filtered in handler)
            pageNumber,
            pageSize,
            sortBy,
            sortDescending);

        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetGameById(
        IMediator mediator,
        HttpContext context,
        Guid id,
        CancellationToken ct)
    {
        var query = new GetSharedGameByIdQuery(id);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);

        // Public access: only return Published games
        // Admin/Editor can see all statuses
        if (result is not null)
        {
            var isAdminOrEditor = context.User.IsInRole("Admin") || context.User.IsInRole("Editor");
            if (!isAdminOrEditor && result.Status != GameStatus.Published)
            {
                return Results.NotFound(); // Hide draft/archived games from public
            }
            return Results.Ok(result);
        }

        return Results.NotFound();
    }

    private static async Task<IResult> HandleGetCategories(
        IMediator mediator,
        CancellationToken ct)
    {
        var query = new GetGameCategoriesQuery();
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetMechanics(
        IMediator mediator,
        CancellationToken ct)
    {
        var query = new GetGameMechanicsQuery();
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetGameFaqs(
        Guid gameId,
        IMediator mediator,
        [FromQuery] int limit = 10,
        [FromQuery] int offset = 0,
        CancellationToken ct = default)
    {
        var query = new GetGameFaqsQuery(gameId, limit, offset);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleUpvoteFaq(
        Guid faqId,
        IMediator mediator,
        CancellationToken ct = default)
    {
        try
        {
            var command = new UpvoteFaqCommand(faqId);
            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        }
        catch (InvalidOperationException)
        {
            return Results.NotFound();
        }
    }

    // ========================================
    // ADMIN HANDLERS
    // ========================================

    private static async Task<IResult> HandleListAllGames(
        IMediator mediator,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] Guid? submittedBy = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        // Issue #3533: Parse status string to enum
        GameStatus? statusEnum = null;
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<GameStatus>(status, ignoreCase: true, out var parsedStatus))
        {
            statusEnum = parsedStatus;
        }

        var query = new GetFilteredSharedGamesQuery(statusEnum, search, pageNumber, pageSize, sortBy, submittedBy);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleCreateGame(
        CreateSharedGameRequest request,
        IMediator mediator,
        HttpContext context,
        CancellationToken ct)
    {
        // Auth check (inline for now, policies handle authorization)
        // Get userId from claims (policies already verified Admin/Editor role)
        var userIdClaim = context.User.FindFirst("user_id")?.Value
            ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Results.Unauthorized();
        }

        var command = new CreateSharedGameCommand(
            request.Title,
            request.YearPublished,
            request.Description,
            request.MinPlayers,
            request.MaxPlayers,
            request.PlayingTimeMinutes,
            request.MinAge,
            request.ComplexityRating,
            request.AverageRating,
            request.ImageUrl,
            request.ThumbnailUrl,
            request.Rules,
            userId,
            request.BggId);

        var gameId = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Created($"/api/v1/shared-games/{gameId}", gameId);
    }

    private static async Task<IResult> HandleUpdateGame(
        Guid id,
        UpdateSharedGameRequest request,
        IMediator mediator,
        HttpContext context,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminOrEditorSession();
        if (!authorized) return error!;

        var command = new UpdateSharedGameCommand(
            id,
            request.Title,
            request.YearPublished,
            request.Description,
            request.MinPlayers,
            request.MaxPlayers,
            request.PlayingTimeMinutes,
            request.MinAge,
            request.ComplexityRating,
            request.AverageRating,
            request.ImageUrl,
            request.ThumbnailUrl,
            request.Rules,
            session!.User!.Id);

        try
        {
            await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.NoContent();
        }
        catch (InvalidOperationException)
        {
            return Results.NotFound();
        }
    }

    // ========================================
    // APPROVAL WORKFLOW HANDLERS (Issue #2514)
    // ========================================

    private static async Task<IResult> HandleSubmitForApproval(
        Guid id,
        IMediator mediator,
        HttpContext context,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminOrEditorSession();
        if (!authorized) return error!;

        var command = new SubmitSharedGameForApprovalCommand(id, session!.User!.Id);

        try
        {
            await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.NoContent();
        }
        catch (InvalidOperationException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> HandleApprovePublication(
        Guid id,
        IMediator mediator,
        HttpContext context,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var command = new ApproveSharedGamePublicationCommand(id, session!.User!.Id);

        try
        {
            await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.NoContent();
        }
        catch (InvalidOperationException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> HandleRejectPublication(
        Guid id,
        [FromBody] RejectPublicationRequest request,
        IMediator mediator,
        HttpContext context,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var command = new RejectSharedGamePublicationCommand(id, session!.User!.Id, request.Reason);

        try
        {
            await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.NoContent();
        }
        catch (InvalidOperationException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> HandleGetPendingApprovals(
        IMediator mediator,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new GetPendingApprovalGamesQuery(pageNumber, pageSize);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    // ========================================
    // BATCH APPROVAL HANDLERS (Issue #3350)
    // ========================================

    private static async Task<IResult> HandleBatchApprovePublications(
        [FromBody] BatchApproveGamesRequest request,
        IMediator mediator,
        HttpContext context,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        if (request.GameIds == null || request.GameIds.Count == 0)
        {
            return Results.BadRequest(new { error = "At least one game ID is required" });
        }

        var command = new BatchApproveGamesCommand(
            request.GameIds,
            session!.User!.Id,
            request.Note);

        var result = await mediator.Send(command, ct).ConfigureAwait(false);

        return Results.Ok(new BatchApproveGamesResponse(
            result.SuccessCount,
            result.FailureCount,
            result.Errors));
    }

    private static async Task<IResult> HandleBatchRejectPublications(
        [FromBody] BatchRejectGamesRequest request,
        IMediator mediator,
        HttpContext context,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        if (request.GameIds == null || request.GameIds.Count == 0)
        {
            return Results.BadRequest(new { error = "At least one game ID is required" });
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Results.BadRequest(new { error = "Rejection reason is required" });
        }

        var command = new BatchRejectGamesCommand(
            request.GameIds,
            session!.User!.Id,
            request.Reason);

        var result = await mediator.Send(command, ct).ConfigureAwait(false);

        return Results.Ok(new BatchRejectGamesResponse(
            result.SuccessCount,
            result.FailureCount,
            result.Errors));
    }

    private static async Task<IResult> HandleArchiveGame(
        Guid id,
        IMediator mediator,
        HttpContext context,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var command = new ArchiveSharedGameCommand(id, session!.User!.Id);

        try
        {
            await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.NoContent();
        }
        catch (InvalidOperationException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> HandleImportFromBgg(
        ImportFromBggRequest request,
        IMediator mediator,
        HttpContext context,
        CancellationToken ct)
    {
        // Policies already verified Admin/Editor role - extract userId from claims
        var userIdClaim = context.User.FindFirst("user_id")?.Value
            ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Results.Unauthorized();
        }

        var command = new ImportGameFromBggCommand(request.BggId, userId);
        var gameId = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Created($"/api/v1/shared-games/{gameId}", gameId);
    }

    private static async Task<IResult> HandleBulkImport(
        BulkImportRequest request,
        IMediator mediator,
        HttpContext context,
        CancellationToken ct)
    {
        // Policies already verified Admin role - extract userId from claims
        var userIdClaim = context.User.FindFirst("user_id")?.Value
            ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Results.Unauthorized();
        }

        var command = new BulkImportGamesCommand(request.Games, userId);
        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleBggSearch(
        IMediator mediator,
        [FromQuery] string query,
        [FromQuery] bool exact = false,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Results.BadRequest(new { error = "Search query is required" });
        }

        try
        {
            var searchQuery = new SearchBggGamesQuery(query, exact);
            var results = await mediator.Send(searchQuery, ct).ConfigureAwait(false);
            return Results.Ok(results);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Rate limit"))
        {
            return Results.StatusCode(StatusCodes.Status429TooManyRequests);
        }
    }

    private static async Task<IResult> HandleCheckBggDuplicate(
        int bggId,
        IMediator mediator,
        CancellationToken ct)
    {
        var query = new CheckBggDuplicateQuery(bggId);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    // Issue #3533: New handlers for admin approval workflow
    private static async Task<IResult> HandleGetApprovalQueue(
        IMediator mediator,
        [FromQuery] bool? urgency = null,
        [FromQuery] Guid? submitter = null,
        [FromQuery] bool? hasPdfs = null,
        CancellationToken ct = default)
    {
        var query = new GetApprovalQueueQuery(urgency, submitter, hasPdfs);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleApproveDocument(
        Guid id,
        Guid docId,
        IMediator mediator,
        HttpContext context,
        [FromBody] ApproveDocumentRequest? request,
        CancellationToken ct)
    {
        // Extract approver userId from claims
        var userIdClaim = context.User.FindFirst("user_id")?.Value
            ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(userIdClaim, out var approvedBy))
        {
            return Results.Unauthorized();
        }

        var command = new ApproveDocumentForRagProcessingCommand(docId, approvedBy, request?.Notes);
        await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> HandleUpdateFromBgg(
        Guid id,
        [FromBody] UpdateFromBggRequest request,
        IMediator mediator,
        HttpContext context,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminOrEditorSession();
        if (!authorized) return error!;

        var command = new UpdateSharedGameFromBggCommand(
            id,
            request.BggId,
            session!.User!.Id,
            request.FieldsToUpdate);

        try
        {
            var gameId = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(gameId);
        }
        catch (InvalidOperationException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> HandleDeleteGame(
        Guid id,
        IMediator mediator,
        HttpContext context,
        [FromBody] DeleteGameRequest? request,
        CancellationToken ct)
    {
        // Get userId and role from claims (policies already verified Admin/Editor role)
        var userIdClaim = context.User.FindFirst("user_id")?.Value
            ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Results.Unauthorized();
        }

        var isAdmin = context.User.IsInRole("Admin");

        if (isAdmin)
        {
            // Admin: direct delete
            var command = new DeleteSharedGameCommand(id, userId);
            await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.NoContent();
        }
        else
        {
            // Editor: create delete request for admin approval
            var requestCommand = new RequestDeleteSharedGameCommand(
                id,
                userId,
                request?.Reason ?? "Delete requested");

            var requestId = await mediator.Send(requestCommand, ct).ConfigureAwait(false);
            return Results.Accepted($"/api/v1/admin/shared-games/pending-deletes", requestId);
        }
    }

    private static async Task<IResult> HandleGetPendingDeletes(
        IMediator mediator,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new GetPendingDeleteRequestsQuery(pageNumber, pageSize);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleApproveDelete(
        Guid requestId,
        IMediator mediator,
        HttpContext context,
        CancellationToken ct)
    {
        // Get userId from claims (policies already verified Admin role)
        var userIdClaim = context.User.FindFirst("user_id")?.Value
            ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Results.Unauthorized();
        }
        var command = new ApproveDeleteRequestCommand(requestId, userId, Comment: null);

        try
        {
            await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.NoContent();
        }
        catch (InvalidOperationException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> HandleRejectDelete(
        Guid requestId,
        [FromBody] RejectDeleteRequest request,
        IMediator mediator,
        HttpContext context,
        CancellationToken ct)
    {
        // Get userId from claims (policies already verified Admin role)
        var userIdClaim = context.User.FindFirst("user_id")?.Value
            ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Results.Unauthorized();
        }
        var command = new RejectDeleteRequestCommand(requestId, userId, request.Reason);

        try
        {
            await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.NoContent();
        }
        catch (InvalidOperationException)
        {
            return Results.NotFound();
        }
    }

    // Quick Question Handlers
    private static async Task<IResult> HandleGetQuickQuestions(
        Guid id,
        IMediator mediator,
        CancellationToken ct = default)
    {
        var query = new GetQuickQuestionsQuery(id, ActiveOnly: true);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGenerateQuickQuestions(
        Guid id,
        IMediator mediator,
        CancellationToken ct = default)
    {
        var command = new GenerateQuickQuestionsCommand(id);
        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Created($"/games/{id}/quick-questions", result);
    }

    private static async Task<IResult> HandleAddManualQuickQuestion(
        Guid id,
        [FromBody] AddQuickQuestionRequest request,
        IMediator mediator,
        CancellationToken ct = default)
    {
        var command = new AddManualQuickQuestionCommand(id, request.Text, request.Emoji, request.Category, request.DisplayOrder);
        var questionId = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Created($"/admin/quick-questions/{questionId}", questionId);
    }

    private static async Task<IResult> HandleUpdateQuickQuestion(
        Guid questionId,
        [FromBody] UpdateQuickQuestionRequest request,
        IMediator mediator,
        CancellationToken ct = default)
    {
        var command = new UpdateQuickQuestionCommand(questionId, request.Text, request.Emoji, request.Category, request.DisplayOrder);
        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleDeleteQuickQuestion(
        Guid questionId,
        IMediator mediator,
        CancellationToken ct = default)
    {
        var command = new DeleteQuickQuestionCommand(questionId);
        await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.NoContent();
    }

    // FAQ Handlers
    private static async Task<IResult> HandleAddFaq(
        Guid id,
        [FromBody] AddFaqRequest request,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new AddGameFaqCommand(id, request.Question, request.Answer, request.Order);
        var faqId = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Created($"/api/v1/admin/shared-games/{id}/faq/{faqId}", faqId);
    }

#pragma warning disable S1172 // Unused method parameters - Route parameter for URL binding, command uses faqId directly
    private static async Task<IResult> HandleUpdateFaq(
        Guid _,
        Guid faqId,
        [FromBody] UpdateFaqRequest request,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new UpdateGameFaqCommand(faqId, request.Question, request.Answer, request.Order);

        try
        {
            await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.NoContent();
        }
        catch (InvalidOperationException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> HandleDeleteFaq(
        Guid _,
        Guid faqId,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new DeleteGameFaqCommand(faqId);

        try
        {
            await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.NoContent();
        }
        catch (InvalidOperationException)
        {
            return Results.NotFound();
        }
    }
#pragma warning restore S1172

    // Errata Handlers
    private static async Task<IResult> HandleAddErrata(
        Guid id,
        [FromBody] AddErrataRequest request,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new AddGameErrataCommand(id, request.Description, request.PageReference, request.PublishedDate);
        var errataId = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Created($"/api/v1/admin/shared-games/{id}/errata/{errataId}", errataId);
    }

#pragma warning disable S1172 // Unused method parameters - Route parameter for URL binding, command uses errataId directly
    private static async Task<IResult> HandleUpdateErrata(
        Guid _,
        Guid errataId,
        [FromBody] UpdateErrataRequest request,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new UpdateGameErrataCommand(errataId, request.Description, request.PageReference, request.PublishedDate);

        try
        {
            await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.NoContent();
        }
        catch (InvalidOperationException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> HandleDeleteErrata(
        Guid _,
        Guid errataId,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new DeleteGameErrataCommand(errataId);

        try
        {
            await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.NoContent();
        }
        catch (InvalidOperationException)
        {
            return Results.NotFound();
        }
    }
#pragma warning restore S1172

    // ========================================
    // DOCUMENT HANDLERS (Issue #2391 Sprint 1)
    // ========================================

    private static async Task<IResult> HandleGetDocuments(
        Guid id,
        IMediator mediator,
        [FromQuery] SharedGameDocumentType? type,
        CancellationToken ct)
    {
        var query = new GetDocumentsBySharedGameQuery(id, type);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetActiveDocuments(
        Guid id,
        IMediator mediator,
        CancellationToken ct)
    {
        var query = new GetActiveDocumentsQuery(id);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleAddDocument(
        Guid id,
        [FromBody] AddDocumentRequest request,
        IMediator mediator,
        HttpContext context,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminOrEditorSession();
        if (!authorized) return error!;

        var command = new AddDocumentToSharedGameCommand(
            id,
            request.PdfDocumentId,
            request.DocumentType,
            request.Version,
            request.Tags,
            request.SetAsActive,
            session!.User!.Id);

        try
        {
            var documentId = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Created($"/api/v1/admin/shared-games/{id}/documents/{documentId}", documentId);
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

#pragma warning disable S1172 // Unused method parameters - Route parameter for URL binding
    private static async Task<IResult> HandleSetActiveDocument(
        Guid _,
        Guid documentId,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new SetActiveDocumentVersionCommand(_, documentId);

        try
        {
            await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.NoContent();
        }
        catch (InvalidOperationException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> HandleRemoveDocument(
        Guid id,
        Guid documentId,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new RemoveDocumentFromSharedGameCommand(id, documentId);

        try
        {
            await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    // ========================================
    // AGENT LINKING HANDLERS (Issue #4228)
    // ========================================

    private static async Task<IResult> HandleLinkAgent(
        Guid id,
        Guid agentId,
        IMediator mediator,
        HttpContext context,
        CancellationToken ct)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var command = new LinkAgentToSharedGameCommand(id, agentId);

        try
        {
            await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.NoContent();
        }
        catch (NotFoundException)
        {
            return Results.NotFound();
        }
        catch (InvalidOperationException ex)
        {
            // Agent already linked
            return Results.Conflict(new { error = ex.Message });
        }
    }

    private static async Task<IResult> HandleUnlinkAgent(
        Guid id,
        IMediator mediator,
        HttpContext context,
        CancellationToken ct)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var command = new UnlinkAgentFromSharedGameCommand(id);

        try
        {
            await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.NoContent();
        }
        catch (NotFoundException)
        {
            return Results.NotFound();
        }
        catch (InvalidOperationException ex)
        {
            // No agent linked
            return Results.Conflict(new { error = ex.Message });
        }
    }
#pragma warning restore S1172

    // ========================================
    // GAME STATE TEMPLATE HANDLERS (Issue #2400)
    // ========================================

    private static async Task<IResult> HandleGetActiveStateTemplate(
        Guid id,
        IMediator mediator,
        CancellationToken ct)
    {
        var query = new GetActiveGameStateTemplateQuery(id);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);

        return result is not null
            ? Results.Ok(result)
            : Results.NotFound();
    }

    private static async Task<IResult> HandleGetStateTemplateVersions(
        Guid id,
        IMediator mediator,
        CancellationToken ct)
    {
        var query = new GetGameStateTemplateVersionsQuery(id);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGenerateStateTemplate(
        Guid id,
        [FromBody] GenerateStateTemplateRequest request,
        IMediator mediator,
        HttpContext context,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminOrEditorSession();
        if (!authorized) return error!;

        var command = new GenerateGameStateTemplateCommand(
            id,
            request.Name,
            session!.User!.Id,
            request.SetAsActive);

        try
        {
            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Created($"/api/v1/admin/shared-games/{id}/state-template/{result.Id}", result);
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

#pragma warning disable S1172 // Unused method parameters - Route parameter for URL binding
    private static async Task<IResult> HandleUpdateStateTemplate(
        Guid _,
        Guid templateId,
        [FromBody] UpdateStateTemplateRequest request,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new UpdateGameStateTemplateCommand(
            templateId,
            request.Name,
            request.SchemaJson,
            request.Version);

        try
        {
            await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.NoContent();
        }
        catch (InvalidOperationException)
        {
            return Results.NotFound();
        }
    }

    private static async Task<IResult> HandleActivateStateTemplate(
        Guid _,
        Guid templateId,
        IMediator mediator,
        CancellationToken ct)
    {
        var command = new ActivateGameStateTemplateCommand(templateId);

        try
        {
            await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.NoContent();
        }
        catch (InvalidOperationException)
        {
            return Results.NotFound();
        }
    }
#pragma warning restore S1172

    // ========================================
    // USER SHARE REQUEST ENDPOINTS (Protected)
    // Issue #2733
    // ========================================

    private static void MapUserShareRequestEndpoints(RouteGroupBuilder group)
    {
        // Create share request
        group.MapPost("/share-requests", HandleCreateShareRequest)
            .RequireAuthorization()
            .RequireRateLimiting("ShareRequestCreation")
            .WithName("CreateShareRequest")
            .WithSummary("Create a new share request")
            .WithDescription("Submit a game from user library to the shared catalog for community review.")
            .Produces<CreateShareRequestResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status429TooManyRequests);

        // List user's share requests
        group.MapGet("/share-requests", HandleGetUserShareRequests)
            .RequireAuthorization()
            .RequireRateLimiting("ShareRequestQuery")
            .WithName("GetUserShareRequests")
            .WithSummary("Get user's share requests")
            .WithDescription("Returns all share requests created by the authenticated user with pagination and status filtering.")
            .Produces<PagedResult<UserShareRequestDto>>();

        // Get share request details
        group.MapGet("/share-requests/{id:guid}", HandleGetShareRequestDetails)
            .RequireAuthorization()
            .RequireRateLimiting("ShareRequestQuery")
            .WithName("GetShareRequestDetails")
            .WithSummary("Get share request details")
            .WithDescription("Returns detailed information about a specific share request. User can only access their own requests.")
            .Produces<ShareRequestDetailsDto>()
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status403Forbidden);

        // Update share request documents
        group.MapPut("/share-requests/{id:guid}/documents", HandleUpdateShareRequestDocuments)
            .RequireAuthorization()
            .RequireRateLimiting("ShareRequestUpdate")
            .WithName("UpdateShareRequestDocuments")
            .WithSummary("Update attached documents")
            .WithDescription("Update the list of documents attached to a pending share request. User can only update their own requests.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status403Forbidden);

        // Withdraw share request
        group.MapDelete("/share-requests/{id:guid}", HandleWithdrawShareRequest)
            .RequireAuthorization()
            .RequireRateLimiting("ShareRequestUpdate")
            .WithName("WithdrawShareRequest")
            .WithSummary("Withdraw a share request")
            .WithDescription("Withdraw a pending share request. Only pending requests can be withdrawn. User can only withdraw their own requests.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status403Forbidden);
    }

    // ========================================
    // USER SHARE REQUEST HANDLERS
    // ========================================

    private static async Task<IResult> HandleCreateShareRequest(
        CreateShareRequestRequest request,
        IMediator mediator,
        HttpContext context,
        CancellationToken ct)
    {
        // Extract user ID from claims
        var userIdClaim = context.User.FindFirst("user_id")?.Value
            ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Results.Unauthorized();
        }

        var command = new CreateShareRequestCommand(
            userId,
            request.SourceGameId,
            request.Notes,
            request.AttachedDocumentIds ?? new List<Guid>());

        var response = await mediator.Send(command, ct).ConfigureAwait(false);

        return Results.Created(
            $"/api/v1/share-requests/{response.ShareRequestId}",
            response);
    }

    private static async Task<IResult> HandleGetUserShareRequests(
        [FromQuery] ShareRequestStatus? status,
        [FromQuery] int pageNumber,
        [FromQuery] int pageSize,
        IMediator mediator,
        HttpContext context,
        CancellationToken ct)
    {
        // Extract user ID from claims
        var userIdClaim = context.User.FindFirst("user_id")?.Value
            ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Results.Unauthorized();
        }

        var query = new GetUserShareRequestsQuery(
            userId,
            status,
            pageNumber > 0 ? pageNumber : 1,
            pageSize > 0 ? pageSize : 20);

        var result = await mediator.Send(query, ct).ConfigureAwait(false);

        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetShareRequestDetails(
        Guid id,
        IMediator mediator,
        HttpContext context,
        CancellationToken ct)
    {
        // Extract user ID from claims
        var userIdClaim = context.User.FindFirst("user_id")?.Value
            ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Results.Unauthorized();
        }

        var query = new GetShareRequestDetailsQuery(id, userId);

        try
        {
            var result = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(result);
        }
        catch (InvalidOperationException)
        {
            // Request not found or user doesn't own it
            return Results.NotFound();
        }
    }

    private static async Task<IResult> HandleUpdateShareRequestDocuments(
        Guid id,
        UpdateShareRequestDocumentsRequest request,
        IMediator mediator,
        HttpContext context,
        CancellationToken ct)
    {
        // Extract user ID from claims
        var userIdClaim = context.User.FindFirst("user_id")?.Value
            ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Results.Unauthorized();
        }

        var command = new UpdateShareRequestDocumentsCommand(
            id,
            userId,
            request.DocumentIds ?? new List<Guid>());

        try
        {
            await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.NoContent();
        }
        catch (InvalidOperationException)
        {
            // Request not found, user doesn't own it, or invalid state
            return Results.NotFound();
        }
    }

    private static async Task<IResult> HandleWithdrawShareRequest(
        Guid id,
        IMediator mediator,
        HttpContext context,
        CancellationToken ct)
    {
        // Extract user ID from claims
        var userIdClaim = context.User.FindFirst("user_id")?.Value
            ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Results.Unauthorized();
        }

        var command = new WithdrawShareRequestCommand(id, userId);

        try
        {
            await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.NoContent();
        }
        catch (InvalidOperationException)
        {
            // Request not found, user doesn't own it, or can't be withdrawn (not pending)
            return Results.NotFound();
        }
    }

    // ========================================
    // ADMIN SHARE REQUEST HANDLERS
    // Issue #2734
    // ========================================

    private static async Task<IResult> HandleGetPendingShareRequests(
        [FromQuery] ShareRequestStatus? status,
        [FromQuery] ContributionType? type,
        [FromQuery] string? search,
        [FromQuery] ShareRequestSortField sortBy,
        [FromQuery] SortDirection sortDirection,
        [FromQuery] int pageNumber,
        [FromQuery] int pageSize,
        IMediator mediator,
        CancellationToken ct)
    {
        var query = new GetPendingShareRequestsQuery(
            status,
            type,
            search,
            sortBy,
            sortDirection,
            pageNumber > 0 ? pageNumber : 1,
            pageSize > 0 && pageSize <= 100 ? pageSize : 20);

        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetShareRequestForReview(
        Guid id,
        IMediator mediator,
        HttpContext context,
        CancellationToken ct)
    {
        // Extract admin ID from claims
        var userIdClaim = context.User.FindFirst("user_id")?.Value
            ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(userIdClaim, out var adminId))
        {
            return Results.Unauthorized();
        }

        var query = new GetShareRequestDetailsQuery(id, adminId);

        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return result is not null ? Results.Ok(result) : Results.NotFound();
    }

    private static async Task<IResult> HandleApproveShareRequest(
        Guid id,
        ApproveShareRequestRequest request,
        IMediator mediator,
        HttpContext context,
        CancellationToken ct)
    {
        // Extract admin ID from claims
        var userIdClaim = context.User.FindFirst("user_id")?.Value
            ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(userIdClaim, out var adminId))
        {
            return Results.Unauthorized();
        }

        var command = new ApproveShareRequestCommand(
            id,
            adminId,
            request.TargetSharedGameId,
            request.AdminNotes);

        try
        {
            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        }
        catch (ShareRequestReviewerMismatchException ex)
        {
            // Another admin has the lock
            throw new ConflictException(ex.Message);
        }
        catch (ShareRequestLockExpiredException ex)
        {
            // Lock expired
            throw new ConflictException(ex.Message);
        }
        catch (InvalidShareRequestStateException ex)
        {
            // Invalid state transition
            throw new ConflictException(ex.Message);
        }
    }

    private static async Task<IResult> HandleApproveGameProposal(
        Guid id,
        ApproveGameProposalRequest request,
        IMediator mediator,
        HttpContext context,
        CancellationToken ct)
    {
        // Extract admin ID from claims
        var userIdClaim = context.User.FindFirst("user_id")?.Value
            ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(userIdClaim, out var adminId))
        {
            return Results.Unauthorized();
        }

        var command = new ApproveGameProposalCommand(
            id,
            adminId,
            request.ApprovalAction,
            request.TargetSharedGameId,
            request.AdminNotes);

        try
        {
            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        }
        catch (ShareRequestReviewerMismatchException ex)
        {
            // Another admin has the lock
            throw new ConflictException(ex.Message);
        }
        catch (ShareRequestLockExpiredException ex)
        {
            // Lock expired
            throw new ConflictException(ex.Message);
        }
        catch (InvalidShareRequestStateException ex)
        {
            // Invalid state transition
            throw new ConflictException(ex.Message);
        }
    }

    private static async Task<IResult> HandleCheckPrivateGameDuplicates(
        Guid id,
        IMediator mediator,
        CancellationToken ct)
    {
        var query = new CheckPrivateGameDuplicatesQuery(id);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleRejectShareRequest(
        Guid id,
        RejectShareRequestRequest request,
        IMediator mediator,
        HttpContext context,
        CancellationToken ct)
    {
        // Extract admin ID from claims
        var userIdClaim = context.User.FindFirst("user_id")?.Value
            ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(userIdClaim, out var adminId))
        {
            return Results.Unauthorized();
        }

        var command = new RejectShareRequestCommand(id, adminId, request.Reason);

        try
        {
            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        }
        catch (ShareRequestReviewerMismatchException ex)
        {
            // Another admin has the lock
            throw new ConflictException(ex.Message);
        }
        catch (ShareRequestLockExpiredException ex)
        {
            // Lock expired
            throw new ConflictException(ex.Message);
        }
        catch (InvalidShareRequestStateException ex)
        {
            // Invalid state transition
            throw new ConflictException(ex.Message);
        }
    }

    // Issue #2893: Bulk approve share requests handler
    private static async Task<IResult> HandleBulkApproveShareRequests(
        BulkApproveShareRequestsRequest request,
        IMediator mediator,
        HttpContext context,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminOrEditorSession();
        if (!authorized) return error!;

        var editorId = session!.User!.Id;

        logger.LogInformation(
            "Editor {EditorId} bulk approving {Count} share requests",
            editorId, request.ShareRequestIds.Count);

        try
        {
            var command = new BulkApproveShareRequestsCommand(
                request.ShareRequestIds,
                editorId,
                request.TargetSharedGameId,
                request.AdminNotes);

            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogInformation(
                "Bulk approval result: {SuccessCount}/{TotalRequested} succeeded",
                result.SuccessCount, result.TotalRequested);

            return Results.Ok(result);
        }
        catch (DomainException ex)
        {
            logger.LogWarning(ex, "Domain error during bulk approval");
            return Results.BadRequest(new { error = "domain_error", message = ex.Message });
        }
    }

    // Issue #2893: Bulk reject share requests handler
    private static async Task<IResult> HandleBulkRejectShareRequests(
        BulkRejectShareRequestsRequest request,
        IMediator mediator,
        HttpContext context,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminOrEditorSession();
        if (!authorized) return error!;

        var editorId = session!.User!.Id;

        logger.LogInformation(
            "Editor {EditorId} bulk rejecting {Count} share requests with reason: {Reason}",
            editorId, request.ShareRequestIds.Count, request.Reason);

        try
        {
            var command = new BulkRejectShareRequestsCommand(
                request.ShareRequestIds,
                editorId,
                request.Reason);

            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogInformation(
                "Bulk rejection result: {SuccessCount}/{TotalRequested} succeeded",
                result.SuccessCount, result.TotalRequested);

            return Results.Ok(result);
        }
        catch (DomainException ex)
        {
            logger.LogWarning(ex, "Domain error during bulk rejection");
            return Results.BadRequest(new { error = "domain_error", message = ex.Message });
        }
    }

    private static async Task<IResult> HandleRequestShareRequestChanges(
        Guid id,
        RequestShareRequestChangesRequest request,
        IMediator mediator,
        HttpContext context,
        CancellationToken ct)
    {
        // Extract admin ID from claims
        var userIdClaim = context.User.FindFirst("user_id")?.Value
            ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(userIdClaim, out var adminId))
        {
            return Results.Unauthorized();
        }

        var command = new RequestShareRequestChangesCommand(id, adminId, request.Feedback);

        try
        {
            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        }
        catch (ShareRequestReviewerMismatchException ex)
        {
            // Another admin has the lock
            throw new ConflictException(ex.Message);
        }
        catch (ShareRequestLockExpiredException ex)
        {
            // Lock expired
            throw new ConflictException(ex.Message);
        }
        catch (InvalidShareRequestStateException ex)
        {
            // Invalid state transition
            throw new ConflictException(ex.Message);
        }
    }

    private static async Task<IResult> HandleStartReview(
        Guid id,
        IMediator mediator,
        HttpContext context,
        CancellationToken ct)
    {
        // Extract admin ID from claims
        var userIdClaim = context.User.FindFirst("user_id")?.Value
            ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(userIdClaim, out var adminId))
        {
            return Results.Unauthorized();
        }

        var command = new StartReviewCommand(id, adminId);

        try
        {
            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Ok(result);
        }
        catch (ShareRequestAlreadyInReviewException ex)
        {
            // Another admin has the lock
            return Results.Conflict(new ProblemDetails
            {
                Title = "Request Already In Review",
                Detail = ex.Message,
                Status = StatusCodes.Status409Conflict,
                Extensions = { ["reviewingAdminId"] = ex.CurrentReviewerAdminId }
            });
        }
        catch (InvalidShareRequestStateException ex)
        {
            // Invalid state for review
            throw new ConflictException(ex.Message);
        }
    }

    private static async Task<IResult> HandleReleaseReview(
        Guid id,
        IMediator mediator,
        HttpContext context,
        CancellationToken ct)
    {
        // Extract admin ID from claims
        var userIdClaim = context.User.FindFirst("user_id")?.Value
            ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(userIdClaim, out var adminId))
        {
            return Results.Unauthorized();
        }

        var command = new ReleaseReviewCommand(id, adminId);

        try
        {
            await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.NoContent();
        }
        catch (ShareRequestReviewerMismatchException)
        {
            // Not the reviewing admin
            return Results.Forbid();
        }
    }

    private static async Task<IResult> HandleGetMyActiveReviews(
        IMediator mediator,
        HttpContext context,
        CancellationToken ct)
    {
        // Extract admin ID from claims
        var userIdClaim = context.User.FindFirst("user_id")?.Value
            ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(userIdClaim, out var adminId))
        {
            return Results.Unauthorized();
        }

        var query = new GetMyActiveReviewsQuery(adminId);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    // ========================================
    // CONTRIBUTOR ENDPOINTS
    // Issue #2735
    // ========================================

    private static void MapContributorEndpoints(RouteGroupBuilder group)
    {
        // Public: Get contributors for a shared game
        group.MapGet("/shared-games/{id:guid}/contributors", HandleGetGameContributors)
            .AllowAnonymous()
            .RequireRateLimiting("SharedGamesPublic")
            .WithName("GetGameContributors")
            .WithSummary("Get contributors for a shared game")
            .WithDescription("Returns all contributors who have contributed to a shared game, including their contribution count and top badges.")
            .Produces<List<GameContributorDto>>()
            .Produces(StatusCodes.Status404NotFound);

        // Public: Get user's contributions (paginated)
        group.MapGet("/users/{id:guid}/contributions", HandleGetUserContributions)
            .AllowAnonymous()
            .RequireRateLimiting("SharedGamesPublic")
            .WithName("GetUserContributions")
            .WithSummary("Get user's contributions")
            .WithDescription("Returns paginated list of contributions made by a specific user to shared games.")
            .Produces<PagedResult<UserContributionDto>>();

        // Private: Get my contribution stats
        group.MapGet("/users/me/contribution-stats", HandleGetMyContributionStats)
            .RequireAuthorization()
            .RequireRateLimiting("UserDashboard")
            .WithName("GetMyContributionStats")
            .WithSummary("Get my contribution statistics")
            .WithDescription("Returns detailed contribution statistics for the authenticated user, including badges, rate limits, and approval rate.")
            .Produces<UserContributionStatsDto>();
    }

    // ========================================
    // CONTRIBUTOR HANDLERS
    // ========================================

    private static async Task<IResult> HandleGetGameContributors(
        Guid id,
        IMediator mediator,
        CancellationToken ct)
    {
        var query = new GetGameContributorsQuery(id);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetUserContributions(
        Guid id,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        IMediator mediator = default!,
        CancellationToken ct = default)
    {
        var query = new GetUserContributionsQuery(
            id,
            pageNumber,
            pageSize);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetMyContributionStats(
        IMediator mediator,
        HttpContext context,
        CancellationToken ct)
    {
        // Extract user ID from claims
        var userIdClaim = context.User.FindFirst("user_id")?.Value
            ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Results.Unauthorized();
        }

        var query = new GetUserContributionStatsQuery(userId);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    // ========================================
    // BADGE ENDPOINTS (Issue #2736)
    // ========================================

    private static void MapBadgeEndpoints(RouteGroupBuilder group)
    {
        // Public: Get all badge definitions
        group.MapGet("/badges", HandleGetAllBadges)
            .AllowAnonymous()
            .RequireRateLimiting("SharedGamesPublic")
            .WithName("GetAllBadges")
            .WithSummary("Get all badge definitions")
            .WithDescription("Returns all available badge types with requirement descriptions. Results are cached for 24 hours.")
            .Produces<List<BadgeDefinitionDto>>();

        // Public: Get user's badges (excludes hidden)
        group.MapGet("/users/{id:guid}/badges", HandleGetUserBadges)
            .AllowAnonymous()
            .RequireRateLimiting("SharedGamesPublic")
            .WithName("GetUserBadges")
            .WithSummary("Get user's badges")
            .WithDescription("Returns all visible badges earned by a specific user. Hidden badges are excluded.")
            .Produces<List<UserBadgeDto>>();

        // Private: Get my badges (includes hidden)
        group.MapGet("/users/me/badges", HandleGetMyBadges)
            .RequireAuthorization()
            .RequireRateLimiting("UserDashboard")
            .WithName("GetMyBadges")
            .WithSummary("Get my badges")
            .WithDescription("Returns all badges earned by the authenticated user, including hidden badges.")
            .Produces<List<UserBadgeDto>>();

        // Public: Get badge leaderboard
        group.MapGet("/badges/leaderboard", HandleGetLeaderboard)
            .AllowAnonymous()
            .RequireRateLimiting("SharedGamesPublic")
            .WithName("GetBadgeLeaderboard")
            .WithSummary("Get badge leaderboard")
            .WithDescription("Returns top contributors with badge counts, highest tier, and top 3 badges. Supports period filtering (Week, Month, AllTime). Results are cached for 1 hour.")
            .Produces<List<LeaderboardEntryDto>>();

        // Private: Toggle badge visibility
        group.MapPut("/users/me/badges/{id:guid}/display", HandleToggleBadgeDisplay)
            .RequireAuthorization()
            .RequireRateLimiting("UserDashboard")
            .WithName("ToggleBadgeDisplay")
            .WithSummary("Toggle badge visibility")
            .WithDescription("Toggle whether a badge is displayed on your public profile.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);
    }

    // ========================================
    // BADGE HANDLERS (Issue #2736)
    // ========================================

    private static async Task<IResult> HandleGetAllBadges(
        IMediator mediator,
        CancellationToken ct)
    {
        var query = new GetAllBadgesQuery();
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetUserBadges(
        Guid id,
        IMediator mediator,
        CancellationToken ct)
    {
        var query = new GetUserBadgesQuery(id, IncludeHidden: false);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetMyBadges(
        IMediator mediator,
        HttpContext context,
        CancellationToken ct)
    {
        // Extract user ID from claims
        var userIdClaim = context.User.FindFirst("user_id")?.Value
            ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Results.Unauthorized();
        }

        var query = new GetUserBadgesQuery(userId, IncludeHidden: true);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetLeaderboard(
        [FromQuery] LeaderboardPeriod period,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        IMediator mediator = default!,
        CancellationToken ct = default)
    {
        var query = new GetBadgeLeaderboardQuery(period, pageNumber, pageSize);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleToggleBadgeDisplay(
        Guid id,
        ToggleBadgeDisplayRequest request,
        IMediator mediator,
        HttpContext context,
        CancellationToken ct)
    {
        // Extract user ID from claims
        var userIdClaim = context.User.FindFirst("user_id")?.Value
            ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            return Results.Unauthorized();
        }

        var command = new ToggleBadgeDisplayCommand(id, userId, request.IsDisplayed);

        await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.NoContent();
    }

    // ========================================
    // TRENDING ENDPOINTS (Issue #3918)
    // ========================================

    private static void MapTrendingEndpoints(RouteGroupBuilder group)
    {
        // Get trending games
        group.MapGet("/catalog/trending", HandleGetCatalogTrending)
            .AllowAnonymous()
            .RequireRateLimiting("SharedGamesPublic")
            .WithName("GetCatalogTrending")
            .WithSummary("Get top trending games in the catalog")
            .WithDescription("Returns the top trending games based on weighted analytics events (searches, views, library additions, plays) from the last 7 days with time-decay scoring.")
            .Produces<List<TrendingGameDto>>()
            .Produces(StatusCodes.Status400BadRequest);

        // Record a game analytics event
        group.MapPost("/catalog/events", HandleRecordGameEvent)
            .RequireAuthorization()
            .RequireRateLimiting("SharedGamesPublic")
            .WithName("RecordGameEvent")
            .WithSummary("Record a game analytics event")
            .WithDescription("Records a game interaction event (search, view, library addition, play) for trending analytics.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest);
    }

    // ========================================
    // PDF WIZARD ENDPOINTS (Admin/Editor)
    // Issue #4139: Backend - API Endpoints PDF Wizard
    // ========================================

    private static void MapWizardEndpoints(RouteGroupBuilder group)
    {
        // Step 1: Upload PDF for temporary storage (no processing)
        group.MapPost("/admin/shared-games/wizard/upload-pdf", HandleWizardUploadPdf)
            .DisableAntiforgery() // Required for multipart/form-data file uploads
            .RequireAuthorization("AdminOrEditorPolicy")
            .RequireRateLimiting("SharedGamesAdmin")
            .WithName("WizardUploadPdf")
            .WithSummary("Upload PDF for wizard (Admin/Editor)")
            .WithDescription("Uploads PDF to temporary storage for metadata extraction. File path returned for subsequent wizard steps.")
            .Produces<TempPdfUploadResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        // Step 2: Get PDF preview with extracted metadata and BGG suggestions
        group.MapGet("/admin/shared-games/wizard/pdf/preview", HandleWizardPreview)
            .RequireAuthorization("AdminOrEditorPolicy")
            .RequireRateLimiting("SharedGamesAdmin")
            .WithName("WizardGetPdfPreview")
            .WithSummary("Get PDF preview with metadata (Admin/Editor)")
            .WithDescription("Extracts game metadata from uploaded PDF, fetches BGG match suggestions (top 5), and checks for duplicate games by title.")
            .Produces<PdfGamePreviewDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        // Step 3a: Search BGG games by title
        group.MapGet("/admin/shared-games/wizard/bgg/search", HandleWizardBggSearch)
            .RequireAuthorization("AdminOrEditorPolicy")
            .RequireRateLimiting("SharedGamesAdmin")
            .WithName("WizardSearchBgg")
            .WithSummary("Search BGG games (Admin/Editor)")
            .WithDescription("Searches BoardGameGeek API for games matching the query. Returns top matching results for manual BGG ID selection.")
            .Produces<List<BggSearchResultDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status429TooManyRequests);

        // Step 3b: Get BGG game details by ID
        group.MapGet("/admin/shared-games/wizard/bgg/{bggId:int}", HandleWizardBggDetails)
            .RequireAuthorization("AdminOrEditorPolicy")
            .RequireRateLimiting("SharedGamesAdmin")
            .WithName("WizardGetBggDetails")
            .WithSummary("Get BGG game details (Admin/Editor)")
            .WithDescription("Fetches detailed game information from BoardGameGeek API for preview and data merge.")
            .Produces<BggGameDetailsDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        // Step 4: Create SharedGame from PDF metadata (final wizard step)
        group.MapPost("/admin/shared-games/wizard/create", HandleWizardCreateGame)
            .RequireAuthorization("AdminOrEditorPolicy")
            .RequireRateLimiting("SharedGamesAdmin")
            .WithName("WizardCreateGame")
            .WithSummary("Create game from PDF wizard (Admin/Editor)")
            .WithDescription("Creates SharedGame from extracted metadata with optional BGG enrichment. Admin users publish immediately, Editor users create draft requiring approval.")
            .Produces<CreateGameFromPdfResult>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> HandleGetCatalogTrending(
        [FromQuery] int limit,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var query = new GetCatalogTrendingQuery(limit > 0 ? limit : 10);
        var result = await mediator.Send(query, cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleRecordGameEvent(
        RecordGameEventRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new RecordGameEventCommand(
            request.GameId,
            request.EventType,
            request.UserId);

        await mediator.Send(command, cancellationToken).ConfigureAwait(false);
        return Results.NoContent();
    }

    // ========================================
    // PDF WIZARD HANDLERS (Issue #4139)
    // ========================================

    /// <summary>
    /// Handler for wizard PDF upload endpoint.
    /// Uploads PDF to temporary storage without processing.
    /// </summary>
    private static async Task<IResult> HandleWizardUploadPdf(
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var userId = context.User.GetUserId();

        // Read multipart/form-data
        var form = await context.Request.ReadFormAsync(ct).ConfigureAwait(false);

        // Defensive check: form.Files could be null or empty
        if (form.Files == null || form.Files.Count == 0)
        {
            return Results.BadRequest(new { error = "validation_failed", details = new Dictionary<string, string>(StringComparer.Ordinal) { ["file"] = "No file provided" } });
        }

        var file = form.Files.GetFile("file");

        // Null check BEFORE accessing file.Length
        if (file == null)
        {
            return Results.BadRequest(new { error = "validation_failed", details = new Dictionary<string, string>(StringComparer.Ordinal) { ["file"] = "No file provided" } });
        }

        if (file.Length == 0)
        {
            return Results.BadRequest(new { error = "validation_failed", details = new Dictionary<string, string>(StringComparer.Ordinal) { ["file"] = "File is empty" } });
        }

        var command = new UploadPdfForGameExtractionCommand(file, userId);
        var result = await mediator.Send(command, ct).ConfigureAwait(false);

        if (!result.Success)
        {
            logger.LogWarning("Wizard PDF upload failed: {Error}", result.ErrorMessage);
            return Results.BadRequest(new { error = result.ErrorMessage });
        }

        logger.LogInformation("Wizard PDF uploaded successfully: FileId={FileId}, FilePath={FilePath}", result.FileId, result.FilePath);
        return Results.Ok(result);
    }

    /// <summary>
    /// Handler for wizard PDF preview endpoint.
    /// Extracts metadata, fetches BGG suggestions, and checks duplicates.
    /// </summary>
    private static async Task<IResult> HandleWizardPreview(
        [FromQuery] string filePath,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var userId = context.User.GetUserId();

        if (string.IsNullOrWhiteSpace(filePath))
        {
            return Results.BadRequest(new { error = "validation_failed", details = new Dictionary<string, string>(StringComparer.Ordinal) { ["filePath"] = "File path is required" } });
        }

        logger.LogInformation("Generating wizard preview for FilePath={FilePath}, UserId={UserId}", filePath, userId);

        var query = new GetPdfPreviewForWizardQuery(filePath, userId);
        var preview = await mediator.Send(query, ct).ConfigureAwait(false);

        return Results.Ok(preview);
    }

    /// <summary>
    /// Handler for wizard BGG search endpoint.
    /// Searches BoardGameGeek API for games matching query.
    /// </summary>
    private static async Task<IResult> HandleWizardBggSearch(
        [FromQuery] string query,
        [FromQuery] bool exact,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Results.BadRequest(new { error = "validation_failed", details = new Dictionary<string, string>(StringComparer.Ordinal) { ["query"] = "Search query is required" } });
        }

        logger.LogInformation("Wizard BGG search: Query={Query}, Exact={Exact}", query, exact);

        try
        {
            var searchQuery = new SearchBggGamesQuery(query, exact);
            var results = await mediator.Send(searchQuery, ct).ConfigureAwait(false);
            return Results.Ok(results);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Rate limit"))
        {
            logger.LogWarning(ex, "BGG API rate limit exceeded for wizard search");
            return Results.StatusCode(StatusCodes.Status429TooManyRequests);
        }
    }

    /// <summary>
    /// Handler for wizard BGG game details endpoint.
    /// Fetches detailed game information from BoardGameGeek API.
    /// </summary>
    private static async Task<IResult> HandleWizardBggDetails(
        int bggId,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        if (bggId <= 0)
        {
            return Results.BadRequest(new { error = "validation_failed", details = new Dictionary<string, string>(StringComparer.Ordinal) { ["bggId"] = "BGG ID must be a positive integer" } });
        }

        logger.LogInformation("Fetching wizard BGG details: BggId={BggId}", bggId);

        var query = new GetBggGameDetailsQuery(bggId);
        var details = await mediator.Send(query, ct).ConfigureAwait(false);

        if (details == null)
        {
            logger.LogWarning("BGG game not found: BggId={BggId}", bggId);
            return Results.NotFound(new { error = "BGG game not found", bggId });
        }

        return Results.Ok(details);
    }

    /// <summary>
    /// Handler for wizard game creation endpoint (final step).
    /// Creates SharedGame from extracted metadata with optional BGG enrichment.
    /// </summary>
    private static async Task<IResult> HandleWizardCreateGame(
        CreateGameFromPdfRequest request,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var userId = context.User.GetUserId();

        // Determine if user is Admin (auto-publish) or Editor (requires approval)
        var isAdmin = context.User.IsInRole("Admin");
        var requiresApproval = !isAdmin; // Editors require approval, Admins auto-publish

        logger.LogInformation(
            "Wizard create game: PdfId={PdfId}, Title='{Title}', BggId={BggId}, UserId={UserId}, RequiresApproval={RequiresApproval}",
            request.PdfDocumentId, request.ExtractedTitle, request.SelectedBggId, userId, requiresApproval);

        var command = new CreateSharedGameFromPdfCommand(
            request.PdfDocumentId,
            userId,
            request.ExtractedTitle,
            request.MinPlayers,
            request.MaxPlayers,
            request.PlayingTimeMinutes,
            request.MinAge,
            request.SelectedBggId,
            requiresApproval);

        try
        {
            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogInformation(
                "Wizard game created successfully: GameId={GameId}, Status={Status}, BggEnriched={BggEnriched}",
                result.GameId, result.ApprovalStatus, result.BggEnrichmentApplied);

            return Results.Created($"/api/v1/admin/shared-games/{result.GameId}", result);
        }
        catch (ConflictException ex)
        {
            logger.LogWarning(ex, "Wizard create game conflict: {Message}", ex.Message);
            return Results.Conflict(new { error = ex.Message });
        }
    }
}

// ========================================
// REQUEST DTOS
// ========================================

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
    int? BggId);

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