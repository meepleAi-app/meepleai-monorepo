using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.Extensions;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;

#pragma warning disable MA0048 // File name must match type name - Contains Interface with supporting types
namespace Api.Routing;

/// <summary>
/// Document collection management endpoints.
/// Handles creation, retrieval, and management of document collections for games.
/// Issue #2051: Multi-document collections
/// </summary>
public static class DocumentCollectionEndpoints
{
    public static RouteGroupBuilder MapDocumentCollectionEndpoints(this RouteGroupBuilder group)
    {
        var collectionGroup = group.MapGroup("/document-collections")
            .WithTags("DocumentCollections")
            .WithOpenApi();

        // Create a new document collection
        collectionGroup.MapPost("/{gameId:guid}", async (
            Guid gameId,
            HttpContext context,
            [FromBody] CreateCollectionRequest request,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            var userId = session!.User!.Id;

            logger.LogInformation(
                "User {UserId} creating document collection '{CollectionName}' for game {GameId}",
                userId, request.Name, gameId);

            var command = new CreateDocumentCollectionCommand(
                gameId,
                userId,
                request.Name,
                request.Description,
                request.InitialDocuments);

            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            if (result == null)
            {
                logger.LogWarning(
                    "Failed to create document collection '{CollectionName}' for game {GameId}",
                    request.Name, gameId);
                return Results.BadRequest(new { error = "Failed to create collection" });
            }

            logger.LogInformation(
                "Document collection '{CollectionName}' created successfully with ID {CollectionId}",
                request.Name, result.Id);

            return Results.Created($"/api/v1/document-collections/{result.Id}", result);
        })
        .RequireSession()
        .WithName("CreateDocumentCollection")
        .WithDescription("Create a new document collection for a game with optional initial documents");

        // Get collection by ID
        collectionGroup.MapGet("/{collectionId:guid}", async (
            Guid collectionId,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            logger.LogInformation("User {UserId} retrieving collection {CollectionId}",
                session!.User!.Id, collectionId);

            var result = await mediator.Send(new GetCollectionByIdQuery(collectionId), ct)
                .ConfigureAwait(false);

            if (result == null)
            {
                logger.LogWarning("Collection {CollectionId} not found", collectionId);
                return Results.NotFound(new { error = "Collection not found" });
            }

            return Results.Ok(result);
        })
        .RequireSession()
        .WithName("GetCollectionById")
        .WithDescription("Get a document collection by its ID");

        // Get collection by game ID
        collectionGroup.MapGet("/by-game/{gameId:guid}", async (
            Guid gameId,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            logger.LogInformation("User {UserId} retrieving collection for game {GameId}",
                session!.User!.Id, gameId);

            var result = await mediator.Send(new GetCollectionByGameQuery(gameId), ct)
                .ConfigureAwait(false);

            if (result == null)
            {
                logger.LogInformation("No collection found for game {GameId}", gameId);
                return Results.Ok(new { collection = (object?)null });
            }

            return Results.Ok(result);
        })
        .RequireSession()
        .WithName("GetCollectionByGame")
        .WithDescription("Get the document collection for a specific game");

        // Get all collections by user ID
        collectionGroup.MapGet("/by-user/{userId:guid}", async (
            Guid userId,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            // Authorization: Users can only view their own collections unless they are admin
            var currentUserId = session!.User!.Id;
            var isAdmin = string.Equals(session!.User!.Role, UserRole.Admin.ToString(),
                StringComparison.OrdinalIgnoreCase);

            if (currentUserId != userId && !isAdmin)
            {
                logger.LogWarning(
                    "User {UserId} denied access to collections for user {TargetUserId}",
                    currentUserId, userId);
                return Results.Forbid();
            }

            logger.LogInformation("Retrieving collections for user {UserId}", userId);

            var result = await mediator.Send(new GetCollectionsByUserQuery(userId), ct)
                .ConfigureAwait(false);

            return Results.Ok(new { collections = result });
        })
        .RequireSession()
        .WithName("GetCollectionsByUser")
        .WithDescription("Get all document collections created by a specific user");

        // Add document to collection
        collectionGroup.MapPost("/{collectionId:guid}/documents", async (
            Guid collectionId,
            HttpContext context,
            [FromBody] AddDocumentToCollectionRequest request,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            logger.LogInformation(
                "User {UserId} adding document {PdfId} to collection {CollectionId}",
                session!.User!.Id, request.PdfDocumentId, collectionId);

            var command = new AddDocumentToCollectionCommand(
                collectionId,
                request.PdfDocumentId,
                request.DocumentType,
                request.SortOrder);

            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            if (!result)
            {
                logger.LogWarning(
                    "Failed to add document {PdfId} to collection {CollectionId}",
                    request.PdfDocumentId, collectionId);
                return Results.BadRequest(new { error = "Failed to add document to collection" });
            }

            logger.LogInformation(
                "Document {PdfId} successfully added to collection {CollectionId}",
                request.PdfDocumentId, collectionId);

            return Results.Ok(new { success = true, message = "Document added to collection" });
        })
        .RequireSession()
        .WithName("AddDocumentToCollection")
        .WithDescription("Add a PDF document to an existing collection");

        // Remove document from collection
        collectionGroup.MapDelete("/{collectionId:guid}/documents/{pdfId:guid}", async (
            Guid collectionId,
            Guid pdfId,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            logger.LogInformation(
                "User {UserId} removing document {PdfId} from collection {CollectionId}",
                session!.User!.Id, pdfId, collectionId);

            var command = new RemoveDocumentFromCollectionCommand(collectionId, pdfId);

            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            if (!result)
            {
                logger.LogWarning(
                    "Failed to remove document {PdfId} from collection {CollectionId}",
                    pdfId, collectionId);
                return Results.BadRequest(new { error = "Failed to remove document from collection" });
            }

            logger.LogInformation(
                "Document {PdfId} successfully removed from collection {CollectionId}",
                pdfId, collectionId);

            return Results.NoContent();
        })
        .RequireSession()
        .WithName("RemoveDocumentFromCollection")
        .WithDescription("Remove a PDF document from a collection");

        return group;
    }
}
