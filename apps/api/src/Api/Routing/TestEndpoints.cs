using Api.BoundedContexts.Administration.Application.Commands;
using Api.Extensions;
using Api.Helpers;
using Api.Middleware;
using Api.Middleware.Exceptions;
using MediatR;

#pragma warning disable MA0048 // File name must match type name - Contains Interface with supporting types
namespace Api.Routing;

/// <summary>
/// Test endpoints for runbook validation and Prometheus alert testing.
/// Issue #2004: Enable validation of high-error-rate.md and error-spike.md runbooks.
///
/// Security:
/// - Admin-only access via RequireAdminSession()
/// - Disabled in production via TestEndpoints:Enabled config (default: false)
/// - Rate limiting applied (Admin: 1000 tokens, 10/sec refill)
///
/// Usage:
/// - high-error-rate.md: Burst 200 POST /api/v1/test/error in 120 seconds
/// - error-spike.md: Burst 200 POST /api/v1/test/error, validate 3x baseline increase
/// </summary>
internal static class TestEndpoints
{
    public static RouteGroupBuilder MapTestEndpoints(this RouteGroupBuilder group)
    {
        // POST /api/v1/test/error - Simulate error for testing (admin-only)
        group.MapPost("/test/error", async (
            HttpContext context,
            IMediator mediator,
            SimulateErrorRequest request,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            // Admin-only validation
            var sessionResult = context.RequireAdminSession();
            if (!sessionResult.IsAuthorized) return sessionResult.ErrorResult!;
            var session = sessionResult.Session;

            logger.LogInformation("Admin {UserId} simulating error type: {ErrorType}",
                session.User!.Id, LogSanitizer.Sanitize(request.ErrorType));

            try
            {
                // Handler will throw appropriate exception based on ErrorType
                await mediator.Send(new SimulateErrorCommand(request.ErrorType), ct).ConfigureAwait(false);

                // Should never reach here (handler always throws)
                return Results.StatusCode(StatusCodes.Status500InternalServerError);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("disabled"))
            {
                // TestEndpoints not enabled
                logger.LogWarning(ex, "Test endpoints disabled: {Message}", ex.Message);
                return Results.Problem(
                    title: "Test endpoints disabled",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status403Forbidden
                );
            }
            catch (ArgumentException ex)
            {
                // Invalid error type
                logger.LogWarning(ex, "Invalid error type: {ErrorType}", LogSanitizer.Sanitize(request.ErrorType));
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                // Expected simulated errors - log for Prometheus
                logger.LogError(ex, "Simulated error: {ErrorType}", LogSanitizer.Sanitize(request.ErrorType));

                // Return appropriate status code
                if (string.Equals(request.ErrorType, "500", StringComparison.OrdinalIgnoreCase))
                {
                    return Results.Problem(
                        title: "Simulated Internal Server Error",
                        detail: ex.Message,
                        statusCode: StatusCodes.Status500InternalServerError
                    );
                }
                else if (string.Equals(request.ErrorType, "400", StringComparison.OrdinalIgnoreCase))
                {
                    return Results.BadRequest(new { error = ex.Message });
                }
                else if (string.Equals(request.ErrorType, "timeout", StringComparison.OrdinalIgnoreCase))
                {
                    return Results.Problem(
                        title: "Simulated Timeout",
                        detail: ex.Message,
                        statusCode: StatusCodes.Status504GatewayTimeout
                    );
                }
                else if (string.Equals(request.ErrorType, "exception", StringComparison.OrdinalIgnoreCase))
                {
                    return Results.Problem(
                        title: "Simulated Exception",
                        detail: ex.Message,
                        statusCode: StatusCodes.Status500InternalServerError
                    );
                }
                else
                {
                    return Results.Problem(
                        title: "Simulated Error",
                        detail: ex.Message,
                        statusCode: StatusCodes.Status500InternalServerError
                    );
                }
            }
        })
        .WithName("SimulateError")
        .WithTags("Testing", "Admin")
        .WithDescription("Simulate error for runbook validation (admin-only, dev/staging only)")
        .Produces(StatusCodes.Status500InternalServerError)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status429TooManyRequests)
        .Produces(StatusCodes.Status504GatewayTimeout);

        // POST /api/v1/test/reset-smoke-aaron - Wipe smoke-aaron persona data (any authenticated user, dev/staging only).
        // Issue #943 — Bruno smoke pre-request hook. See ResetSmokeAaronCommandHandler for the triple-gate.
        //
        // Why session-only (not admin-only): the triple-gate in the handler is already
        // strong enough — production blocks via IsProduction() check independent of
        // the config flag, and the target user is hardcoded (cannot be supplied by
        // the caller). Requiring admin would force the Bruno smoke collection to
        // maintain admin credentials separately from the smoke-aaron free-tier
        // persona it is actually testing. Self-reset is acceptable here.
        group.MapPost("/test/reset-smoke-aaron", async (
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            logger.LogWarning(
                "User {UserId} invoking smoke-aaron reset",
                session!.User!.Id);

            try
            {
                var result = await mediator.Send(new ResetSmokeAaronCommand(), ct).ConfigureAwait(false);
                return Results.Ok(result);
            }
            catch (ForbiddenException ex)
            {
                logger.LogWarning(ex, "Smoke-aaron reset denied: {Reason}", ex.Message);
                return Results.Problem(
                    title: "Reset denied",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status403Forbidden);
            }
        })
        .RequireSession()
        .RequireRateLimiting("Admin")
        .WithName("ResetSmokeAaron")
        .WithTags("Testing")
        .WithDescription("Wipe smoke-aaron's transactional data so Bruno smoke runs from an empty state. Any authenticated user, dev/staging only — triple-gate enforced in handler.")
        .Produces<ResetSmokeAaronResult>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);

        return group;
    }
}

/// <summary>
/// Request model for SimulateError endpoint.
/// </summary>
/// <param name="ErrorType">Type of error to simulate: "500", "400", "timeout", "exception"</param>
internal record SimulateErrorRequest(string ErrorType);
