using System.Security.Claims;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.BoundedContexts.SessionTracking.Application.Services;
using Api.Extensions;
using Api.Middleware.Exceptions;
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
        MapEncounterParseEndpoint(group);

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
        .Produces(StatusCodes.Status403Forbidden)
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
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .WithTags("Gamebook")
        .WithSummary("Trigger OCR segmentation on an uploaded photo")
        .WithDescription("Runs OCR on the uploaded photo and extracts numbered paragraph segments. Idempotent — returns existing artifact if already segmented.")
        .WithOpenApi();
    }

    /// <summary>
    /// SSE endpoint for streaming paragraph translations. Issue #1415: implements a
    /// 5-branch exception priority chain that translates domain exceptions into either
    /// proper HTTP status codes (when headers have not yet been flushed) or typed SSE
    /// events with explicit <c>code</c> fields (when headers have already been flushed).
    /// </summary>
    /// <remarks>
    /// Branch priority (any new domain exception added to TranslateGamebookSegmentQueryHandler
    /// MUST be classified explicitly here — do NOT rely on the fallback <c>catch (Exception)</c>,
    /// which exists only as a safety net for genuinely unknown errors):
    /// <list type="number">
    /// <item><c>OperationCanceledException</c> (client disconnect) → log Info, silent return.</item>
    /// <item><c>ForbiddenException</c> pre-flush → rethrow → middleware HTTP 403.</item>
    /// <item><c>NotFoundException</c> pre-flush → rethrow → middleware HTTP 404.</item>
    /// <item>Same exceptions post-flush → emit typed SSE event <c>{code:"FORBIDDEN" | "NOT_FOUND"}</c>.</item>
    /// <item>Unknown <c>Exception</c> → log Error, emit <c>{code:"INTERNAL_ERROR"}</c>.</item>
    /// </list>
    /// <para>
    /// Pre-flight ownership check via <see cref="ICampaignOwnershipGuard"/> runs BEFORE any
    /// SSE header is written so that 401/403/404/504 (preflight DB timeout) can be returned
    /// as proper HTTP errors by <c>ApiExceptionHandlerMiddleware</c>.
    /// </para>
    /// </remarks>
    private static void MapTranslateStreamEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/gamebook/campaigns/{campaignId:guid}/photos/translate", async (
            Guid campaignId,
            [FromQuery] Guid photoId,
            [FromQuery] int paragraphNumber,
            [FromQuery] Guid gameBookId,
            IMediator mediator,
            ICampaignOwnershipGuard ownershipGuard,
            HttpContext context,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;
            if (!TryGetUserId(context, session, out var userId)) return Results.Unauthorized();

            // Issue #1415: pre-flight ownership check BEFORE writing SSE headers so middleware
            // can translate ForbiddenException/NotFoundException into proper HTTP 403/404.
            // TimeoutException (DB preflight timeout) → HTTP 504 by middleware default mapping.
            await ownershipGuard.AssertOwnedByAsync(campaignId, userId, ct).ConfigureAwait(false);

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
                // Branch 1: client disconnect — log Info, no metric increment
                logger.LogInformation(ex,
                    "Gamebook translate stream cancelled for campaign {CampaignId} paragraph {Paragraph}",
                    campaignId, paragraphNumber);
            }
            catch (ForbiddenException) when (!context.Response.HasStarted)
            {
                // Branch 2: pre-flush — let middleware → HTTP 403
                throw;
            }
            catch (NotFoundException) when (!context.Response.HasStarted)
            {
                // Branch 3: pre-flush — let middleware → HTTP 404
                throw;
            }
            catch (ForbiddenException ex)
            {
                // Branch 4a: post-flush — emit typed SSE event
                logger.LogWarning(ex,
                    "Forbidden mid-stream for campaign {CampaignId} (headers already flushed)",
                    campaignId);
                await EmitSseErrorAsync(context, code: "FORBIDDEN", message: ex.Message).ConfigureAwait(false);
            }
            catch (NotFoundException ex)
            {
                // Branch 4b: post-flush — emit typed SSE event
                logger.LogWarning(ex,
                    "NotFound mid-stream for campaign {CampaignId} (headers already flushed)",
                    campaignId);
                await EmitSseErrorAsync(context, code: "NOT_FOUND", message: ex.Message).ConfigureAwait(false);
            }
#pragma warning disable CA1031
            catch (Exception ex)
            {
                // Branch 5: unknown — log Error, preserve current SSE error event shape
                logger.LogError(ex, "Error in gamebook translate stream campaign {CampaignId}", campaignId);
                await EmitSseErrorAsync(context, code: "INTERNAL_ERROR", message: ex.Message).ConfigureAwait(false);
            }
#pragma warning restore CA1031

            return Results.Empty;
        })
        .RequireAuthenticatedUser()
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status504GatewayTimeout)
        .WithTags("Gamebook")
        .WithSummary("Stream paragraph translation as SSE")
        .WithDescription("Server-Sent Events stream of TranslateChunk events. Must be GET for EventSource compatibility. Pass photoId and paragraphNumber as query params. Final chunk has IsComplete=true with ParagraphId. Non-owner callers receive HTTP 403 BEFORE the stream opens (Issue #1415).")
        .WithOpenApi();
    }

    /// <summary>
    /// Emits a typed SSE error event with the shape <c>{ error, code }</c> and flushes.
    /// Issue #1415: used by Branch 4a/4b/5 of <see cref="MapTranslateStreamEndpoint"/>
    /// when an exception is caught AFTER the SSE headers have been flushed (so the
    /// middleware can no longer rewrite the HTTP status code). Swallows any nested
    /// I/O exception that arises from the client having disconnected mid-write.
    /// </summary>
    /// <remarks>
    /// Intentionally uses <see cref="CancellationToken.None"/> for the write: we are
    /// already in an error path emitted as a best-effort to the client; honoring the
    /// caller's (possibly cancelled) token would throw <see cref="OperationCanceledException"/>
    /// before any byte is written, which would be swallowed by the bare <c>catch</c>
    /// below and silently drop the error event. Best-effort write semantics give the
    /// client a chance to see the error code if its connection is still alive.
    /// </remarks>
    private static async Task EmitSseErrorAsync(HttpContext context, string code, string message)
    {
        try
        {
            var errorJson = System.Text.Json.JsonSerializer.Serialize(
                new { error = message, code }, SseJsonOptions);
            await context.Response.WriteAsync($"data: {errorJson}\n\n", CancellationToken.None).ConfigureAwait(false);
            await context.Response.Body.FlushAsync(CancellationToken.None).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch
        {
            // client already disconnected — nothing to do
        }
#pragma warning restore CA1031
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
        .Produces(StatusCodes.Status403Forbidden)
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
        .Produces(StatusCodes.Status403Forbidden)
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
        .Produces(StatusCodes.Status403Forbidden)
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
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .WithTags("Gamebook")
        .WithSummary("Create or update a glossary entry")
        .WithDescription("If the entry with the given id does not exist, creates a new manual entry. If it exists, updates the Italian translation and sets Source to Manual. Note: TermEn is immutable on update; to rename a term, delete and recreate.")
        .WithOpenApi();
    }

    /// <summary>
    /// Issue #1520 — synchronous, ephemeral encounter cheatsheet parse.
    /// Unlike the SSE translate flow, this returns a fully-formed
    /// <see cref="EncounterCheatsheetDto"/> in a single response and persists
    /// nothing (per §9.1 Encounter Book privacy: card is session-scoped only).
    /// </summary>
    private static void MapEncounterParseEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/gamebook/campaigns/{campaignId:guid}/photos/{photoId:guid}/encounter-parse", async (
            Guid campaignId,
            Guid photoId,
            [FromBody] ParseEncounterRequest body,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;
            if (!TryGetUserId(context, session, out var userId)) return Results.Unauthorized();

            if (body is null)
                return Results.BadRequest(new { error = "request body is required" });
            if (body.ParagraphNumber <= 0)
                return Results.BadRequest(new { error = "paragraphNumber must be a positive integer" });
            if (body.GameBookId == Guid.Empty)
                return Results.BadRequest(new { error = "gameBookId is required" });

            var dto = await mediator.Send(
                new ParseEncounterQuery(campaignId, photoId, body.ParagraphNumber, userId, body.GameBookId),
                ct).ConfigureAwait(false);

            return Results.Ok(dto);
        })
        .RequireAuthenticatedUser()
        .Produces<EncounterCheatsheetDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict)
        .WithTags("Gamebook")
        .WithSummary("Parse an encounter cheatsheet from a gamebook photo segment")
        .WithDescription(
            "Extracts a structured cheatsheet (enemies, options, win/loss conditions) " +
            "from a previously segmented photo's paragraph. Synchronous, ephemeral — " +
            "no server-side persistence per Encounter Book privacy (§9.1). Returns 409 " +
            "if the LLM cannot extract a usable structured payload.")
        .WithOpenApi();
    }

    private static bool TryGetUserId(HttpContext context, SessionStatusDto? session, out Guid userId)
    {
        userId = Guid.Empty;
        if (session != null)
        {
            userId = session.Principal!.Subject.Id;
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
