using Api.BoundedContexts.Administration.Application.Commands;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Miscellaneous admin endpoints.
/// Handles seed data creation and other administrative utilities.
/// </summary>
internal static class AdminMiscEndpoints
{
    public static RouteGroupBuilder MapAdminMiscEndpoints(this RouteGroupBuilder group)
    {
        // E2E Test User Seeding - Only for development/testing environments
        // SEC-02: Requires admin auth + Development environment guard
        group.MapPost("/seed-e2e-users", async (HttpContext context, IMediator mediator, ILogger<Program> logger, IWebHostEnvironment env, CancellationToken ct) =>
        {
            // Only allow in Development environment for security
            if (!env.IsDevelopment())
            {
                logger.LogWarning("Attempted to seed E2E test users in non-development environment");
                return Results.Forbid();
            }

            // SEC-02: Require admin authentication
            var (authorized, session, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            logger.LogInformation("Admin {UserId} seeding E2E test users", session!.User!.Id);

            await mediator.Send(new SeedE2ETestUsersCommand(), ct).ConfigureAwait(false);

            logger.LogInformation("E2E test users seeded successfully by admin {UserId}", session.User.Id);
            // SEC-02: Do not expose passwords in response
            return Results.Json(new
            {
                success = true,
                message = "E2E test users seeded: admin (from INITIAL_ADMIN_EMAIL), editor@meepleai.dev, user@meepleai.dev"
            });
        });

        return group;
    }
}
