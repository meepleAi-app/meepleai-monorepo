using Api.BoundedContexts.SharedGameCatalog.Application;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands.ApproveGameProposal;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.CheckPrivateGameDuplicates;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetMyActiveReviews;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetPendingShareRequests;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetShareRequestDetails;
using Api.BoundedContexts.SharedGameCatalog.Domain.Exceptions;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Extensions;
using Api.Middleware.Exceptions;
using Api.Models;
using Api.SharedKernel.Application.DTOs;
using Api.SharedKernel.Domain.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Admin-only share request review endpoints (Issue #2734).
/// </summary>
internal static class SharedGameCatalogAdminShareRequestEndpoints
{
    public static void Map(RouteGroupBuilder group)
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
}
