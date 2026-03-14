using Api.BoundedContexts.Authentication.Application.Commands.Onboarding;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Application.Queries.Onboarding;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Onboarding endpoints for first-time user experience.
/// Provides status, wizard-seen, and dismiss operations.
/// </summary>
internal static class OnboardingEndpoints
{
    public static RouteGroupBuilder MapOnboardingEndpoints(this RouteGroupBuilder group)
    {
        // GET /api/v1/users/me/onboarding-status
        group.MapGet("/users/me/onboarding-status", async (
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;
            var query = new GetOnboardingStatusQuery(session!.User!.Id);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireSession()
        .WithName("GetOnboardingStatus")
        .WithTags("Onboarding")
        .WithSummary("Get onboarding status for current user")
        .WithDescription(@"Returns onboarding completion status including wizard/dismiss timestamps
and checklist step completion derived from real data (games, sessions, profile).")
        .Produces<OnboardingStatusResponse>(200)
        .Produces(401);

        // POST /api/v1/users/me/onboarding-wizard-seen
        group.MapPost("/users/me/onboarding-wizard-seen", async (
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;
            var command = new MarkOnboardingWizardSeenCommand(session!.User!.Id);
            await mediator.Send(command, ct).ConfigureAwait(false);
            logger.LogInformation("Onboarding wizard marked as seen for user {UserId}", session.User.Id);
            return Results.Json(new { ok = true });
        })
        .RequireSession()
        .WithName("MarkOnboardingWizardSeen")
        .WithTags("Onboarding")
        .WithSummary("Mark onboarding wizard as seen for current user")
        .WithDescription("Marks the onboarding wizard as seen. Idempotent — subsequent calls are no-ops.")
        .Produces(200)
        .Produces(401);

        // POST /api/v1/users/me/onboarding-dismiss
        group.MapPost("/users/me/onboarding-dismiss", async (
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;
            var command = new DismissOnboardingCommand(session!.User!.Id);
            await mediator.Send(command, ct).ConfigureAwait(false);
            logger.LogInformation("Onboarding dismissed for user {UserId}", session.User.Id);
            return Results.Json(new { ok = true });
        })
        .RequireSession()
        .WithName("DismissOnboarding")
        .WithTags("Onboarding")
        .WithSummary("Dismiss onboarding checklist for current user")
        .WithDescription("Dismisses the onboarding checklist permanently. Idempotent — subsequent calls are no-ops.")
        .Produces(200)
        .Produces(401);

        return group;
    }
}
