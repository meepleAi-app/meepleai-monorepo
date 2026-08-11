using Api.BoundedContexts.Authentication.Application.Commands;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Application.Queries;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Terms-of-Service acceptance endpoints (#2954 F1). Foundation only: records/reads
/// acceptance; no blocking gate is wired to needsReAcceptance in this scope.
/// </summary>
internal static class TermsConsentEndpoints
{
    public static RouteGroupBuilder MapTermsConsentEndpoints(this RouteGroupBuilder group)
    {
        MapGetTermsStatus(group);
        MapAcceptTerms(group);
        return group;
    }

    private static void MapGetTermsStatus(RouteGroupBuilder group)
    {
        group.MapGet("/users/me/terms/status", async (
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;
            var status = await mediator.Send(
                new GetTermsConsentStatusQuery(session.Principal!.Subject.Id), ct).ConfigureAwait(false);
            return Results.Json(status);
        })
        .RequireSession()
        .RequireAuthorization()
        .WithName("GetTermsConsentStatus")
        .WithTags("User Profile", "Terms")
        .WithSummary("Get current user's ToS acceptance status")
        .WithDescription(@"Returns the authenticated user's Terms-of-Service acceptance status (#2954 F1).

**Response**: TermsConsentStatusDto with the current server version, the user's last
accepted version (nullable), the acceptance timestamp, and a computed needsReAcceptance flag.")
        .Produces(200)
        .Produces(401);
    }

    private static void MapAcceptTerms(RouteGroupBuilder group)
    {
        group.MapPost("/users/me/terms/accept", async (
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;
            var status = await mediator.Send(new RecordTermsAcceptanceCommand(
                UserId: session.Principal!.Subject.Id,
                IpAddress: context.Connection.RemoteIpAddress?.ToString(),
                UserAgent: context.Request.Headers.UserAgent.ToString()), ct).ConfigureAwait(false);
            return Results.Json(status);
        })
        .RequireSession()
        .RequireAuthorization()
        .WithName("AcceptTerms")
        .WithTags("User Profile", "Terms")
        .WithSummary("Record acceptance of the current ToS version")
        .WithDescription(@"Records that the authenticated user accepted the current Terms-of-Service
version (#2954 F1). Idempotent: no new row is written when the user's latest accepted
version already equals the current one.")
        .Produces(200)
        .Produces(401);
    }
}
