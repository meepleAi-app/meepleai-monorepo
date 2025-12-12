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
        // Game-scoped endpoints (create and list by game)
        var gameCollectionGroup = group.MapGroup("/games/{gameId:guid}/document-collections")
            .WithTags("DocumentCollections")
            .WithOpenApi();

        // Create a new document collection
        gameCollectionGroup.MapPost("", async (
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

            return Results.Created($"/api/v1/games/{gameId}/document-collections/{result.Id}", result);
        })
        .RequireSession()
        .WithName("CreateDocumentCollection")
        .WithDescription("Create a new document collection for a game with optional initial documents");

        // Get collection by game ID
        gameCollectionGroup.MapGet("", async (
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

        // Collection-scoped endpoints (operations on existing collections)
        var collectionGroup = group.MapGroup("/games/{gameId:guid}/document-collections")
            .WithTags("DocumentCollections")
            .WithOpenApi();

        // Get collection by ID (within game scope)
        collectionGroup.MapGet("/{collectionId:guid}", async (
            Guid gameId,
            Guid collectionId,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            logger.LogInformation("User {UserId} retrieving collection {CollectionId} for game {GameId}",
                session!.User!.Id, collectionId, gameId);

            var result = await mediator.Send(new GetCollectionByIdQuery(collectionId), ct)
                .ConfigureAwait(false);

            if (result == null)
            {
                logger.LogWarning("Collection {CollectionId} not found for game {GameId}", collectionId, gameId);
                return Results.NotFound(new { error = "Collection not found" });
            }

            // Verify collection belongs to specified game
            if (result.GameId != gameId)
            {
                logger.LogWarning(
                    "Collection {CollectionId} does not belong to game {GameId}",
                    collectionId, gameId);
                return Results.NotFound(new { error = "Collection not found for this game" });
            }

            return Results.Ok(result);
        })
        .RequireSession()
        .WithName("GetCollectionById")
        .WithDescription("Get a document collection by its ID within a game scope");

        // Get all collections by user ID (admin/separate group)
        var userCollectionGroup = group.MapGroup("/document-collections/by-user")
            .WithTags("DocumentCollections")
            .WithOpenApi();

        userCollectionGroup.MapGet("/{userId:guid}", async (
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
            Guid gameId,
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
                request.SortOrder,
                session!.User!.Id); // Authorization: pass current user ID

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
        collectionGroup.MapDelete("/{collectionId:guid}/documents/{documentId:guid}", async (
            Guid gameId,
            Guid collectionId,
            Guid documentId,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            logger.LogInformation(
                "User {UserId} removing document {DocumentId} from collection {CollectionId}",
                session!.User!.Id, documentId, collectionId);

            var command = new RemoveDocumentFromCollectionCommand(collectionId, documentId);

            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            if (!result)
            {
                logger.LogWarning(
                    "Failed to remove document {DocumentId} from collection {CollectionId}",
                    documentId, collectionId);
                return Results.BadRequest(new { error = "Failed to remove document from collection" });
            }

            logger.LogInformation(
                "Document {DocumentId} successfully removed from collection {CollectionId}",
                documentId, collectionId);

            return Results.NoContent();
        })
        .RequireSession()
        .WithName("RemoveDocumentFromCollection")
        .WithDescription("Remove a PDF document from a collection");

        return group;
    }
}
