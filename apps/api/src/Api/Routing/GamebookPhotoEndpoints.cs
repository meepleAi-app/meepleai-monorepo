using System.Security.Claims;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// Gamebook photo translate endpoints for Libro Game (Iter 1.B).
/// Provides photo upload, OCR segmentation, SSE translation, history, and glossary management.
/// </summary>
internal static class GamebookPhotoEndpoints
{
    private static readonly System.Text.Json.JsonSerializerOptions SseJsonOptions = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
    };

    public static RouteGroupBuilder MapGamebookPhotoEndpoints(this RouteGroupBuilder group)
    {
        MapUploadPhotoEndpoint(group);
        MapSegmentPhotoEndpoint(group);
        MapTranslateStreamEndpoint(group);
        MapGetHistoryEndpoint(group);
        MapGetGlossaryEndpoint(group);
        MapBootstrapGlossaryEndpoint(group);
        MapUpsertGlossaryEntryEndpoint(group);

        return group;
    }

    private static void MapUploadPhotoEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/gamebook/campaigns/{campaignId:guid}/photos", async (
            Guid campaignId,
            IFormFile file,
            [FromForm] Guid gameBookId,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;
            if (!TryGetUserId(context, session, out var userId)) return Results.Unauthorized();
            if (file is null || file.Length == 0)
                return Results.BadRequest(new { error = "file is required" });
            if (gameBookId == Guid.Empty)
                return Results.BadRequest(new { error = "gameBookId is required" });

            var stream = file.OpenReadStream();
            await using (stream.ConfigureAwait(false))
            {
                var dto = await mediator.Send(
                    new UploadGamebookPhotoCommand(campaignId, gameBookId, userId, stream, file.ContentType),
                    ct).ConfigureAwait(false);

                return Results.Created($"/api/v1/gamebook/campaigns/{campaignId}/photos/{dto.Id}", dto);
            }
        })
        .RequireAuthenticatedUser()
        .DisableAntiforgery()
        .Produces<GamebookPhotoArtifactDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound)
        .WithTags("Gamebook")
        .WithSummary("Upload a photo of a gamebook page")
        .WithDescription("Uploads a photo for OCR processing. EXIF metadata is stripped server-side. Photo expires 24h after upload.")
        .WithOpenApi();
    }

    private static void MapSegmentPhotoEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/gamebook/campaigns/{campaignId:guid}/photos/{photoId:guid}/segment", async (
            Guid campaignId,
            Guid photoId,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;
            if (!TryGetUserId(context, session, out var userId)) return Results.Unauthorized();

            var dto = await mediator.Send(
                new SegmentGamebookPhotoCommand(campaignId, photoId, userId),
                ct).ConfigureAwait(false);

            return Results.Ok(dto);
        })
        .RequireAuthenticatedUser()
        .Produces<GamebookPhotoArtifactDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound)
        .WithTags("Gamebook")
        .WithSummary("Trigger OCR segmentation on an uploaded photo")
        .WithDescription("Runs OCR on the uploaded photo and extracts numbered paragraph segments. Idempotent — returns existing artifact if already segmented.")
        .WithOpenApi();
    }

    private static void MapTranslateStreamEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/gamebook/campaigns/{campaignId:guid}/photos/translate", async (
            Guid campaignId,
            [FromQuery] Guid photoId,
            [FromQuery] int paragraphNumber,
            [FromQuery] Guid gameBookId,
            IMediator mediator,
            HttpContext context,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;
            if (!TryGetUserId(context, session, out var userId)) return Results.Unauthorized();

            context.Response.Headers["Content-Type"] = "text/event-stream";
            context.Response.Headers["Cache-Control"] = "no-cache";
            context.Response.Headers["Connection"] = "keep-alive";

            // C2 (2026-05-19): gameBookId scopes the per-book progress update.
            var query = new TranslateGamebookSegmentQuery(campaignId, photoId, paragraphNumber, userId, gameBookId);

            try
            {
                await foreach (var chunk in mediator.CreateStream(query, ct).ConfigureAwait(false))
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(chunk, SseJsonOptions);
                    await context.Response.WriteAsync($"data: {json}\n\n", ct).ConfigureAwait(false);
                    await context.Response.Body.FlushAsync(ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException ex)
            {
                logger.LogInformation(ex,
                    "Gamebook translate stream cancelled for campaign {CampaignId} paragraph {Paragraph}",
                    campaignId, paragraphNumber);
            }
#pragma warning disable CA1031
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in gamebook translate stream campaign {CampaignId}", campaignId);
                try
                {
                    var errorJson = System.Text.Json.JsonSerializer.Serialize(
                        new { error = ex.Message, code = "INTERNAL_ERROR" }, SseJsonOptions);
                    await context.Response.WriteAsync($"data: {errorJson}\n\n", ct).ConfigureAwait(false);
                    await context.Response.Body.FlushAsync(ct).ConfigureAwait(false);
                }
                catch { /* client disconnected */ }
            }
#pragma warning restore CA1031

            return Results.Empty;
        })
        .RequireAuthenticatedUser()
        .WithTags("Gamebook")
        .WithSummary("Stream paragraph translation as SSE")
        .WithDescription("Server-Sent Events stream of TranslateChunk events. Must be GET for EventSource compatibility. Pass photoId and paragraphNumber as query params. Final chunk has IsComplete=true with ParagraphId.")
        .WithOpenApi();
    }

    private static void MapGetHistoryEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/gamebook/campaigns/{campaignId:guid}/history", async (
            Guid campaignId,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;
            if (!TryGetUserId(context, session, out var userId)) return Results.Unauthorized();

            var result = await mediator.Send(
                new GetGamebookHistoryQuery(campaignId, userId), ct).ConfigureAwait(false);

            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .Produces<IReadOnlyList<TranslatedParagraphDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound)
        .WithTags("Gamebook")
        .WithSummary("List translated paragraphs for a campaign")
        .WithDescription("Returns all translated paragraphs for the given campaign, ordered by creation date ascending.")
        .WithOpenApi();
    }

    private static void MapGetGlossaryEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/gamebook/campaigns/{campaignId:guid}/glossary", async (
            Guid campaignId,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;
            if (!TryGetUserId(context, session, out var userId)) return Results.Unauthorized();

            var result = await mediator.Send(
                new GetGamebookGlossaryQuery(campaignId, userId), ct).ConfigureAwait(false);

            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .Produces<IReadOnlyList<GamebookGlossaryEntryDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound)
        .WithTags("Gamebook")
        .WithSummary("List glossary entries for a campaign")
        .WithDescription("Returns all EN→IT glossary entries for the given campaign. Entries may be auto-bootstrapped or manually edited.")
        .WithOpenApi();
    }

    private static void MapBootstrapGlossaryEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/gamebook/campaigns/{campaignId:guid}/glossary/bootstrap", async (
            Guid campaignId,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;
            if (!TryGetUserId(context, session, out var userId)) return Results.Unauthorized();

            var result = await mediator.Send(
                new BootstrapGamebookGlossaryCommand(campaignId, userId), ct).ConfigureAwait(false);

            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .Produces<IReadOnlyList<GamebookGlossaryEntryDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound)
        .WithTags("Gamebook")
        .WithSummary("Bootstrap glossary entries via LLM for a campaign")
        .WithDescription("Triggers LLM-based glossary generation for the campaign's game. Idempotent — returns existing entries if already bootstrapped.")
        .WithOpenApi();
    }

    private static void MapUpsertGlossaryEntryEndpoint(RouteGroupBuilder group)
    {
        group.MapPut("/gamebook/campaigns/{campaignId:guid}/glossary/{entryId:guid}", async (
            Guid campaignId,
            Guid entryId,
            [FromBody] UpsertGlossaryEntryRequest body,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;
            if (!TryGetUserId(context, session, out var userId)) return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(body.TermEn))
                return Results.BadRequest(new { error = "termEn is required" });
            if (string.IsNullOrWhiteSpace(body.TermIt))
                return Results.BadRequest(new { error = "termIt is required" });

            var dto = await mediator.Send(
                new UpsertGlossaryEntryCommand(campaignId, entryId, body.TermEn, body.TermIt, userId),
                ct).ConfigureAwait(false);

            return Results.Ok(dto);
        })
        .RequireAuthenticatedUser()
        .Produces<GamebookGlossaryEntryDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound)
        .WithTags("Gamebook")
        .WithSummary("Create or update a glossary entry")
        .WithDescription("If the entry with the given id does not exist, creates a new manual entry. If it exists, updates the Italian translation and sets Source to Manual. Note: TermEn is immutable on update; to rename a term, delete and recreate.")
        .WithOpenApi();
    }

    private static bool TryGetUserId(HttpContext context, SessionStatusDto? session, out Guid userId)
    {
        userId = Guid.Empty;
        if (session != null)
        {
            userId = session.User!.Id;
            return true;
        }

        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out userId))
        {
            return true;
        }

        return false;
    }
}

/// <summary>Request body for upserting a glossary entry.</summary>
public sealed record UpsertGlossaryEntryRequest(string TermEn, string TermIt);
