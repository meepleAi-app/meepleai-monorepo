using Api.Configuration;
using Api.Extensions;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using MediatR;
using Microsoft.AspNetCore.Mvc; // For [FromBody] attribute
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

// DDD CQRS imports (using aliases to avoid conflicts with Api.Models)
using DddRegisterCommand = Api.BoundedContexts.Authentication.Application.Commands.RegisterCommand;
using DddLoginCommand = Api.BoundedContexts.Authentication.Application.Commands.LoginCommand;
using DddLogoutCommand = Api.BoundedContexts.Authentication.Application.Commands.LogoutCommand;
using DddCreateSessionCommand = Api.BoundedContexts.Authentication.Application.Commands.CreateSessionCommand;

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

        // User registration (DDD/CQRS)
        group.MapPost("/auth/register", async (RegisterPayload payload, IMediator mediator, HttpContext context, ILogger<Program> logger, CancellationToken ct) =>
        {
            try
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
                var result = await mediator.Send(command, ct);
                writeSessionCookie(context, result.SessionToken, result.ExpiresAt);
                logger.LogInformation("User {UserId} registered successfully with role {Role}", result.User.Id, result.User.Role);

                // Map to legacy AuthResponse for backward compatibility
                var legacyUser = new AuthUser(
                    Id: result.User.Id.ToString(),
                    Email: result.User.Email,
                    DisplayName: result.User.DisplayName,
                    Role: result.User.Role);
                return Results.Json(new AuthResponse(legacyUser, result.ExpiresAt));
            }
            catch (ArgumentException ex)
            {
                // SEC-738: Pass exception object for proper destructuring (CWE-532 prevention)
                logger.LogWarning(ex, "Registration validation failed for {Email}", payload.Email);
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                // SEC-738: Pass exception object for proper destructuring (CWE-532 prevention)
                logger.LogWarning(ex, "Registration conflict for {Email}", payload.Email);
                return Results.Conflict(new { error = ex.Message });
            }
            catch (Api.SharedKernel.Domain.Exceptions.DomainException ex)
            {
                logger.LogWarning(ex, "Registration domain validation failed");
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        // User login with 2FA support (AUTH-07) - DDD CQRS
        group.MapPost("/auth/login", async (HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            // Manual deserialization to support both camelCase and PascalCase JSON
            // ASP.NET Core 9.0 Minimal API auto-deserialization ignores ConfigureHttpJsonOptions
            LoginPayload? payload;
            try
            {
                var jsonOptions = new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true,
                    PropertyNamingPolicy = null
                };
                payload = await context.Request.ReadFromJsonAsync<LoginPayload>(jsonOptions, ct);
            }
            catch (System.Text.Json.JsonException ex)
            {
                logger.LogWarning(ex, "Login failed: invalid JSON payload");
                return Results.BadRequest(new { error = "Invalid JSON format" });
            }

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
                var command = new DddLoginCommand(
                    Email: payload.Email,
                    Password: payload.Password,
                    IpAddress: context.Connection.RemoteIpAddress?.ToString(),
                    UserAgent: context.Request.Headers.UserAgent.ToString());

                logger.LogInformation("Login attempt for {Email}", payload.Email);
                var result = await mediator.Send(command, ct);

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

                // Calculate session expiration (30 days default)
                var expiresAt = DateTime.UtcNow.AddDays(30);
                writeSessionCookie(context, result.SessionToken, expiresAt);
                logger.LogInformation("User {UserId} logged in successfully", result.User.Id);

                // Map to legacy AuthResponse for backward compatibility
                var legacyUser = new AuthUser(
                    Id: result.User.Id.ToString(),
                    Email: result.User.Email,
                    DisplayName: result.User.DisplayName,
                    Role: result.User.Role);

                return Results.Json(new AuthResponse(legacyUser, expiresAt));
            }
            catch (Api.SharedKernel.Domain.Exceptions.DomainException ex)
            {
                logger.LogWarning(ex, "Login domain validation failed for {Email}", payload.Email);
                removeSessionCookie(context);
                return Results.Unauthorized(); // Don't leak information about which part failed
            }
#pragma warning disable CA1031 // Do not catch general exception types
            // Justification: API endpoint boundary - must catch all exceptions to return proper HTTP 500 response
            // All business exceptions are handled in LoginCommandHandler; this catches unexpected infrastructure failures
            catch (Exception ex)
            {
                // Top-level API endpoint handler: Catches all exceptions to return HTTP 500
                // Specific exception handling occurs in command handler (LoginCommandHandler)
                logger.LogError(ex, "Login endpoint error");
                return Results.Problem(detail: ex.Message, statusCode: 500);
            }
#pragma warning restore CA1031
        });

        // User logout - DDD CQRS
        group.MapPost("/auth/logout", async (HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            var sessionCookieName = getSessionCookieName(context);

            if (context.Request.Cookies.TryGetValue(sessionCookieName, out var token) &&
                !string.IsNullOrWhiteSpace(token))
            {
                try
                {
                    var command = new DddLogoutCommand(SessionToken: token);
                    await mediator.Send(command, ct);
                    logger.LogInformation("User logged out successfully");
                }
                catch (Api.SharedKernel.Domain.Exceptions.DomainException ex)
                {
                    // Invalid session token - log but don't fail (allow cookie cleanup)
                    logger.LogWarning(ex, "Logout: Invalid session token");
                }
            }

            removeSessionCookie(context);
            return Results.Json(new { ok = true });
        });

        // Get current user (AUTH-01: Supports both cookie and API key auth)
        // Priority: API key > Cookie session
        group.MapGet("/auth/me", (HttpContext context) =>
        {
            // Check for API key authentication first (higher priority)
            // API keys are identified by the "AuthType" claim with value "ApiKey"
            var authType = context.User.FindFirst("AuthType")?.Value;
            if (authType == "ApiKey" && context.User.Identity?.IsAuthenticated == true)
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

                var user = new AuthUser(userId, email, displayName ?? email, role ?? UserRole.User.ToString());
                return Results.Json(new AuthResponse(user, null)); // API keys don't have session expiration
            }

            // Fall back to cookie-based session auth
            if (context.Items.TryGetValue(nameof(ActiveSession), out var value) && value is ActiveSession session)
            {
                return Results.Json(new AuthResponse(session.User, session.ExpiresAt));
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

            try
            {
                var setup = await totpService.GenerateSetupAsync(userId, userEmail);
                logger.LogInformation("2FA setup generated for user {UserId}", userId);
                return Results.Ok(setup);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            // Justification: API endpoint boundary - must catch all exceptions to return proper HTTP 500 response
            // All business exceptions are handled in TotpService; this catches unexpected infrastructure failures
            catch (Exception ex)
            {
                // Top-level API endpoint handler: Catches all exceptions to return HTTP 500
                // Specific exception handling occurs in service layer (TotpService)
                logger.LogError(ex, "2FA setup failed for user {UserId}", userId);
                return Results.Problem(detail: ex.Message, statusCode: 500);
            }
#pragma warning restore CA1031
        })
        .RequireAuthorization()
        .WithName("Setup2FA")
        .WithTags("Authentication");

        group.MapPost("/auth/2fa/enable", async (TwoFactorEnableRequest request, HttpContext context, ITotpService totpService, ILogger<Program> logger) =>
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
#pragma warning disable CA1031 // Do not catch general exception types
            // Justification: API endpoint boundary - must catch all exceptions to return proper HTTP 500 response
            // All business exceptions are handled in TotpService; this catches unexpected infrastructure failures
            catch (Exception ex)
            {
                // Top-level API endpoint handler: Catches all exceptions to return HTTP 500
                // Specific exception handling occurs in service layer (TotpService)
                logger.LogError(ex, "2FA enable error for user {UserId}", userId);
                return Results.Problem(detail: ex.Message, statusCode: 500);
            }
#pragma warning restore CA1031
        })
        .RequireAuthorization()
        .WithName("Enable2FA")
        .WithTags("Authentication");

        group.MapPost("/auth/2fa/verify", async (TwoFactorVerifyRequest request, HttpContext context, ITotpService totpService, ITempSessionService tempSessionService, IRateLimitService rateLimitService, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
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
                var userIdNullable = await tempSessionService.ValidateAndConsumeTempSessionAsync(request.SessionToken);
                if (userIdNullable == null)
                {
                    logger.LogWarning("2FA verify failed: Invalid temp session");
                    return Results.Unauthorized();
                }

                var userId = userIdNullable.Value;

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

                // Create actual session after 2FA verification - DDD CQRS
                var command = new DddCreateSessionCommand(
                    UserId: userId,
                    IpAddress: context.Connection.RemoteIpAddress?.ToString(),
                    UserAgent: context.Request.Headers.UserAgent.ToString());

                var sessionResult = await mediator.Send(command, ct);

                CookieHelpers.WriteSessionCookie(context, sessionResult.SessionToken, sessionResult.ExpiresAt);
                logger.LogInformation("2FA verified, session created for user {UserId}", userId);

                // Map to legacy format for backward compatibility
                var legacyUser = new AuthUser(
                    Id: sessionResult.User.Id.ToString(),
                    Email: sessionResult.User.Email,
                    DisplayName: sessionResult.User.DisplayName,
                    Role: sessionResult.User.Role);

                return Results.Ok(new { message = "2FA verification successful", user = legacyUser });
            }
#pragma warning disable CA1031 // Do not catch general exception types
            // Justification: API endpoint boundary - must catch all exceptions to return proper HTTP 500 response
            // All business exceptions are handled in TotpService/TempSessionService/CreateSessionCommandHandler; this catches unexpected failures
            catch (Exception ex)
            {
                // Top-level API endpoint handler: Catches all exceptions to return HTTP 500
                // Specific exception handling occurs in service layer (TotpService, TempSessionService) and command handler (CreateSessionCommandHandler)
                logger.LogError(ex, "2FA verify error");
                return Results.Problem(detail: ex.Message, statusCode: 500);
            }
#pragma warning restore CA1031
        })
        .WithName("Verify2FA")
        .WithTags("Authentication");

        group.MapPost("/auth/2fa/disable", async (TwoFactorDisableRequest request, HttpContext context, ITotpService totpService, ILogger<Program> logger) =>
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

            try
            {
                await totpService.DisableTwoFactorAsync(userId, request.Password, request.Code);
                logger.LogInformation("2FA disabled for user {UserId}", userId);
                return Results.Ok(new { message = "Two-factor authentication disabled successfully" });
            }
            catch (UnauthorizedAccessException ex)
            {
                // SEC-738: Pass exception object for proper destructuring (CWE-532 prevention)
                logger.LogWarning(ex, "2FA disable unauthorized for user {UserId}", userId);
                return Results.Unauthorized();
            }
#pragma warning disable CA1031 // Do not catch general exception types
            // Justification: API endpoint boundary - must catch all exceptions to return proper HTTP 500 response
            // All business exceptions are handled in TotpService; this catches unexpected infrastructure failures
            catch (Exception ex)
            {
                // Top-level API endpoint handler: Catches all exceptions to return HTTP 500
                // Specific exception handling occurs in service layer (TotpService)
                logger.LogError(ex, "2FA disable error for user {UserId}", userId);
                return Results.Problem(detail: ex.Message, statusCode: 500);
            }
#pragma warning restore CA1031
        })
        .RequireAuthorization()
        .WithName("Disable2FA")
        .WithTags("Authentication");

        group.MapGet("/users/me/2fa/status", async (HttpContext context, ITotpService totpService, ILogger<Program> logger) =>
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

            try
            {
                var status = await totpService.GetTwoFactorStatusAsync(userId);
                return Results.Ok(status);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            // Justification: API endpoint boundary - must catch all exceptions to return proper HTTP 500 response
            // All business exceptions are handled in TotpService; this catches unexpected infrastructure failures
            catch (Exception ex)
            {
                // Top-level API endpoint handler: Catches all exceptions to return HTTP 500
                // Specific exception handling occurs in service layer (TotpService)
                logger.LogError(ex, "Get 2FA status error for user {UserId}", userId);
                return Results.Problem(detail: ex.Message, statusCode: 500);
            }
#pragma warning restore CA1031
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

        // TODO: Migrate to HandleOAuthCallbackCommand when token encryption fully moved to handlers
        // Currently uses IOAuthService due to complex orchestration with token encryption
        group.MapGet("/auth/oauth/{provider}/callback", async (
            string provider,
            string code,
            string state,
            IOAuthService oauthService,
            IMediator mediator,
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

                // Create session for the user - DDD CQRS
                var sessionIpAddress = context.Connection.RemoteIpAddress?.ToString();
                var userAgent = context.Request.Headers.UserAgent.ToString();

                var command = new DddCreateSessionCommand(
                    UserId: Guid.Parse(result.User.Id),
                    IpAddress: sessionIpAddress,
                    UserAgent: userAgent);

                var sessionResult = await mediator.Send(command, ct);

                // Set session cookie
                writeSessionCookie(context, sessionResult.SessionToken, sessionResult.ExpiresAt);

                // Redirect to frontend with success
                var frontendUrl = config["FrontendUrl"] ?? "http://localhost:3000";
                var redirectUrl = $"{frontendUrl}/auth/callback?success=true&new={result.IsNewUser}";
                return Results.Redirect(redirectUrl);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            // Justification: API endpoint boundary - must catch all exceptions to redirect user with error message
            // All business exceptions are handled in OAuthService; this catches unexpected infrastructure failures
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
#pragma warning restore CA1031
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

        /// <summary>
        /// Unlink OAuth provider from user account (DDD CQRS pattern).
        /// Uses IMediator to send UnlinkOAuthAccountCommand instead of direct service call.
        /// Business logic enforced in handler: Cannot unlink if only auth method (prevents lockout).
        /// </summary>
        group.MapDelete("/auth/oauth/{provider}/unlink", async (
            string provider,
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            var userId = Guid.Parse(session.User.Id);
            var command = new Api.BoundedContexts.Authentication.Application.Commands.OAuth.UnlinkOAuthAccountCommand
            {
                UserId = userId,
                Provider = provider
            };

            var result = await mediator.Send(command);

            if (!result.Success)
            {
                logger.LogWarning("Failed to unlink OAuth account for user {UserId}, provider {Provider}: {ErrorMessage}",
                    userId, provider, result.ErrorMessage);
                return Results.BadRequest(new { error = result.ErrorMessage });
            }

            logger.LogInformation("Successfully unlinked OAuth account for user {UserId}, provider {Provider}",
                userId, provider);
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

**Security**: Cannot unlink if it's the only authentication method (prevents account lockout).

**Implementation**: Uses DDD CQRS pattern with IMediator and UnlinkOAuthAccountCommand.")
        .Produces(204)
        .Produces(400)
        .Produces(401)
        .Produces(404);

        /// <summary>
        /// Get user's linked OAuth accounts (DDD CQRS pattern).
        /// Uses IMediator to send GetLinkedOAuthAccountsQuery instead of direct service call.
        /// Returns list of OAuth providers linked to authenticated user.
        /// </summary>
        group.MapGet("/users/me/oauth-accounts", async (
            HttpContext context,
            IMediator mediator,
            ILogger<Program> logger) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            var userId = Guid.Parse(session.User.Id);
            var query = new Api.BoundedContexts.Authentication.Application.Queries.OAuth.GetLinkedOAuthAccountsQuery
            {
                UserId = userId
            };

            var result = await mediator.Send(query);

            logger.LogInformation("Retrieved {Count} linked OAuth accounts for user {UserId}",
                result.Accounts.Count, userId);
            return Results.Json(result.Accounts);
        })
        .WithName("GetLinkedOAuthAccounts")
        .WithTags("Authentication", "OAuth", "User Profile")
        .WithSummary("Get user's linked OAuth accounts")
        .WithDescription(@"Returns list of OAuth providers linked to the authenticated user's account.

**Authorization**: Requires active session (cookie-based authentication).

**Response**: Array of OAuthAccountDto objects containing:
- `provider`: Provider name (google, discord, github)
- `createdAt`: Timestamp when account was linked

**Implementation**: Uses DDD CQRS pattern with IMediator and GetLinkedOAuthAccountsQuery.")
        .Produces<List<Api.BoundedContexts.Authentication.Application.Queries.OAuth.OAuthAccountDto>>(200)
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
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

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

            if (dbSession == null)
            {
                return Results.Unauthorized();
            }

            if (dbSession.RevokedAt != null)
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
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

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

            if (dbSession == null)
            {
                return Results.Unauthorized();
            }

            if (dbSession.RevokedAt != null)
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
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            var sessions = await sessionManagement.GetUserSessionsAsync(Guid.Parse(session.User.Id), ct);
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
                // SEC-738: Pass exception object for proper destructuring (CWE-532 prevention)
                logger.LogWarning(ex, "Password reset request error");
                return Results.BadRequest(new { error = ex.Message });
            }
#pragma warning disable CA1031 // Do not catch general exception types
            // Justification: API endpoint boundary - must catch all exceptions to return proper HTTP 500 response
            // All business exceptions are handled in PasswordResetService; this catches unexpected failures
            catch (Exception ex)
            {
                // Top-level API endpoint handler: Catches all exceptions to return HTTP 500
                // Specific exception handling occurs in service layer (PasswordResetService)
                logger.LogError(ex, "Password reset request endpoint error");
                return Results.Problem(detail: "An error occurred processing your request", statusCode: 500);
            }
#pragma warning restore CA1031
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
#pragma warning disable CA1031 // Do not catch general exception types
            // Justification: API endpoint boundary - must catch all exceptions to return proper HTTP 500 response
            // All business exceptions are handled in PasswordResetService; this catches unexpected failures
            catch (Exception ex)
            {
                // Top-level API endpoint handler: Catches all exceptions to return HTTP 500
                // Specific exception handling occurs in service layer (PasswordResetService)
                logger.LogError(ex, "Password reset verify endpoint error");
                return Results.Problem(detail: "An error occurred processing your request", statusCode: 500);
            }
#pragma warning restore CA1031
        });

        group.MapPut("/auth/password-reset/confirm", async (
            PasswordResetConfirmPayload payload,
            HttpContext context,
            IPasswordResetService passwordResetService,
            IMediator mediator,
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
                var (success, userIdNullable) = await passwordResetService.ResetPasswordAsync(
                    payload.Token,
                    payload.NewPassword,
                    ct);

                if (!success || userIdNullable == null)
                {
                    return Results.NotFound(new { error = "Invalid or expired token" });
                }

                var userId = userIdNullable.Value;

                // Create new session for auto-login - DDD CQRS
                var command = new DddCreateSessionCommand(
                    UserId: userId,
                    IpAddress: context.Connection.RemoteIpAddress?.ToString(),
                    UserAgent: context.Request.Headers.UserAgent.ToString());

                var sessionResult = await mediator.Send(command, ct);

                writeSessionCookie(context, sessionResult.SessionToken, sessionResult.ExpiresAt);

                return Results.Json(new { ok = true, message = "Password has been reset successfully" });
            }
            catch (ArgumentException ex)
            {
                // Validation errors (password complexity, etc.)
                // SEC-738: Pass exception object for proper destructuring (CWE-532 prevention)
                logger.LogWarning(ex, "Password reset confirm validation error");
                return Results.BadRequest(new { error = ex.Message });
            }
#pragma warning disable CA1031 // Do not catch general exception types
            // Justification: API endpoint boundary - must catch all exceptions to return proper HTTP 500 response
            // All business exceptions are handled in PasswordResetService; this catches unexpected failures
            catch (Exception ex)
            {
                // Top-level API endpoint handler: Catches all exceptions to return HTTP 500
                // Specific exception handling occurs in service layer (PasswordResetService)
                logger.LogError(ex, "Password reset confirm endpoint error");
                return Results.Problem(detail: "An error occurred processing your request", statusCode: 500);
            }
#pragma warning restore CA1031
        });
    }
}
