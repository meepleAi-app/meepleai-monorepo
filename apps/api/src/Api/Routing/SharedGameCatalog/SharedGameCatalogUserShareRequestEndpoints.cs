using Api.BoundedContexts.SharedGameCatalog.Application;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetShareRequestDetails;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetUserShareRequests;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Authenticated user share request endpoints (Issue #2733).
/// </summary>
internal static class SharedGameCatalogUserShareRequestEndpoints
{
    public static void Map(RouteGroupBuilder group)
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
}
