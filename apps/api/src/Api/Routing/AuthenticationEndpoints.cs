using Api.Configuration;
using Api.Extensions;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using MediatR;
using Microsoft.AspNetCore.Mvc; // For [FromBody] attribute
using Microsoft.EntityFrameworkCore; // For AsNoTracking
using Microsoft.Extensions.Options;

// DDD CQRS imports (using aliases to avoid conflicts with Api.Models)
using DddRegisterCommand = Api.BoundedContexts.Authentication.Application.Commands.RegisterCommand;
using DddLoginCommand = Api.BoundedContexts.Authentication.Application.Commands.LoginCommand;
using DddLogoutCommand = Api.BoundedContexts.Authentication.Application.Commands.LogoutCommand;
using DddCreateSessionCommand = Api.BoundedContexts.Authentication.Application.Commands.CreateSessionCommand;
using GetSessionStatusQuery = Api.BoundedContexts.Authentication.Application.Queries.GetSessionStatusQuery;
using ExtendSessionCommand = Api.BoundedContexts.Authentication.Application.Commands.ExtendSessionCommand;
using RevokeSessionCommand = Api.BoundedContexts.Authentication.Application.Commands.RevokeSessionCommand;
using GetUserSessionsQuery = Api.BoundedContexts.Authentication.Application.Queries.GetUserSessionsQuery;
using LoginWithApiKeyCommand = Api.BoundedContexts.Authentication.Application.Commands.LoginWithApiKeyCommand;
using LogoutApiKeyCommand = Api.BoundedContexts.Authentication.Application.Commands.LogoutApiKeyCommand;
using LogoutAllDevicesCommand = Api.BoundedContexts.Authentication.Application.Commands.LogoutAllDevicesCommand;

namespace Api.Routing;

/// <summary>
/// Core authentication endpoints.
/// Handles user registration, login, logout, API key authentication, and session management.
/// </summary>
public static class AuthenticationEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder group)
    {
        // Import static methods from CookieHelpers for session cookie management
        var writeSessionCookie = CookieHelpers.WriteSessionCookie;
        var removeSessionCookie = CookieHelpers.RemoveSessionCookie;
        var getSessionCookieName = CookieHelpers.GetSessionCookieName;
        var writeApiKeyCookie = CookieHelpers.WriteApiKeyCookie;
        var removeApiKeyCookie = CookieHelpers.RemoveApiKeyCookie;
        var writeUserRoleCookie = CookieHelpers.WriteUserRoleCookie;
        var removeUserRoleCookie = CookieHelpers.RemoveUserRoleCookie;

        // User registration (DDD/CQRS)
        group.MapPost("/auth/register", async (RegisterPayload payload, IMediator mediator, HttpContext context, ILogger<Program> logger, CancellationToken ct) =>
        {
            var displayName = string.IsNullOrWhiteSpace(payload.DisplayName)
                ? payload.Email.Split('@')[0]
                : payload.DisplayName.Trim();

            var command = new DddRegisterCommand(
                Email: payload.Email,
                Password: payload.Password,
                DisplayName: displayName,
                Role: payload.Role,
                IpAddress: context.Connection.RemoteIpAddress?.ToString(),
                UserAgent: context.Request.Headers.UserAgent.ToString());

            logger.LogInformation("User registration attempt for {Email}", payload.Email);
            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            writeSessionCookie(context, result.SessionToken, result.ExpiresAt);
            writeUserRoleCookie(context, result.User.Role, result.ExpiresAt);
            logger.LogInformation("User {UserId} registered successfully with role {Role}", result.User.Id, result.User.Role);

            // Issue #1676 Phase 2: Return UserDto directly (no legacy conversion)
            return Results.Json(new { user = result.User, expiresAt = result.ExpiresAt });
        });

        // User login with 2FA support (AUTH-07) - DDD CQRS
        group.MapPost("/auth/login", async ([FromBody] LoginPayload payload, HttpContext context, IMediator mediator, IConfigurationService configService, ILogger<Program> logger, CancellationToken ct) =>
        {
            if (payload == null)
            {
                logger.LogWarning("Login failed: payload is null");
                return Results.BadRequest(new { error = "Invalid request payload" });
            }

            // Validate email and password are not empty
            if (string.IsNullOrWhiteSpace(payload.Email) || string.IsNullOrWhiteSpace(payload.Password))
            {
                logger.LogWarning("Login failed: email or password is empty");
                return Results.BadRequest(new { error = "Email and password are required" });
            }

            var command = new DddLoginCommand(
                Email: payload.Email,
                Password: payload.Password,
                IpAddress: context.Connection.RemoteIpAddress?.ToString(),
                UserAgent: context.Request.Headers.UserAgent.ToString());

            logger.LogInformation("Login attempt for {Email}", payload.Email);
            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            // AUTH-07: 2FA flow
            if (result.RequiresTwoFactor)
            {
                logger.LogInformation("User requires 2FA, temp session created");
                return Results.Json(new
                {
                    requiresTwoFactor = true,
                    sessionToken = result.TempSessionToken, // Secure temp token (5-min TTL, single-use)
                    message = "Two-factor authentication required"
                });
            }

            // Normal login (no 2FA)
            if (result.User == null || result.SessionToken == null)
            {
                logger.LogWarning("Login failed for {Email}: missing user or session token", payload.Email);
                removeSessionCookie(context);
                return Results.Unauthorized();
            }

            // Calculate session expiration from configuration (default: 30 days)
            var sessionExpirationDays = (await configService.GetValueAsync<int?>("Authentication:SessionManagement:SessionExpirationDays", 30).ConfigureAwait(false)) ?? 30;
            var expiresAt = DateTime.UtcNow.AddDays(sessionExpirationDays);
            writeSessionCookie(context, result.SessionToken, expiresAt);
            writeUserRoleCookie(context, result.User.Role, expiresAt);
            logger.LogInformation("User {UserId} logged in successfully", result.User.Id);

            // Issue #1676 Phase 2: Return UserDto directly (no legacy conversion)
            return Results.Json(new { user = result.User, expiresAt });
        });

        // User logout - DDD CQRS
        group.MapPost("/auth/logout", async (HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            var sessionCookieName = getSessionCookieName(context);

            if (context.Request.Cookies.TryGetValue(sessionCookieName, out var token) &&
                !string.IsNullOrWhiteSpace(token))
            {
                var command = new DddLogoutCommand(SessionToken: token);
                await mediator.Send(command, ct).ConfigureAwait(false);
                logger.LogInformation("User logged out successfully");
            }

            removeSessionCookie(context);
            removeUserRoleCookie(context);
            return Results.Json(new { ok = true });
        });

        // API Key Login - Validates API key credentials and stores them in a secure cookie
        group.MapPost("/auth/apikey/login", async (
            ApiKeyLoginPayload payload,
            HttpContext context,
            IMediator mediator,
            ApiKeyCookieService apiKeyCookieService,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(payload.ApiKey))
            {
                logger.LogWarning("API key login failed: API key is empty");
                return Results.BadRequest(new { error = "API key is required" });
            }

            var command = new LoginWithApiKeyCommand(ApiKey: payload.ApiKey);

            logger.LogInformation("API key login attempt");
            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            var protectedApiKey = apiKeyCookieService.Protect(payload.ApiKey);
            writeApiKeyCookie(context, protectedApiKey);
            writeUserRoleCookie(context, result.User.Role, DateTime.UtcNow.AddDays(90));
            logger.LogInformation("User {UserId} validated API key {ApiKeyId} and cookie issued", result.User.Id, result.ApiKeyId);

            // Issue #1676 Phase 2: Return UserDto directly (no legacy conversion)
            return Results.Json(new
            {
                user = result.User,
                message = "API key verified. You can keep using the secure cookie or send Authorization: ApiKey <value> on API calls."
            });
        })
        .WithName("LoginWithApiKey")
        .WithTags("Authentication", "API Keys")
        .WithSummary("Validate an API key")
        .WithDescription(@"Authenticates an API key, stores it inside an httpOnly cookie for browsers, and returns the associated profile.
Clients can also store the key securely and send it via the `Authorization: ApiKey {key}` header.")
        .Produces(200)
        .Produces(400)
        .Produces(401);

        // API Key Logout - Removes the httpOnly cookie
        group.MapPost("/auth/apikey/logout", async (
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var command = new LogoutApiKeyCommand();
            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            removeApiKeyCookie(context);
            removeUserRoleCookie(context);
            logger.LogInformation("API key cookie removed");

            return Results.Json(new { ok = true, message = result.Message });
        })
        .WithName("LogoutApiKey")
        .WithTags("Authentication", "API Keys")
        .WithSummary("Logout API key authentication by removing httpOnly cookie")
        .WithDescription("Removes the secure API key cookie so browsers no longer send it automatically. Use key management endpoints to revoke the key entirely.")
        .Produces(200);

        // Get current user (AUTH-01: Supports both cookie and API key auth)
        // Priority: API key > Cookie session
        group.MapGet("/auth/me", (HttpContext context) =>
        {
            // Check for API key authentication first (higher priority)
            // API keys are identified by the "AuthType" claim with value "ApiKey"
            var authType = context.User.FindFirst("AuthType")?.Value;
            if (string.Equals(authType, "ApiKey", StringComparison.Ordinal) && context.User.Identity?.IsAuthenticated == true)
            {
                var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var email = context.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
                var displayName = context.User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
                var role = context.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

                if (string.IsNullOrEmpty(userId))
                {
                    return Results.Unauthorized();
                }

                if (string.IsNullOrEmpty(email))
                {
                    return Results.Unauthorized();
                }

                // Issue #1676 Phase 2: Construct UserDto inline (API key auth doesn't have full UserDto)
                var user = new { id = userId, email, displayName = displayName ?? email, role = role ?? UserRole.User.ToString() };
                return Results.Json(new { user, expiresAt = (DateTime?)null }); // API keys don't have session expiration
            }

            // Fall back to cookie-based session auth (DDD SessionStatusDto)
            var (authenticated, session, _) = context.TryGetActiveSession();
            if (authenticated)
            {
                return Results.Json(new { user = session.User, expiresAt = session.ExpiresAt });
            }

            return Results.Unauthorized();
        });

        // Map session management endpoints
        MapSessionEndpoints(group, getSessionCookieName);

        // Map endpoints from other files
        group.MapTwoFactorEndpoints();
        group.MapOAuthEndpoints(writeSessionCookie);
        group.MapPasswordResetEndpoints(writeSessionCookie);

        return group;
    }

    // AUTH-05: Session management endpoints
    private static void MapSessionEndpoints(RouteGroupBuilder group, Func<HttpContext, string> getSessionCookieName)
    {
        group.MapGet("/auth/session/status", (
            HttpContext context,
            IMediator mediator,
            IConfiguration config,
            CancellationToken ct) =>
        {
            // Require authentication
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            // Calculate remaining time
            var now = DateTime.UtcNow;
            var remainingTime = session.ExpiresAt - now;
            var remainingMinutes = (int)Math.Max(0, remainingTime?.TotalMinutes ?? 0);

            // Return session status from already-authenticated session
            var response = new SessionStatusResponse(
                session.ExpiresAt ?? DateTime.UtcNow.AddDays(30),
                session.LastSeenAt ?? DateTime.UtcNow,
                remainingMinutes
            );

            return Results.Json(response);
        });

        group.MapPost("/auth/session/extend", async (
            HttpContext context,
            IMediator mediator,
            IConfiguration config,
            Api.Infrastructure.MeepleAiDbContext db,
            CancellationToken ct) =>
        {
            // Require authentication
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            // Get session token hash from cookie
            var sessionCookieName = getSessionCookieName(context);
            if (!context.Request.Cookies.TryGetValue(sessionCookieName, out var token) || string.IsNullOrWhiteSpace(token))
            {
                return Results.Unauthorized();
            }

            var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
            var tokenHash = Convert.ToBase64String(hash);

            // Look up session by token hash to get session ID
            var dbSession = await db.UserSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.TokenHash == tokenHash, ct).ConfigureAwait(false);

            if (dbSession == null)
            {
                return Results.Unauthorized();
            }

            // Use CQRS Command to extend session (default 30 days extension)
            var command = new ExtendSessionCommand(
                dbSession.Id,
                session!.User!.Id,
                ExtensionDuration: null  // Use default from Session.DefaultLifetime
            );
            var response = await mediator.Send(command, ct).ConfigureAwait(false);

            if (!response.Success)
            {
                return Results.BadRequest(new { error = response.ErrorMessage });
            }

            return Results.Json(new { expiresAt = response.NewExpiresAt });
        });

        group.MapGet("/users/me/sessions", async (HttpContext context, IMediator mediator, CancellationToken ct = default) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            var query = new GetUserSessionsQuery(session!.User!.Id);
            var sessions = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Json(sessions);
        });

        group.MapGet("/auth/sessions/{sessionId:guid}/status", async (
            Guid sessionId,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            // Require authentication
            if (!context.User.Identity?.IsAuthenticated ?? true)
            {
                return Results.Unauthorized();
            }

            // Extract user ID and role from claims
            var userIdClaim = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Results.Unauthorized();
            }

            var role = context.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "user";
            var isAdmin = role.Equals("admin", StringComparison.OrdinalIgnoreCase);

            // Execute query via CQRS
            var query = new GetSessionStatusQuery(sessionId, userId, isAdmin);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            if (result == null)
            {
                return Results.NotFound(new { error = "Session not found or access denied" });
            }

            return Results.Json(result);
        }).RequireAuthorization();

        group.MapPost("/auth/sessions/{sessionId:guid}/extend", async (
            Guid sessionId,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            // Require authentication
            if (!context.User.Identity?.IsAuthenticated ?? true)
            {
                return Results.Unauthorized();
            }

            // Extract user ID from claims
            var userIdClaim = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Results.Unauthorized();
            }

            // Execute command via CQRS (with rate limiting)
            var command = new ExtendSessionCommand(sessionId, userId);
            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            if (!result.Success)
            {
                // Check if it's a rate limit error
                if (result.ErrorMessage?.Contains("Rate limit exceeded") == true)
                {
                    return Results.Json(new { error = result.ErrorMessage }, statusCode: 429);
                }

                return Results.BadRequest(new { error = result.ErrorMessage });
            }

            // Refresh session cookie with new expiration if this is the current session
            var sessionCookieName = CookieHelpers.GetSessionCookieName(context);
            var currentSessionToken = context.Request.Cookies[sessionCookieName];
            if (!string.IsNullOrEmpty(currentSessionToken) && result.NewExpiresAt.HasValue)
            {
                CookieHelpers.WriteSessionCookie(context, currentSessionToken, result.NewExpiresAt.Value);
                logger.LogInformation("Session cookie refreshed for session {SessionId}, new expiration: {ExpiresAt}", sessionId, result.NewExpiresAt.Value);
            }

            return Results.Json(new { ok = true, expiresAt = result.NewExpiresAt });
        }).RequireAuthorization();

        group.MapPost("/auth/sessions/{sessionId:guid}/revoke", async (
            Guid sessionId,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            // Require authentication
            if (!context.User.Identity?.IsAuthenticated ?? true)
            {
                return Results.Unauthorized();
            }

            // Extract user ID and role from claims
            var userIdClaim = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Results.Unauthorized();
            }

            var role = context.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "user";
            var isAdmin = role.Equals("admin", StringComparison.OrdinalIgnoreCase);

            // Execute command via CQRS
            var command = new RevokeSessionCommand(sessionId, userId, isAdmin);
            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            if (!result.Success)
            {
                return Results.BadRequest(new { error = result.ErrorMessage });
            }

            return Results.Json(new { ok = true, message = "Session revoked successfully" });
        }).RequireAuthorization();

        // Issue #2056: Logout from all devices (revoke all sessions except optionally the current one)
        group.MapPost("/auth/sessions/revoke-all", async (
            [FromBody] LogoutAllDevicesPayload? payload,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            // Require authentication
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            var userId = session!.User!.Id;

            // Get current session token hash for optional exclusion
            string? currentTokenHash = null;
            var sessionCookieName = getSessionCookieName(context);
            if (context.Request.Cookies.TryGetValue(sessionCookieName, out var token) && !string.IsNullOrWhiteSpace(token))
            {
                var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
                currentTokenHash = Convert.ToBase64String(hash);
            }

            // Execute command via CQRS
            var command = new LogoutAllDevicesCommand(
                UserId: userId,
                CurrentSessionTokenHash: currentTokenHash,
                IncludeCurrentSession: payload?.IncludeCurrentSession ?? false,
                Password: payload?.Password
            );
            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            if (!result.Success)
            {
                logger.LogWarning("Failed to logout all devices for user {UserId}: {Error}", userId, result.ErrorMessage);
                return Results.BadRequest(new { error = result.ErrorMessage });
            }

            // If current session was revoked, clear the cookie
            if (result.CurrentSessionRevoked)
            {
                CookieHelpers.RemoveSessionCookie(context);
                CookieHelpers.RemoveUserRoleCookie(context);
            }

            logger.LogInformation(
                "User {UserId} logged out of {Count} devices. Current session revoked: {CurrentRevoked}",
                userId, result.RevokedSessionCount, result.CurrentSessionRevoked);

            return Results.Json(new
            {
                ok = true,
                revokedCount = result.RevokedSessionCount,
                currentSessionRevoked = result.CurrentSessionRevoked,
                message = result.RevokedSessionCount > 0
                    ? $"Successfully logged out of {result.RevokedSessionCount} device(s)"
                    : "No other active sessions to revoke"
            });
        }).RequireAuthorization()
        .WithName("LogoutAllDevices")
        .WithTags("Authentication", "Sessions")
        .WithSummary("Logout from all devices")
        .WithDescription("Revokes all active sessions for the current user. Optionally exclude the current session or include it with confirmation. Password verification can be required for additional security.")
        .Produces(200)
        .Produces(400)
        .Produces(401);
    }
}
