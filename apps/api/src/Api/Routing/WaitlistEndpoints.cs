using Api.BoundedContexts.Authentication.Application.Commands.Waitlist;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Public Alpha-program waitlist endpoint.
/// Spec §3.5 / §4.4 (2026-04-27-v2-migration-wave-a-2-join.md).
/// </summary>
internal static class WaitlistEndpoints
{
    public static RouteGroupBuilder MapWaitlistEndpoints(this RouteGroupBuilder group)
    {
        MapJoinWaitlistEndpoint(group);
        return group;
    }

    private static void MapJoinWaitlistEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/waitlist", async (
            JoinWaitlistPayload payload,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(
                new JoinWaitlistCommand(
                    payload.Email,
                    payload.Name,
                    payload.GamePreferenceId,
                    payload.GamePreferenceOther,
                    payload.NewsletterOptIn),
                ct).ConfigureAwait(false);

            if (result.IsAlreadyOnList)
            {
                return Results.Conflict(new
                {
                    error = "ALREADY_ON_LIST",
                    position = result.Position,
                    estimatedWeeks = result.EstimatedWeeks
                });
            }

            return Results.Ok(new
            {
                position = result.Position,
                estimatedWeeks = result.EstimatedWeeks
            });
        }).WithName("JoinWaitlist")
          .RequireRateLimiting("AccessRequest");
    }
}

/// <summary>
/// Request body for <c>POST /api/v1/waitlist</c>.
/// <c>NewsletterOptIn</c> defaults to <c>false</c> when omitted (GDPR Art. 7 — explicit opt-in).
/// </summary>
internal record JoinWaitlistPayload(
    string Email,
    string? Name,
    string GamePreferenceId,
    string? GamePreferenceOther,
    bool NewsletterOptIn = false);
