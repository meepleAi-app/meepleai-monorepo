using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Application.Queries.Onboarding;
using Api.Extensions;
using Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

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
            MeepleAiDbContext dbContext,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;
            var userId = session!.User!.Id;
            // Direct update to avoid IUnitOfWork scope mismatch (SaveChanges returned 0 via MediatR handlers)
            var rows = await dbContext.Users
                .Where(u => u.Id == userId && u.OnboardingWizardSeenAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.OnboardingWizardSeenAt, DateTime.UtcNow), ct)
                .ConfigureAwait(false);
            logger.LogInformation("Onboarding wizard marked as seen for user {UserId}, rows={Rows}", userId, rows);
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
            MeepleAiDbContext dbContext,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var session = (SessionStatusDto)context.Items[nameof(SessionStatusDto)]!;
            var userId = session!.User!.Id;
            // Direct update to avoid IUnitOfWork scope mismatch (SaveChanges returned 0 via MediatR handlers)
            var rows = await dbContext.Users
                .Where(u => u.Id == userId && u.OnboardingDismissedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.OnboardingDismissedAt, DateTime.UtcNow), ct)
                .ConfigureAwait(false);
            logger.LogInformation("Onboarding dismissed for user {UserId}, rows={Rows}", userId, rows);
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
