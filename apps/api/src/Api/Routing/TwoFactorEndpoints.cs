using System.Security.Claims;
using Api.Extensions;
using Api.Helpers;
using Api.Middleware;
using Api.Middleware.Exceptions;
using Api.Models;
using Api.Services;
using MediatR;

// DDD CQRS imports
using GenerateTotpSetupCommand = Api.BoundedContexts.Authentication.Application.Commands.TwoFactor.GenerateTotpSetupCommand;
using Verify2FACommand = Api.BoundedContexts.Authentication.Application.Commands.TwoFactor.Verify2FACommand;
using AdminDisable2FACommand = Api.BoundedContexts.Authentication.Application.Commands.TwoFactor.AdminDisable2FACommand;
using DddCreateSessionCommand = Api.BoundedContexts.Authentication.Application.Commands.CreateSessionCommand;
using StepUpTwoFactorCommand = Api.BoundedContexts.Authentication.Application.Commands.TwoFactor.StepUpTwoFactorCommand;
using StepUpOutcome = Api.BoundedContexts.Authentication.Application.Commands.TwoFactor.StepUpOutcome;

namespace Api.Routing;

/// <summary>
/// Two-Factor Authentication endpoints.
/// Handles 2FA setup, enable, disable, verification, and status management.
/// </summary>
internal static class TwoFactorEndpoints
{
    // AUTH-07: Two-Factor Authentication endpoints
    public static RouteGroupBuilder MapTwoFactorEndpoints(this RouteGroupBuilder group)
    {

        // Setup and Enable flows
        MapTwoFactorSetupEndpoints(group);
        // Login verification
        MapTwoFactorVerificationEndpoints(group);
        // Self-management (Disable, Status)
        MapTwoFactorManagementEndpoints(group);
        // Admin overrides
        MapTwoFactorAdminEndpoints(group);

        return group;
    }

    private static void MapTwoFactorSetupEndpoints(RouteGroupBuilder group)
    {
        group.MapPost("/auth/2fa/setup", async (HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            var userIdStr = GetUserIdFromClaims(context.User);
            var userEmail = GetEmailFromClaims(context.User);

            if (string.IsNullOrEmpty(userIdStr) || string.IsNullOrEmpty(userEmail))
            {
                return Results.Unauthorized();
            }

            if (!Guid.TryParse(userIdStr, out var userId))
            {
                return Results.BadRequest(new { error = "invalid_user_id", message = "Invalid user ID format" });
            }

            // Execute TOTP setup generation via CQRS handler
            var command = new GenerateTotpSetupCommand
            {
                UserId = userId,
                UserEmail = userEmail
            };

            var setup = await mediator.Send(command, ct).ConfigureAwait(false);
            logger.LogInformation("2FA setup generated for user {UserId} via CQRS", userId);
            return Results.Ok(setup);
        })
        .RequireAuthorization()
        .WithName("Setup2FA")
        .WithTags("Authentication");

        group.MapPost("/auth/2fa/enable", async (TwoFactorEnableRequest request, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            var userIdStr = GetUserIdFromClaims(context.User);
            if (string.IsNullOrEmpty(userIdStr))
            {
                return Results.Unauthorized();
            }

            if (!Guid.TryParse(userIdStr, out var userId))
            {
                return Results.BadRequest(new { error = "invalid_user_id", message = "Invalid user ID format" });
            }

            // DDD CQRS: Use Enable2FACommand
            var command = new Api.BoundedContexts.Authentication.Application.Commands.Enable2FACommand(
                UserId: userId,
                TotpCode: request.Code
            );

            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            if (!result.Success)
            {
                logger.LogWarning("2FA enable failed for user {UserId}: {ErrorMessage}", userId, result.ErrorMessage);
                return Results.BadRequest(new { error = result.ErrorMessage ?? "Invalid verification code" });
            }

            logger.LogInformation("2FA enabled for user {UserId}", userId);
            return Results.Ok(new
            {
                Success = true,
                BackupCodes = result.BackupCodes, // Return backup codes for user to save
                ErrorMessage = (string?)null
            });
        })
        .RequireAuthorization()
        .WithName("Enable2FA")
        .WithTags("Authentication");
    }

    private static void MapTwoFactorVerificationEndpoints(RouteGroupBuilder group)
    {
        group.MapPost("/auth/2fa/verify", async (TwoFactorVerifyRequest request, HttpContext context, IRateLimitService rateLimitService, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            // Rate limit: 3 attempts per minute per session token
            var rateLimitKey = $"2fa:verify:{request.SessionToken}";
            var result = await rateLimitService.CheckRateLimitAsync(rateLimitKey, maxTokens: 3, refillRate: 0.05).ConfigureAwait(false);

            if (!result.Allowed)
            {
                logger.LogWarning("2FA verify rate limited for session {SessionToken}", LogSanitizer.Sanitize(request.SessionToken));
                return Results.StatusCode(429);
            }

            // Execute 2FA verification via CQRS handler
            var verifyCommand = new Verify2FACommand
            {
                SessionToken = request.SessionToken,
                Code = request.Code
            };

            var verifyResult = await mediator.Send(verifyCommand, ct).ConfigureAwait(false);

            if (!verifyResult.Success || verifyResult.UserId == null)
            {
                logger.LogWarning("2FA verification failed: {ErrorMessage}", verifyResult.ErrorMessage);
                return Results.Json(
                    new { error = "two_factor_failed", message = verifyResult.ErrorMessage ?? "Two-factor verification failed" },
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            // Create actual session after 2FA verification - DDD CQRS
            var sessionCommand = new DddCreateSessionCommand(
                UserId: verifyResult.UserId.Value,
                IpAddress: context.Connection.RemoteIpAddress?.ToString(),
                UserAgent: context.Request.Headers.UserAgent.ToString());

            var sessionResult = await mediator.Send(sessionCommand, ct).ConfigureAwait(false);

            CookieHelpers.WriteSessionCookie(context, sessionResult.SessionToken, sessionResult.ExpiresAt);
            logger.LogInformation("2FA verified and session created for user {UserId} via CQRS", verifyResult.UserId.Value);

            // Issue #1676 Phase 2: Return UserDto directly (no legacy conversion)
            return Results.Ok(new { message = "2FA verification successful", user = sessionResult.User });
        })
        .RequireRateLimiting("AuthVerify2FA") // C6: IP-based throttle on top of per-session-token limit
        .WithName("Verify2FA")
        .WithTags("Authentication");

        // SP5 Admin Security S3 — T5 + Option B: step-up re-verification on the CURRENT session.
        // Clears a step_up_required block from TwoFactorEnforcementBehavior by refreshing
        // LastTotpVerifiedAt. Does NOT create a new session. The acting admin (EffectiveActor) is
        // verified — during an impersonation that is the admin, not the target. All error mapping
        // delegates to ApiExceptionHandlerMiddleware via TwoFactorRequiredException /
        // TwoFactorUnavailableException for one shared error vocabulary with the enforcement filter.
        // Wire contract: docs/api/2fa-step-up-protocol.md.
        group.MapPost("/auth/2fa/step-up", async (
            TwoFactorStepUpRequest request, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            // EffectiveActor: the impersonating admin during an impersonation, else the user.
            var actorId = session.Principal!.EffectiveActor.Id;
            var sessionId = session.SessionId ?? Guid.Empty;
            if (sessionId == Guid.Empty)
            {
                // A valid session without a SessionId should not happen post-S2; fail closed.
                return Results.Unauthorized();
            }

            var result = await mediator.Send(
                new StepUpTwoFactorCommand(sessionId, actorId, request.Code), ct).ConfigureAwait(false);

            return result.Outcome switch
            {
                StepUpOutcome.Success => Results.Ok(new
                {
                    success = true,
                    lastTotpVerifiedAt = result.LastTotpVerifiedAt
                }),
                // The three non-success outcomes are mapped by the middleware to the shared 2FA
                // error vocabulary (401 two_factor_required + subcode for InvalidCode/LockedOut;
                // 503 two_factor_unavailable for store-down) so a client speaks the SAME wire format
                // here as it does for an enforcement-blocked command.
                StepUpOutcome.LockedOut => throw new TwoFactorRequiredException(
                    TwoFactorRequiredSubcode.LockedOut,
                    "Too many failed step-up attempts. Try again later.",
                    retryAfterSeconds: result.RetryAfterSeconds ?? 900),
                StepUpOutcome.Unavailable => throw new TwoFactorUnavailableException(
                    "Two-factor service is temporarily unavailable. Please try again."),
                StepUpOutcome.InvalidCode => throw new TwoFactorRequiredException(
                    TwoFactorRequiredSubcode.InvalidCode,
                    "Invalid or expired verification code."),
                _ => throw new TwoFactorRequiredException(
                    TwoFactorRequiredSubcode.InvalidCode,
                    "Invalid or expired verification code.")
            };
        })
        .RequireAuthorization()
        .WithName("StepUp2FA")
        .WithTags("Authentication")
        .WithSummary("Step-up 2FA verification on the current session (SP5 S3)")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status503ServiceUnavailable);
    }

    private static void MapTwoFactorManagementEndpoints(RouteGroupBuilder group)
    {
        group.MapPost("/auth/2fa/disable", async (TwoFactorDisableRequest request, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            var userIdStr = GetUserIdFromClaims(context.User);
            if (string.IsNullOrEmpty(userIdStr))
            {
                return Results.Unauthorized();
            }

            if (!Guid.TryParse(userIdStr, out var userId))
            {
                return Results.BadRequest(new { error = "invalid_user_id", message = "Invalid user ID format" });
            }

            // DDD CQRS: Use Disable2FACommand
            var command = new Api.BoundedContexts.Authentication.Application.Commands.Disable2FACommand(
                UserId: userId,
                CurrentPassword: request.Password,
                TotpOrBackupCode: request.Code
            );

            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            if (!result.Success)
            {
                logger.LogWarning("2FA disable failed for user {UserId}: {ErrorMessage}", userId, result.ErrorMessage);
                if (result.ErrorMessage?.Contains("password", StringComparison.OrdinalIgnoreCase) is true)
                {
                    return Results.Unauthorized();
                }
                return Results.BadRequest(new { error = result.ErrorMessage ?? "Failed to disable two-factor authentication" });
            }

            logger.LogInformation("2FA disabled for user {UserId}", userId);
            return Results.Ok(new { message = "Two-factor authentication disabled successfully" });
        })
        .AddEndpointFilter<Api.Infrastructure.Filters.AntiforgeryEndpointFilter>() // C8: CSRF
        .RequireAuthorization()
        .WithName("Disable2FA")
        .WithTags("Authentication");

        group.MapGet("/users/me/2fa/status", async (HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            var userIdStr = GetUserIdFromClaims(context.User);
            if (string.IsNullOrEmpty(userIdStr))
            {
                return Results.Unauthorized();
            }

            if (!Guid.TryParse(userIdStr, out var userId))
            {
                return Results.BadRequest(new { error = "invalid_user_id", message = "Invalid user ID format" });
            }

            // DDD CQRS: Use Get2FAStatusQuery
            var query = new Api.BoundedContexts.Authentication.Application.Queries.Get2FAStatusQuery(
                UserId: userId
            );

            var status = await mediator.Send(query, ct).ConfigureAwait(false);

            if (status == null)
            {
                logger.LogWarning("User {UserId} not found for 2FA status query", userId);
                return Results.NotFound(new { error = "User not found" });
            }

            return Results.Ok(status);
        })
        .RequireAuthorization()
        .WithName("Get2FAStatus")
        .WithTags("Users");
    }

    private static void MapTwoFactorAdminEndpoints(RouteGroupBuilder group)
    {
        // AUTH-08: Admin override to disable 2FA for locked-out users
        group.MapPost("/auth/admin/2fa/disable", async (AdminDisable2FARequest request, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            // Get admin user ID from authentication context
            var adminUserIdStr = GetUserIdFromClaims(context.User);
            if (string.IsNullOrEmpty(adminUserIdStr))
            {
                return Results.Unauthorized();
            }

            if (!Guid.TryParse(adminUserIdStr, out var adminUserId))
            {
                return Results.BadRequest(new { error = "invalid_user_id", message = "Invalid admin user ID format" });
            }

            // Validate target user ID
            if (!Guid.TryParse(request.TargetUserId, out var targetUserId))
            {
                return Results.BadRequest(new { error = "invalid_target_user_id", message = "Invalid target user ID format" });
            }

            // I4 (auth security fixes): admin re-auth with their own password.
            if (string.IsNullOrWhiteSpace(request.AdminPassword))
            {
                return Results.BadRequest(new
                {
                    error = "admin_password_required",
                    message = "Admin password is required to disable two-factor authentication for another user."
                });
            }

            // Execute admin 2FA disable via CQRS handler
            var command = new AdminDisable2FACommand(
                AdminUserId: adminUserId,
                TargetUserId: targetUserId,
                AdminPassword: request.AdminPassword
            );

            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            if (!result.Success)
            {
                logger.LogWarning(
                    "Admin {AdminUserId} failed to disable 2FA for user {TargetUserId}: {ErrorMessage}",
                    adminUserId,
                    targetUserId,
                    result.ErrorMessage);

                // Return appropriate status code based on error
                if (result.ErrorMessage?.Contains("Unauthorized") is true)
                {
                    return Results.Json(new { error = result.ErrorMessage }, statusCode: 403);
                }

                return Results.BadRequest(new { error = result.ErrorMessage });
            }

            logger.LogInformation(
                "Admin {AdminUserId} successfully disabled 2FA for user {TargetUserId}",
                adminUserId,
                targetUserId);

            return Results.Ok(new { message = "Two-factor authentication disabled successfully. User has been notified via email." });
        })
        .RequireAuthorization()
        .WithName("AdminDisable2FA")
        .WithTags("Authentication", "Admin")
        .WithSummary("Admin override to disable 2FA for locked-out users")
        .WithDescription(@"Allows administrators to disable two-factor authentication for users who have lost access to their authenticator app and backup codes.

**Authorization**: Requires admin role.

**Security**:
- Admin authorization is verified in the command handler
- Email notification is automatically sent to the affected user
- Action is logged in audit trail with admin override flag

**Use Case**: Support request for users locked out of their accounts due to lost 2FA credentials.

**Implementation**: Uses DDD CQRS pattern with IMediator and AdminDisable2FACommand.")
        .Produces(200)
        .Produces(400)
        .Produces(401)
        .Produces(403);


    }

    /// <summary>
    /// Extracts user ID from claims with fallback for different claim types.
    /// Supports both "sub" (JWT standard) and ClaimTypes.NameIdentifier (session auth).
    /// </summary>
    private static string? GetUserIdFromClaims(ClaimsPrincipal user)
    {
        return user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value;
    }

    /// <summary>
    /// Extracts email from claims with fallback for different claim types.
    /// </summary>
    private static string? GetEmailFromClaims(ClaimsPrincipal user)
    {
        return user.FindFirst(ClaimTypes.Email)?.Value
            ?? user.FindFirst("email")?.Value;
    }
}
