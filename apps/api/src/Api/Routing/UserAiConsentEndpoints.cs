using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

#pragma warning disable MA0048 // File name must match type name - Contains supporting types
namespace Api.Routing;

/// <summary>
/// User AI consent management endpoints (Issue #5512 — GDPR compliance)
/// </summary>
internal static class UserAiConsentEndpoints
{
    public static RouteGroupBuilder MapUserAiConsentEndpoints(this RouteGroupBuilder group)
    {
        MapGetAiConsent(group);
        MapUpdateAiConsent(group);

        return group;
    }

    private static void MapGetAiConsent(RouteGroupBuilder group)
    {
        group.MapGet("/users/me/ai-consent", async (
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;
            var query = new GetUserAiConsentQuery(session!.User!.Id);
            var consent = await mediator.Send(query, ct).ConfigureAwait(false);

            // Return default state if no consent record exists yet
            if (consent is null)
            {
                return Results.Json(new UserAiConsentDto(
                    session.User.Id,
                    ConsentedToAiProcessing: false,
                    ConsentedToExternalProviders: false,
                    ConsentedAt: DateTime.MinValue,
                    ConsentVersion: string.Empty));
            }

            return Results.Json(consent);
        })
        .RequireSession()
        .RequireAuthorization()
        .WithName("GetUserAiConsent")
        .WithTags("User Profile", "AI Consent")
        .WithSummary("Get current user's AI consent preferences")
        .WithDescription(@"Returns AI consent status for the authenticated user.

**GDPR Art. 6/13**: Tracks legal basis for AI data processing.

**Response**: UserAiConsentDto with consent flags, timestamp, and policy version.")
        .Produces(200)
        .Produces(401);
    }

    private static void MapUpdateAiConsent(RouteGroupBuilder group)
    {
        group.MapPut("/users/me/ai-consent", async (
            [FromBody] UpdateAiConsentPayload payload,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;

            var command = new UpdateUserAiConsentCommand(
                UserId: session!.User!.Id,
                ConsentedToAiProcessing: payload.ConsentedToAiProcessing,
                ConsentedToExternalProviders: payload.ConsentedToExternalProviders,
                ConsentVersion: payload.ConsentVersion);

            await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogInformation("AI consent updated for user {UserId}", session.User.Id);

            return Results.Json(new { ok = true, message = "AI consent preferences updated" });
        })
        .RequireSession()
        .RequireAuthorization()
        .WithName("UpdateUserAiConsent")
        .WithTags("User Profile", "AI Consent")
        .WithSummary("Update current user's AI consent preferences")
        .WithDescription(@"Creates or updates AI consent preferences for the authenticated user.

**GDPR Art. 6**: Records explicit consent for AI data processing.
**GDPR Art. 7**: Consent must be freely given, specific, informed, and unambiguous.

**Request Body**: UpdateAiConsentPayload with consent flags and policy version.

**Security**: Users can only update their own consent.")
        .Produces(200)
        .Produces(400)
        .Produces(401);
    }
}

/// <summary>
/// Payload for updating AI consent preferences
/// </summary>
internal record UpdateAiConsentPayload(
    bool ConsentedToAiProcessing,
    bool ConsentedToExternalProviders,
    string ConsentVersion);
