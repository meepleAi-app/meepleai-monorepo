using Api.Configuration;
using Api.Extensions;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Security;
using Api.Models;
using Api.Services;
using MediatR;
using Microsoft.AspNetCore.Mvc; // For [FromBody] attribute

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
using GetSessionByTokenHashQuery = Api.BoundedContexts.Authentication.Application.Queries.GetSessionByTokenHashQuery;
using AcceptInvitationCommand = Api.BoundedContexts.Authentication.Application.Commands.Invitation.AcceptInvitationCommand;
using ValidateInvitationTokenQuery = Api.BoundedContexts.Authentication.Application.Queries.Invitation.ValidateInvitationTokenQuery;

namespace Api.Routing;

/// <summary>
/// Core authentication endpoints.
/// Handles user registration, login, logout, API key authentication, and session management.
///
/// Authentication pattern:
/// - <c>RequireAdminSession()</c>: Custom extension that validates the session cookie and checks admin role
///   in a single call. Used for endpoints that need admin access without ASP.NET authorization middleware.
/// - <c>.RequireAuthorization("AdminOnlyPolicy")</c>: Standard ASP.NET authorization middleware policy.
///   Used for endpoints registered via MapGroup() with policy-based auth.
/// Both resolve to the same "admin" role check. RequireAdminSession() is preferred in endpoint handlers
/// that need the session object for auditing (e.g., session.User.Id for logging).
/// </summary>
internal static class AuthenticationEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder group)
    {
        MapRegisterEndpoint(group);
        MapLoginEndpoint(group);
        MapLogoutEndpoint(group);
        MapApiKeyEndpoints(group);
        MapMeEndpoint(group);

        // Map session management endpoints
        MapSessionEndpoints(group);

        // Map endpoints from other files
        group.MapTwoFactorEndpoints();
        group.MapOAuthEndpoints(CookieHelpers.WriteSessionCookie);
        group.MapPasswordResetEndpoints(CookieHelpers.WriteSessionCookie);
        group.MapEmailVerificationEndpoints(); // ISSUE-3071: Email verification

        // ISSUE-124: Invitation acceptance endpoints (public, unauthenticated)
        MapAcceptInvitationEndpoint(group);
        MapValidateInvitationEndpoint(group);

        return group;
    }

    private static void MapRegisterEndpoint(RouteGroupBuilder group)
    {
        // SEC-05: Rate limit registration to 5 req/min per IP
        group.MapPost("/auth/register", async (HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            RegisterPayload? payload;
            try
            {
                payload = await context.Request.ReadFromJsonAsync<RegisterPayload>(ct).ConfigureAwait(false);
            }
            catch (System.Text.Json.JsonException)
            {
                return Results.BadRequest(new { error = "Invalid request payload" });
            }

            if (payload == null)
            {
                return Results.BadRequest(new { error = "Invalid request payload" });
            }

            if (string.IsNullOrWhiteSpace(payload.Email) || string.IsNullOrWhiteSpace(payload.Password))
            {
                return Results.BadRequest(new { error = "Email and password are required" });
            }

            var displayName = string.IsNullOrWhiteSpace(payload.DisplayName)
                ? payload.Email.Split('@')[0]
                : payload.DisplayName.Trim();

            var command = new DddRegisterCommand(
                Email: payload.Email,
                Password: payload.Password,
                DisplayName: displayName,
                Role: null,
                IpAddress: context.Connection.RemoteIpAddress?.ToString(),
                UserAgent: context.Request.Headers.UserAgent.ToString());

            logger.LogInformation("User registration attempt for {Email}", DataMasking.MaskEmail(payload.Email));
            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            CookieHelpers.WriteSessionCookie(context, result.SessionToken, result.ExpiresAt);
            CookieHelpers.WriteUserRoleCookie(context, result.User.Role, result.ExpiresAt);
            logger.LogInformation("User {UserId} registered successfully with role {Role}", result.User.Id, result.User.Role);

            return Results.Json(new { user = result.User, expiresAt = result.ExpiresAt });
        }).RequireRateLimiting("AuthRegister");
    }

    private static void MapLoginEndpoint(RouteGroupBuilder group)
    {
        // SEC-05: Rate limit login to 10 req/min per IP
        group.MapPost("/auth/login", async (HttpContext context, IMediator mediator, IConfigurationService configService, ILogger<Program> logger, CancellationToken ct) =>
        {
            LoginPayload? payload;
            try
            {
                payload = await context.Request.ReadFromJsonAsync<LoginPayload>(ct).ConfigureAwait(false);
            }
            catch (System.Text.Json.JsonException)
            {
                return Results.BadRequest(new { error = "Invalid request payload" });
            }

            if (payload == null)
            {
                logger.LogWarning("Login failed: payload is null");
                return Results.BadRequest(new { error = "Invalid request payload" });
            }

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

            logger.LogInformation("Login attempt for {Email}", DataMasking.MaskEmail(payload.Email));
            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            if (result.RequiresTwoFactor)
            {
                logger.LogInformation("User requires 2FA, temp session created");
                return Results.Json(new
                {
                    requiresTwoFactor = true,
                    sessionToken = result.TempSessionToken,
                    message = "Two-factor authentication required"
                });
            }

            if (result.User == null || result.SessionToken == null)
            {
                logger.LogWarning("Login failed for {Email}: missing user or session token", DataMasking.MaskEmail(payload.Email));
                CookieHelpers.RemoveSessionCookie(context);
                return Results.Json(new { error = "Invalid email or password" }, statusCode: StatusCodes.Status401Unauthorized);
            }

            var sessionExpirationDays = (await configService.GetValueAsync<int?>("Authentication:SessionManagement:SessionExpirationDays", 30).ConfigureAwait(false)) ?? 30;
            var expiresAt = DateTime.UtcNow.AddDays(sessionExpirationDays);
            CookieHelpers.WriteSessionCookie(context, result.SessionToken, expiresAt);
            CookieHelpers.WriteUserRoleCookie(context, result.User.Role, expiresAt);
            logger.LogInformation("User {UserId} logged in successfully with role {Role}", result.User.Id, result.User.Role);

            return Results.Json(new { user = result.User, expiresAt });
        }).RequireRateLimiting("AuthLogin");
    }

    private static void MapLogoutEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/auth/logout", async (HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            var sessionCookieName = CookieHelpers.GetSessionCookieName(context);

            if (context.Request.Cookies.TryGetValue(sessionCookieName, out var token) &&
                !string.IsNullOrWhiteSpace(token))
            {
                var command = new DddLogoutCommand(SessionToken: token);
                await mediator.Send(command, ct).ConfigureAwait(false);
                logger.LogInformation("User logged out successfully");
            }

            CookieHelpers.RemoveSessionCookie(context);
            CookieHelpers.RemoveUserRoleCookie(context);
            return Results.Json(new { ok = true });
        });
    }

    private static void MapApiKeyEndpoints(RouteGroupBuilder group)
    {
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
            CookieHelpers.WriteApiKeyCookie(context, protectedApiKey);
            logger.LogInformation("User {UserId} validated API key {ApiKeyId} and cookie issued", result.User.Id, result.ApiKeyId);

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

        group.MapPost("/auth/apikey/logout", async (
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var command = new LogoutApiKeyCommand();
            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            CookieHelpers.RemoveApiKeyCookie(context);
            logger.LogInformation("API key cookie removed");

            return Results.Json(new { ok = true, message = result.Message });
        })
        .WithName("LogoutApiKey")
        .WithTags("Authentication", "API Keys")
        .WithSummary("Logout API key authentication by removing httpOnly cookie")
        .WithDescription("Removes the secure API key cookie so browsers no longer send it automatically. Use key management endpoints to revoke the key entirely.")
        .Produces(200);
    }

    private static void MapMeEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/auth/me", (HttpContext context) =>
        {
            var authType = context.User.FindFirst("AuthType")?.Value;
            if (string.Equals(authType, "ApiKey", StringComparison.Ordinal) && context.User.Identity?.IsAuthenticated is true)
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

                var user = new { id = userId, email, displayName = displayName ?? email, role = role ?? UserRole.User.ToString() };
                return Results.Json(new { user, expiresAt = (DateTime?)null });
            }

            var (authenticated, session, _) = context.TryGetActiveSession();
            if (authenticated)
            {
                return Results.Json(new { user = session.User, expiresAt = session.ExpiresAt });
            }

            return Results.Unauthorized();
        });
    }

    // AUTH-05: Session management endpoints
    private static void MapSessionEndpoints(RouteGroupBuilder group)
    {
        MapSessionStatusEndpoint(group);
        MapSessionExtendEndpoint(group);
        MapUserSessionsEndpoint(group);
        MapSessionIdStatusEndpoint(group);
        MapSessionIdExtendEndpoint(group);
        MapSessionRevokeEndpoint(group);
        MapRevokeAllSessionsEndpoint(group);
    }

    private static void MapSessionStatusEndpoint(RouteGroupBuilder group)
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
    }

    private static void MapSessionExtendEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/auth/session/extend", async (
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            // Require authentication
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            // Get session token hash from cookie
            var sessionCookieName = CookieHelpers.GetSessionCookieName(context);
            if (!context.Request.Cookies.TryGetValue(sessionCookieName, out var token) || string.IsNullOrWhiteSpace(token))
            {
                return Results.Unauthorized();
            }

            var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
            var tokenHash = Convert.ToBase64String(hash);

            // Look up session by token hash via CQRS query
            var dbSession = await mediator.Send(new GetSessionByTokenHashQuery(tokenHash), ct).ConfigureAwait(false);

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
    }

    private static void MapUserSessionsEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/users/me/sessions", async (HttpContext context, IMediator mediator, CancellationToken ct = default) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            var query = new GetUserSessionsQuery(session!.User!.Id);
            var sessions = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Json(sessions);
        });
    }

    private static void MapSessionIdStatusEndpoint(RouteGroupBuilder group)
    {
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
    }

    private static void MapSessionIdExtendEndpoint(RouteGroupBuilder group)
    {
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
                if (result.ErrorMessage?.Contains("Rate limit exceeded") is true)
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
    }

    private static void MapSessionRevokeEndpoint(RouteGroupBuilder group)
    {
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
    }

    private static void MapRevokeAllSessionsEndpoint(RouteGroupBuilder group)
    {
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
            var sessionCookieName = CookieHelpers.GetSessionCookieName(context);
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

    // ISSUE-124: Accept invitation (public, unauthenticated)
    private static void MapAcceptInvitationEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/auth/accept-invitation", async (
            HttpContext context,
            IMediator mediator,
            IConfigurationService configService,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            AcceptInvitationPayload? payload;
            try
            {
                payload = await context.Request.ReadFromJsonAsync<AcceptInvitationPayload>(ct).ConfigureAwait(false);
            }
            catch (System.Text.Json.JsonException)
            {
                return Results.BadRequest(new { error = "Invalid request payload" });
            }

            if (payload == null)
            {
                return Results.BadRequest(new { error = "Invalid request payload" });
            }

            if (string.IsNullOrWhiteSpace(payload.Token) ||
                string.IsNullOrWhiteSpace(payload.Password) ||
                string.IsNullOrWhiteSpace(payload.ConfirmPassword))
            {
                return Results.BadRequest(new { error = "Token, password, and confirmPassword are required" });
            }

            logger.LogInformation("Invitation acceptance attempt");

            var command = new AcceptInvitationCommand(
                payload.Token,
                payload.Password,
                payload.ConfirmPassword);

            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            // Set auth cookie for immediate login (follows existing login pattern)
            var sessionExpirationDays = (await configService.GetValueAsync<int?>("Authentication:SessionManagement:SessionExpirationDays", 30).ConfigureAwait(false)) ?? 30;
            var expiresAt = DateTime.UtcNow.AddDays(sessionExpirationDays);
            CookieHelpers.WriteSessionCookie(context, result.SessionToken, expiresAt);
            CookieHelpers.WriteUserRoleCookie(context, result.User.Role, expiresAt);

            logger.LogInformation("User {UserId} created via invitation and logged in with role {Role}",
                result.User.Id, result.User.Role);

            return Results.Json(new { user = result.User, expiresAt });
        })
        .WithName("AcceptInvitation")
        .WithTags("Authentication", "Invitations")
        .WithSummary("Accept an invitation and create an account")
        .WithDescription("Validates the invitation token, creates the user account, and returns a session for immediate login.")
        .RequireRateLimiting("AuthRegister")
        .Produces(200)
        .Produces(400);
    }

    // ISSUE-124: Validate invitation token (public, unauthenticated)
    private static void MapValidateInvitationEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/auth/validate-invitation", async (
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            ValidateInvitationPayload? payload;
            try
            {
                payload = await context.Request.ReadFromJsonAsync<ValidateInvitationPayload>(ct).ConfigureAwait(false);
            }
            catch (System.Text.Json.JsonException)
            {
                return Results.BadRequest(new { error = "Invalid request payload" });
            }

            if (payload == null || string.IsNullOrWhiteSpace(payload.Token))
            {
                return Results.BadRequest(new { error = "Token is required" });
            }

            logger.LogInformation("Invitation token validation attempt");

            var query = new ValidateInvitationTokenQuery(payload.Token);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);

            return Results.Ok(result);
        })
        .WithName("ValidateInvitationToken")
        .WithTags("Authentication", "Invitations")
        .WithSummary("Validate an invitation token")
        .WithDescription("Checks whether an invitation token is valid without consuming it. Returns role and expiry info.")
        .RequireRateLimiting("AuthRegister")
        .Produces(200)
        .Produces(400);
    }
}

/// <summary>
/// Payload for accepting an invitation (Issue #124).
/// </summary>
internal record AcceptInvitationPayload(string Token, string Password, string ConfirmPassword);

/// <summary>
/// Payload for validating an invitation token (Issue #124).
/// </summary>
internal record ValidateInvitationPayload(string Token);
