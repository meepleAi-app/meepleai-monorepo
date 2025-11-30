using Api.Models;
using Api.Services;
using MediatR;

// DDD CQRS imports
using GenerateTotpSetupCommand = Api.BoundedContexts.Authentication.Application.Commands.TwoFactor.GenerateTotpSetupCommand;
using Verify2FACommand = Api.BoundedContexts.Authentication.Application.Commands.TwoFactor.Verify2FACommand;
using AdminDisable2FACommand = Api.BoundedContexts.Authentication.Application.Commands.TwoFactor.AdminDisable2FACommand;
using DddCreateSessionCommand = Api.BoundedContexts.Authentication.Application.Commands.CreateSessionCommand;

namespace Api.Routing;

/// <summary>
/// Two-Factor Authentication endpoints.
/// Handles 2FA setup, enable, disable, verification, and status management.
/// </summary>
public static class TwoFactorEndpoints
{
    // AUTH-07: Two-Factor Authentication endpoints
    public static RouteGroupBuilder MapTwoFactorEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/auth/2fa/setup", async (HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            var userIdStr = context.User.FindFirst("sub")?.Value;
            var userEmail = context.User.FindFirst("email")?.Value;

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
            var userIdStr = context.User.FindFirst("sub")?.Value;
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

        group.MapPost("/auth/2fa/verify", async (TwoFactorVerifyRequest request, HttpContext context, IRateLimitService rateLimitService, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            // Rate limit: 3 attempts per minute per session token
            var rateLimitKey = $"2fa:verify:{request.SessionToken}";
            var result = await rateLimitService.CheckRateLimitAsync(rateLimitKey, maxTokens: 3, refillRate: 0.05).ConfigureAwait(false);

            if (!result.Allowed)
            {
                logger.LogWarning("2FA verify rate limited for session {SessionToken}", request.SessionToken);
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
                return Results.Unauthorized();
            }

            // Create actual session after 2FA verification - DDD CQRS
            var sessionCommand = new DddCreateSessionCommand(
                UserId: verifyResult.UserId.Value,
                IpAddress: context.Connection.RemoteIpAddress?.ToString(),
                UserAgent: context.Request.Headers.UserAgent.ToString());

            var sessionResult = await mediator.Send(sessionCommand, ct).ConfigureAwait(false);

            CookieHelpers.WriteSessionCookie(context, sessionResult.SessionToken, sessionResult.ExpiresAt);
            logger.LogInformation("2FA verified and session created for user {UserId} via CQRS", verifyResult.UserId.Value);

            // Map to legacy format for backward compatibility
            var legacyUser = new AuthUser(
                Id: sessionResult.User.Id.ToString(),
                Email: sessionResult.User.Email,
                DisplayName: sessionResult.User.DisplayName,
                Role: sessionResult.User.Role);

            return Results.Ok(new { message = "2FA verification successful", user = legacyUser });
        })
        .WithName("Verify2FA")
        .WithTags("Authentication");

        group.MapPost("/auth/2fa/disable", async (TwoFactorDisableRequest request, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            var userIdStr = context.User.FindFirst("sub")?.Value;
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
                if (result.ErrorMessage?.Contains("password", StringComparison.OrdinalIgnoreCase) == true)
                {
                    return Results.Unauthorized();
                }
                return Results.BadRequest(new { error = result.ErrorMessage ?? "Failed to disable two-factor authentication" });
            }

            logger.LogInformation("2FA disabled for user {UserId}", userId);
            return Results.Ok(new { message = "Two-factor authentication disabled successfully" });
        })
        .RequireAuthorization()
        .WithName("Disable2FA")
        .WithTags("Authentication");

        group.MapGet("/users/me/2fa/status", async (HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            var userIdStr = context.User.FindFirst("sub")?.Value;
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

        // AUTH-08: Admin override to disable 2FA for locked-out users
        group.MapPost("/auth/admin/2fa/disable", async (AdminDisable2FARequest request, HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            // Get admin user ID from authentication context
            var adminUserIdStr = context.User.FindFirst("sub")?.Value;
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

            // Execute admin 2FA disable via CQRS handler
            var command = new AdminDisable2FACommand(
                AdminUserId: adminUserId,
                TargetUserId: targetUserId
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
                if (result.ErrorMessage?.Contains("Unauthorized") == true)
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

        return group;
    }
}
