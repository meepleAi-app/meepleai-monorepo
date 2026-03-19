using Api.BoundedContexts.SharedGameCatalog.Application;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands.AddRagToSharedGame;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands.RemoveRagFromSharedGame;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetGameRagReadiness;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetSharedGameDocuments;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Extensions;
using Api.Middleware.Exceptions;
using Api.Models;
using Api.SharedKernel.Domain.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Admin/Editor SharedGameCatalog management endpoints.
/// </summary>
internal static class SharedGameCatalogAdminEndpoints
{
#pragma warning disable MA0051 // Method is too long - endpoint registration methods are inherently long
    public static void Map(RouteGroupBuilder group)
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

        // Quick-publish (Draft → Published directly) - Issue #250
        group.MapPost("/admin/shared-games/{id:guid}/quick-publish", HandleQuickPublish)
            .RequireAuthorization("AdminOnlyPolicy")
            .WithName("QuickPublishSharedGame")
            .WithSummary("Quick-publish game directly from Draft to Published (Admin only)")
            .WithDescription("Combines submit and approve into a single operation. Only works for Draft games.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status400BadRequest);

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

        // Distinct metadata for autocomplete (categories, mechanics, designers, publishers)
        group.MapGet("/admin/shared-games/metadata/distinct", HandleGetDistinctMetadata)
            .RequireAuthorization("AdminOrEditorPolicy")
            .WithName("GetDistinctMetadata")
            .WithSummary("Get distinct categories, mechanics, designers, publishers (Admin/Editor)")
            .Produces<DistinctMetadataDto>();

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
            .WithName("GetSharedGameDocuments")
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

        group.MapDelete("/admin/shared-games/{id:guid}/documents/{documentId:guid}/full", HandleRemoveRagFromSharedGame)
            .RequireAuthorization("AdminPolicy")
            .WithName("RemoveRagFromSharedGame")
            .WithSummary("Remove document with full PDF cleanup (Admin only)")
            .WithDescription("Removes SharedGameDocument link and deletes PDF with cascade cleanup (VectorDoc, TextChunks, Qdrant, blob).")
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

        // RAG Readiness aggregation (cross-BC: SharedGameCatalog + DocumentProcessing + KnowledgeBase)
        group.MapGet("/admin/shared-games/{id:guid}/rag-readiness", HandleGetGameRagReadiness)
            .RequireAuthorization("AdminOrEditorPolicy")
            .WithName("GetGameRagReadiness")
            .WithSummary("Get RAG readiness status for a shared game (Admin/Editor)")
            .WithDescription("Aggregates document processing status and agent linkage across bounded contexts to determine if a game is RAG-ready.")
            .Produces<GameRagReadinessDto>()
            .Produces(StatusCodes.Status404NotFound);

        // Issue #119: Per-SharedGame Document Overview (enriched with PDF processing status)
        group.MapGet("/admin/shared-games/{gameId:guid}/documents/overview", HandleGetSharedGameDocuments)
            .RequireAuthorization("AdminOrEditorPolicy")
            .WithName("GetSharedGameDocumentsOverview")
            .WithSummary("Get document overview for a shared game with PDF status (Admin/Editor)")
            .WithDescription("Returns all documents associated with a shared game, enriched with PDF processing status from the document processing context.")
            .Produces<GetSharedGameDocumentsResult>(StatusCodes.Status200OK);
    }
#pragma warning restore MA0051

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
            request.BggId,
            request.Categories,
            request.Mechanics,
            request.Designers,
            request.Publishers);

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

    private static async Task<IResult> HandleQuickPublish(
        Guid id,
        IMediator mediator,
        HttpContext context,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var command = new QuickPublishSharedGameCommand(id, session!.User!.Id);

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
            return Results.BadRequest(new { error = ex.Message });
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
        return Results.Created($"/api/v1/shared-games/{gameId}", new { id = gameId });
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

    private static async Task<IResult> HandleGetDistinctMetadata(
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(new GetDistinctMetadataQuery(), ct).ConfigureAwait(false);
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

    private static async Task<IResult> HandleGetGameRagReadiness(
        Guid id,
        IMediator mediator,
        CancellationToken ct)
    {
        var query = new GetGameRagReadinessQuery(id);
        var result = await mediator.Send(query, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    /// <summary>
    /// Issue #119: Get all documents for a shared game with PDF processing status.
    /// </summary>
    private static async Task<IResult> HandleGetSharedGameDocuments(
        Guid gameId,
        IMediator mediator,
        CancellationToken ct)
    {
        var query = new GetSharedGameDocumentsQuery(gameId);
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

    private static async Task<IResult> HandleRemoveRagFromSharedGame(
        Guid id,
        Guid documentId,
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var command = new RemoveRagFromSharedGameCommand(id, documentId, session!.User!.Id);
        await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.NoContent();
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
}
