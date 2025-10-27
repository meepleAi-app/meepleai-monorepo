using Api.Configuration;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Routing;

/// <summary>
/// Authentication and authorization endpoints.
/// Handles user registration, login, logout, OAuth, 2FA, sessions, and password reset.
/// </summary>
public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder group)
    {
        // Import static methods from CookieHelpers for session cookie management
        var writeSessionCookie = CookieHelpers.WriteSessionCookie;
        var removeSessionCookie = CookieHelpers.RemoveSessionCookie;
        var getSessionCookieName = CookieHelpers.GetSessionCookieName;

        // User registration
        group.MapPost("/auth/register", async (RegisterPayload payload, HttpContext context, AuthService auth, ILogger<Program> logger, CancellationToken ct) =>
        {
            try
            {
                var command = new RegisterCommand(
                    payload.Email,
                    payload.Password,
                    payload.DisplayName,
                    payload.Role,
                    context.Connection.RemoteIpAddress?.ToString(),
                    context.Request.Headers.UserAgent.ToString());

                logger.LogInformation("User registration attempt for {Email}", payload.Email);
                var result = await auth.RegisterAsync(command, ct);
                writeSessionCookie(context, result.SessionToken, result.ExpiresAt);
                logger.LogInformation("User {UserId} registered successfully with role {Role}", result.User.Id, result.User.Role);
                return Results.Json(new AuthResponse(result.User, result.ExpiresAt));
            }
            catch (ArgumentException ex)
            {
                logger.LogWarning("Registration validation failed for {Email}: {Error}", payload.Email, ex.Message);
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                logger.LogWarning("Registration conflict for {Email}: {Error}", payload.Email, ex.Message);
                return Results.Conflict(new { error = ex.Message });
            }
        });

        // User login with 2FA support (AUTH-07)
        group.MapPost("/auth/login", async (LoginPayload? payload, HttpContext context, AuthService auth, MeepleAiDbContext db, ITempSessionService tempSessionService, ILogger<Program> logger, CancellationToken ct) =>
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

            try
            {
                var command = new LoginCommand(
                    payload.Email,
                    payload.Password,
                    context.Connection.RemoteIpAddress?.ToString(),
                    context.Request.Headers.UserAgent.ToString());

                logger.LogInformation("Login attempt for {Email}", payload.Email);
                var result = await auth.LoginAsync(command, ct);
                if (result == null)
                {
                    logger.LogWarning("Login failed for {Email}", payload.Email);
                    removeSessionCookie(context);
                    return Results.Unauthorized();
                }

                // AUTH-07: Check if 2FA is enabled
                var user = await db.Users.FindAsync(result.User.Id);
                if (user?.IsTwoFactorEnabled == true)
                {
                    // Create temp session for 2FA verification
                    var tempToken = await tempSessionService.CreateTempSessionAsync(
                        result.User.Id,
                        context.Connection.RemoteIpAddress?.ToString());

                    logger.LogInformation("User {UserId} requires 2FA, temp session created", result.User.Id);
                    return Results.Json(new
                    {
                        requiresTwoFactor = true,
                        sessionToken = tempToken, // Secure temp token (5-min TTL, single-use)
                        message = "Two-factor authentication required"
                    });
                }

                // Normal login (no 2FA)
                writeSessionCookie(context, result.SessionToken, result.ExpiresAt);
                logger.LogInformation("User {UserId} logged in successfully", result.User.Id);
                return Results.Json(new AuthResponse(result.User, result.ExpiresAt));
            }
            catch (Exception ex)
            {
                // Top-level API endpoint handler: Catches all exceptions to return HTTP 500
                // Specific exception handling occurs in service layer (AuthService)
                logger.LogError(ex, "Login endpoint error");
                return Results.Problem(detail: ex.Message, statusCode: 500);
            }
        });

        // User logout
        group.MapPost("/auth/logout", async (HttpContext context, AuthService auth, CancellationToken ct) =>
        {
            var sessionCookieName = getSessionCookieName(context);

            if (context.Request.Cookies.TryGetValue(sessionCookieName, out var token) &&
                !string.IsNullOrWhiteSpace(token))
            {
                await auth.LogoutAsync(token, ct);
            }

            removeSessionCookie(context);
            return Results.Json(new { ok = true });
        });

        // Get current user (AUTH-01: Supports both cookie and API key auth)
        group.MapGet("/auth/me", (HttpContext context) =>
        {
            // Support both cookie-based session auth and API key auth
            if (context.Items.TryGetValue(nameof(ActiveSession), out var value) && value is ActiveSession session)
            {
                return Results.Json(new AuthResponse(session.User, session.ExpiresAt));
            }

            // Check for API key authentication
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                var email = context.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
                var displayName = context.User.FindFirst("displayName")?.Value;
                var role = context.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

                if (!string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(email))
                {
                    var user = new AuthUser(userId, email, displayName ?? email, role ?? UserRole.User.ToString());
                    return Results.Json(new AuthResponse(user, null)); // API keys don't have session expiration
                }
            }

            return Results.Unauthorized();
        });

        Map2FAEndpoints(group);
        MapOAuthEndpoints(group, writeSessionCookie);
        MapSessionEndpoints(group, getSessionCookieName);
        MapPasswordResetEndpoints(group, writeSessionCookie);

        return group;
    }

    // AUTH-07: Two-Factor Authentication endpoints
    private static void Map2FAEndpoints(RouteGroupBuilder group)
    {
        group.MapPost("/auth/2fa/setup", async (HttpContext context, ITotpService totpService, ILogger<Program> logger) =>
        {
            var userId = context.User.FindFirst("sub")?.Value;
            var userEmail = context.User.FindFirst("email")?.Value;

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(userEmail))
            {
                return Results.Unauthorized();
            }

            try
            {
                var setup = await totpService.GenerateSetupAsync(userId, userEmail);
                logger.LogInformation("2FA setup generated for user {UserId}", userId);
                return Results.Ok(setup);
            }
            catch (Exception ex)
            {
                // Top-level API endpoint handler: Catches all exceptions to return HTTP 500
                // Specific exception handling occurs in service layer (TotpService)
                logger.LogError(ex, "2FA setup failed for user {UserId}", userId);
                return Results.Problem(detail: ex.Message, statusCode: 500);
            }
        })
        .RequireAuthorization()
        .WithName("Setup2FA")
        .WithTags("Authentication");

        group.MapPost("/auth/2fa/enable", async (TwoFactorEnableRequest request, HttpContext context, ITotpService totpService, ILogger<Program> logger) =>
        {
            var userId = context.User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var success = await totpService.EnableTwoFactorAsync(userId, request.Code);
                if (!success)
                {
                    logger.LogWarning("2FA enable failed: Invalid code for user {UserId}", userId);
                    return Results.BadRequest(new { error = "Invalid verification code" });
                }

                logger.LogInformation("2FA enabled for user {UserId}", userId);
                return Results.Ok(new { message = "Two-factor authentication enabled successfully" });
            }
            catch (Exception ex)
            {
                // Top-level API endpoint handler: Catches all exceptions to return HTTP 500
                // Specific exception handling occurs in service layer (TotpService)
                logger.LogError(ex, "2FA enable error for user {UserId}", userId);
                return Results.Problem(detail: ex.Message, statusCode: 500);
            }
        })
        .RequireAuthorization()
        .WithName("Enable2FA")
        .WithTags("Authentication");

        group.MapPost("/auth/2fa/verify", async (TwoFactorVerifyRequest request, HttpContext context, ITotpService totpService, ITempSessionService tempSessionService, IRateLimitService rateLimitService, AuthService authService, ILogger<Program> logger) =>
        {
            // Rate limit: 3 attempts per minute per session token
            var rateLimitKey = $"2fa:verify:{request.SessionToken}";
            var result = await rateLimitService.CheckRateLimitAsync(rateLimitKey, maxTokens: 3, refillRate: 0.05);

            if (!result.Allowed)
            {
                logger.LogWarning("2FA verify rate limited for session {SessionToken}", request.SessionToken);
                return Results.StatusCode(429);
            }

            try
            {
                // Validate and consume temp session (5-min TTL, single-use)
                var userId = await tempSessionService.ValidateAndConsumeTempSessionAsync(request.SessionToken);
                if (userId == null)
                {
                    logger.LogWarning("2FA verify failed: Invalid temp session");
                    return Results.Unauthorized();
                }

                // Verify TOTP or backup code
                var isValid = await totpService.VerifyCodeAsync(userId, request.Code);
                if (!isValid)
                {
                    isValid = await totpService.VerifyBackupCodeAsync(userId, request.Code);
                }

                if (!isValid)
                {
                    logger.LogWarning("2FA verify failed for user {UserId}", userId);
                    return Results.Unauthorized();
                }

                // Create actual session after 2FA verification
                var loginResult = await authService.CreateSessionForUserAsync(userId,
                    context.Connection.RemoteIpAddress?.ToString(),
                    context.Request.Headers.UserAgent.ToString());
                if (loginResult != null)
                {
                    CookieHelpers.WriteSessionCookie(context, loginResult.SessionToken, loginResult.ExpiresAt);
                    logger.LogInformation("2FA verified, session created for user {UserId}", userId);
                    return Results.Ok(new { message = "2FA verification successful", user = loginResult.User });
                }

                return Results.Problem("Failed to create session after 2FA", statusCode: 500);
            }
            catch (Exception ex)
            {
                // Top-level API endpoint handler: Catches all exceptions to return HTTP 500
                // Specific exception handling occurs in service layer (TotpService, TempSessionService)
                logger.LogError(ex, "2FA verify error");
                return Results.Problem(detail: ex.Message, statusCode: 500);
            }
        })
        .WithName("Verify2FA")
        .WithTags("Authentication");

        group.MapPost("/auth/2fa/disable", async (TwoFactorDisableRequest request, HttpContext context, ITotpService totpService, ILogger<Program> logger) =>
        {
            var userId = context.User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Results.Unauthorized();
            }

            try
            {
                await totpService.DisableTwoFactorAsync(userId, request.Password, request.Code);
                logger.LogInformation("2FA disabled for user {UserId}", userId);
                return Results.Ok(new { message = "Two-factor authentication disabled successfully" });
            }
            catch (UnauthorizedAccessException ex)
            {
                logger.LogWarning("2FA disable unauthorized for user {UserId}: {Message}", userId, ex.Message);
                return Results.Unauthorized();
            }
            catch (Exception ex)
            {
                // Top-level API endpoint handler: Catches all exceptions to return HTTP 500
                // Specific exception handling occurs in service layer (TotpService)
                logger.LogError(ex, "2FA disable error for user {UserId}", userId);
                return Results.Problem(detail: ex.Message, statusCode: 500);
            }
        })
        .RequireAuthorization()
        .WithName("Disable2FA")
        .WithTags("Authentication");

        group.MapGet("/users/me/2fa/status", async (HttpContext context, ITotpService totpService, ILogger<Program> logger) =>
        {
            var userId = context.User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var status = await totpService.GetTwoFactorStatusAsync(userId);
                return Results.Ok(status);
            }
            catch (Exception ex)
            {
                // Top-level API endpoint handler: Catches all exceptions to return HTTP 500
                // Specific exception handling occurs in service layer (TotpService)
                logger.LogError(ex, "Get 2FA status error for user {UserId}", userId);
                return Results.Problem(detail: ex.Message, statusCode: 500);
            }
        })
        .RequireAuthorization()
        .WithName("Get2FAStatus")
        .WithTags("Users");
    }

    // AUTH-06: OAuth 2.0 endpoints (Google, Discord, GitHub)
    private static void MapOAuthEndpoints(RouteGroupBuilder group, Action<HttpContext, string, DateTime> writeSessionCookie)
    {
        group.MapGet("/auth/oauth/{provider}/login", async (
            string provider,
            IOAuthService oauthService,
            HttpContext context,
            IRateLimitService rateLimiter,
            IConfiguration config) =>
        {
            // AUTH-06-P4: Rate limiting to prevent OAuth abuse
            var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var oauthRateLimit = config.GetSection("RateLimit:OAuth").Get<RoleLimitConfiguration>()
                ?? new() { MaxTokens = 10, RefillRate = 0.16667 };

            var rateLimitResult = await rateLimiter.CheckRateLimitAsync(
                $"oauth:login:{ipAddress}",
                oauthRateLimit.MaxTokens,
                oauthRateLimit.RefillRate);

            if (!rateLimitResult.Allowed)
            {
                context.Response.Headers["Retry-After"] = "60";
                return Results.StatusCode(429); // Too Many Requests
            }

            // Generate secure CSRF state
            var state = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
            await oauthService.StoreStateAsync(state);

            var authUrl = await oauthService.GetAuthorizationUrlAsync(provider, state);
            return Results.Redirect(authUrl);
        })
        .WithName("InitiateOAuthLogin")
        .WithTags("Authentication", "OAuth")
        .WithSummary("Initiate OAuth 2.0 login flow")
        .WithDescription(@"Redirects user to OAuth provider (Google, Discord, or GitHub) for authentication.
Generates cryptographically secure CSRF state parameter (32 bytes) with 10-minute expiration.
Rate limited to 10 requests per minute per IP address.

**Supported Providers**: google, discord, github

**Security**: CSRF protection via state parameter, rate limiting prevents abuse.

**Flow**: User clicks OAuth button → Backend redirects to provider → User authorizes → Provider redirects to callback endpoint")
        .Produces(302)
        .Produces(429)
        .Produces(400);

        group.MapGet("/auth/oauth/{provider}/callback", async (
            string provider,
            string code,
            string state,
            IOAuthService oauthService,
            AuthService authService,
            HttpContext context,
            IConfiguration config,
            IRateLimitService rateLimiter,
            CancellationToken ct) =>
        {
            // AUTH-06-P4: Rate limiting on callback to prevent abuse
            var callbackIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var oauthRateLimit = config.GetSection("RateLimit:OAuth").Get<RoleLimitConfiguration>()
                ?? new() { MaxTokens = 10, RefillRate = 0.16667 };

            var rateLimitResult = await rateLimiter.CheckRateLimitAsync(
                $"oauth:callback:{callbackIp}",
                oauthRateLimit.MaxTokens,
                oauthRateLimit.RefillRate);

            if (!rateLimitResult.Allowed)
            {
                var frontendUrl = config["FrontendUrl"] ?? "http://localhost:3000";
                return Results.Redirect($"{frontendUrl}/auth/callback?error=rate_limit");
            }

            try
            {
                var result = await oauthService.HandleCallbackAsync(provider, code, state);

                // Create session for the user
                var sessionIpAddress = context.Connection.RemoteIpAddress?.ToString();
                var userAgent = context.Request.Headers.UserAgent.ToString();
                var authResult = await authService.CreateSessionForUserAsync(result.User.Id, sessionIpAddress, userAgent, ct);

                if (authResult == null)
                {
                    throw new InvalidOperationException("Failed to create session for OAuth user");
                }

                // Set session cookie
                writeSessionCookie(context, authResult.SessionToken, authResult.ExpiresAt);

                // Redirect to frontend with success
                var frontendUrl = config["FrontendUrl"] ?? "http://localhost:3000";
                var redirectUrl = $"{frontendUrl}/auth/callback?success=true&new={result.IsNewUser}";
                return Results.Redirect(redirectUrl);
            }
            catch (Exception ex)
            {
                // Top-level API endpoint handler: Catches all exceptions to return HTTP redirect with error
                // Specific exception handling occurs in service layer (OAuthService)
                var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogError(ex, "OAuth callback failed for provider: {Provider}", provider);

                var frontendUrl = config["FrontendUrl"] ?? "http://localhost:3000";
                var redirectUrl = $"{frontendUrl}/auth/callback?error=oauth_failed";
                return Results.Redirect(redirectUrl);
            }
        })
        .WithName("HandleOAuthCallback")
        .WithTags("Authentication", "OAuth")
        .WithSummary("Handle OAuth 2.0 callback from provider")
        .WithDescription(@"Processes OAuth authorization code from provider and creates user session.
Validates CSRF state parameter (single-use, 10-minute expiration).
Creates new user if email doesn't exist, or links OAuth to existing user by email.
Rate limited to 10 requests per minute per IP address.

**Parameters**:
- `provider`: OAuth provider (google, discord, github)
- `code`: Authorization code from provider
- `state`: CSRF protection state parameter

**Security**: State validation prevents CSRF attacks, tokens encrypted at rest.

**Flow**: Provider redirects here → Validate state → Exchange code for token → Get user info → Create/link account → Create session → Redirect to frontend")
        .Produces(302)
        .Produces(429);

        group.MapDelete("/auth/oauth/{provider}/unlink", async (
            string provider,
            HttpContext context,
            IOAuthService oauthService) =>
        {
            if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
            {
                return Results.Unauthorized();
            }

            await oauthService.UnlinkOAuthAccountAsync(session.User.Id, provider);
            return Results.NoContent();
        })
        .WithName("UnlinkOAuthAccount")
        .WithTags("Authentication", "OAuth", "User Profile")
        .WithSummary("Unlink OAuth provider from user account")
        .WithDescription(@"Removes the specified OAuth provider link from the authenticated user's account.
User must have at least one authentication method remaining (password or another OAuth provider).

**Parameters**:
- `provider`: OAuth provider to unlink (google, discord, github)

**Authorization**: Requires active session (cookie-based authentication).

**Security**: Cannot unlink if it's the only authentication method (prevents account lockout).")
        .Produces(204)
        .Produces(401)
        .Produces(404);

        group.MapGet("/users/me/oauth-accounts", async (
            HttpContext context,
            IOAuthService oauthService) =>
        {
            if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
            {
                return Results.Unauthorized();
            }

            var accounts = await oauthService.GetLinkedAccountsAsync(session.User.Id);
            return Results.Json(accounts);
        })
        .WithName("GetLinkedOAuthAccounts")
        .WithTags("Authentication", "OAuth", "User Profile")
        .WithSummary("Get user's linked OAuth accounts")
        .WithDescription(@"Returns list of OAuth providers linked to the authenticated user's account.

**Authorization**: Requires active session (cookie-based authentication).

**Response**: Array of OAuthAccountDto objects containing:
- `provider`: Provider name (google, discord, github)
- `createdAt`: Timestamp when account was linked")
        .Produces<List<OAuthAccountDto>>(200)
        .Produces(401);
    }

    // AUTH-05: Session management endpoints
    private static void MapSessionEndpoints(RouteGroupBuilder group, Func<HttpContext, string> getSessionCookieName)
    {
        group.MapGet("/auth/session/status", async (
            HttpContext context,
            MeepleAiDbContext db,
            IConfiguration config,
            TimeProvider timeProvider,
            CancellationToken ct) =>
        {
            // Require authentication
            if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
            {
                return Results.Unauthorized();
            }

            var now = timeProvider.GetUtcNow().UtcDateTime;
            var inactivityTimeoutDays = config.GetValue<int>("Authentication:SessionManagement:InactivityTimeoutDays", 30);

            // Get session from database to access LastSeenAt
            var sessionCookieName = getSessionCookieName(context);
            if (!context.Request.Cookies.TryGetValue(sessionCookieName, out var token) || string.IsNullOrWhiteSpace(token))
            {
                return Results.Unauthorized();
            }

            var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
            var tokenHash = Convert.ToBase64String(hash);

            var dbSession = await db.UserSessions
                .FirstOrDefaultAsync(s => s.TokenHash == tokenHash, ct);

            if (dbSession == null || dbSession.RevokedAt != null)
            {
                return Results.Unauthorized();
            }

            // Calculate remaining minutes until session expires from inactivity
            var lastActivity = dbSession.LastSeenAt ?? dbSession.CreatedAt;
            var expiryTime = lastActivity.AddDays(inactivityTimeoutDays);
            var remainingMinutes = (int)Math.Max(0, (expiryTime - now).TotalMinutes);

            var response = new SessionStatusResponse(
                dbSession.ExpiresAt,
                dbSession.LastSeenAt,
                remainingMinutes);

            return Results.Json(response);
        });

        group.MapPost("/auth/session/extend", async (
            HttpContext context,
            MeepleAiDbContext db,
            ISessionCacheService? sessionCache,
            IConfiguration config,
            TimeProvider timeProvider,
            CancellationToken ct) =>
        {
            // Require authentication
            if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
            {
                return Results.Unauthorized();
            }

            var now = timeProvider.GetUtcNow().UtcDateTime;
            var inactivityTimeoutDays = config.GetValue<int>("Authentication:SessionManagement:InactivityTimeoutDays", 30);

            // Get session from database
            var sessionCookieName = getSessionCookieName(context);
            if (!context.Request.Cookies.TryGetValue(sessionCookieName, out var token) || string.IsNullOrWhiteSpace(token))
            {
                return Results.Unauthorized();
            }

            var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
            var tokenHash = Convert.ToBase64String(hash);

            var dbSession = await db.UserSessions
                .FirstOrDefaultAsync(s => s.TokenHash == tokenHash, ct);

            if (dbSession == null || dbSession.RevokedAt != null)
            {
                return Results.Unauthorized();
            }

            // Update LastSeenAt to extend session
            dbSession.LastSeenAt = now;
            await db.SaveChangesAsync(ct);

            // Invalidate cache to force refresh on next request
            if (sessionCache != null)
            {
                await sessionCache.InvalidateAsync(tokenHash, ct);
            }

            // Calculate new remaining minutes
            var expiryTime = now.AddDays(inactivityTimeoutDays);
            var remainingMinutes = (int)Math.Max(0, (expiryTime - now).TotalMinutes);

            var response = new SessionStatusResponse(
                dbSession.ExpiresAt,
                now,
                remainingMinutes);

            return Results.Json(response);
        });

        group.MapGet("/users/me/sessions", async (HttpContext context, ISessionManagementService sessionManagement, CancellationToken ct = default) =>
        {
            if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
            {
                return Results.Unauthorized();
            }

            var sessions = await sessionManagement.GetUserSessionsAsync(session.User.Id, ct);
            return Results.Json(sessions);
        });
    }

    // AUTH-04: Password reset endpoints
    private static void MapPasswordResetEndpoints(RouteGroupBuilder group, Action<HttpContext, string, DateTime> writeSessionCookie)
    {
        group.MapPost("/auth/password-reset/request", async (
            PasswordResetRequestPayload payload,
            IPasswordResetService passwordResetService,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(payload.Email))
                {
                    return Results.BadRequest(new { error = "Email is required" });
                }

                await passwordResetService.RequestPasswordResetAsync(payload.Email, ct);

                // Always return success to prevent email enumeration
                return Results.Json(new { ok = true, message = "If the email exists, a password reset link has been sent" });
            }
            catch (InvalidOperationException ex)
            {
                // Rate limit or validation errors
                logger.LogWarning("Password reset request error: {Message}", ex.Message);
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                // Top-level API endpoint handler: Catches all exceptions to return HTTP 500
                // Specific exception handling occurs in service layer (PasswordResetService)
                logger.LogError(ex, "Password reset request endpoint error");
                return Results.Problem(detail: "An error occurred processing your request", statusCode: 500);
            }
        });

        group.MapGet("/auth/password-reset/verify", async (
            string token,
            IPasswordResetService passwordResetService,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(token))
                {
                    return Results.BadRequest(new { error = "Token is required" });
                }

                var isValid = await passwordResetService.ValidateResetTokenAsync(token, ct);

                if (!isValid)
                {
                    return Results.BadRequest(new { error = "Invalid or expired token" });
                }

                return Results.Json(new { ok = true, message = "Token is valid" });
            }
            catch (Exception ex)
            {
                // Top-level API endpoint handler: Catches all exceptions to return HTTP 500
                // Specific exception handling occurs in service layer (PasswordResetService)
                logger.LogError(ex, "Password reset verify endpoint error");
                return Results.Problem(detail: "An error occurred processing your request", statusCode: 500);
            }
        });

        group.MapPut("/auth/password-reset/confirm", async (
            PasswordResetConfirmPayload payload,
            HttpContext context,
            IPasswordResetService passwordResetService,
            AuthService authService,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(payload.Token))
                {
                    return Results.BadRequest(new { error = "Token is required" });
                }

                if (string.IsNullOrWhiteSpace(payload.NewPassword))
                {
                    return Results.BadRequest(new { error = "New password is required" });
                }

                // Tuple destructuring for userId
                var (success, userId) = await passwordResetService.ResetPasswordAsync(
                    payload.Token,
                    payload.NewPassword,
                    ct);

                if (!success || userId == null)
                {
                    return Results.NotFound(new { error = "Invalid or expired token" });
                }

                // Create new session for auto-login
                var sessionResult = await authService.CreateSessionForUserAsync(
                    userId,
                    context.Connection.RemoteIpAddress?.ToString(),
                    context.Request.Headers.UserAgent.ToString(),
                    ct);

                if (sessionResult != null)
                {
                    writeSessionCookie(context, sessionResult.SessionToken, sessionResult.ExpiresAt);
                }

                return Results.Json(new { ok = true, message = "Password has been reset successfully" });
            }
            catch (ArgumentException ex)
            {
                // Validation errors (password complexity, etc.)
                logger.LogWarning("Password reset confirm validation error: {Message}", ex.Message);
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                // Top-level API endpoint handler: Catches all exceptions to return HTTP 500
                // Specific exception handling occurs in service layer (PasswordResetService)
                logger.LogError(ex, "Password reset confirm endpoint error");
                return Results.Problem(detail: "An error occurred processing your request", statusCode: 500);
            }
        });
    }
}
