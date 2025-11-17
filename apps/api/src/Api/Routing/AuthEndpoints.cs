using Api.Configuration;
using Api.Extensions;
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
using HandleOAuthCallbackCommand = Api.BoundedContexts.Authentication.Application.Commands.OAuth.HandleOAuthCallbackCommand;
using GetSessionStatusQuery = Api.BoundedContexts.Authentication.Application.Queries.GetSessionStatusQuery;
using ExtendSessionCommand = Api.BoundedContexts.Authentication.Application.Commands.ExtendSessionCommand;
using GetUserSessionsQuery = Api.BoundedContexts.Authentication.Application.Queries.GetUserSessionsQuery;

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
        });

        // User login with 2FA support (AUTH-07) - DDD CQRS
        group.MapPost("/auth/login", async (HttpContext context, IMediator mediator, IConfigurationService configService, ILogger<Program> logger, CancellationToken ct) =>
        {
            // Manual deserialization to support both camelCase and PascalCase JSON
            // ASP.NET Core 9.0 Minimal API auto-deserialization ignores ConfigureHttpJsonOptions
            var jsonOptions = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                PropertyNamingPolicy = null
            };
            var payload = await context.Request.ReadFromJsonAsync<LoginPayload>(jsonOptions, ct);

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

            // Calculate session expiration from configuration (default: 30 days)
            var sessionExpirationDays = await configService.GetValueAsync<int?>("Authentication:SessionManagement:SessionExpirationDays", 30) ?? 30;
            var expiresAt = DateTime.UtcNow.AddDays(sessionExpirationDays);
            writeSessionCookie(context, result.SessionToken, expiresAt);
            logger.LogInformation("User {UserId} logged in successfully", result.User.Id);

            // Map to legacy AuthResponse for backward compatibility
            var legacyUser = new AuthUser(
                Id: result.User.Id.ToString(),
                Email: result.User.Email,
                DisplayName: result.User.DisplayName,
                Role: result.User.Role);

            return Results.Json(new AuthResponse(legacyUser, expiresAt));
        });

        // User logout - DDD CQRS
        group.MapPost("/auth/logout", async (HttpContext context, IMediator mediator, ILogger<Program> logger, CancellationToken ct) =>
        {
            var sessionCookieName = getSessionCookieName(context);

            if (context.Request.Cookies.TryGetValue(sessionCookieName, out var token) &&
                !string.IsNullOrWhiteSpace(token))
            {
                var command = new DddLogoutCommand(SessionToken: token);
                await mediator.Send(command, ct);
                logger.LogInformation("User logged out successfully");
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

            var setup = await totpService.GenerateSetupAsync(userId, userEmail);
            logger.LogInformation("2FA setup generated for user {UserId}", userId);
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

            var result = await mediator.Send(command, ct);

            if (!result.Success)
            {
                logger.LogWarning("2FA enable failed for user {UserId}: {ErrorMessage}", userId, result.ErrorMessage);
                return Results.BadRequest(new { error = result.ErrorMessage ?? "Invalid verification code" });
            }

            logger.LogInformation("2FA enabled for user {UserId}", userId);
            return Results.Ok(new
            {
                message = "Two-factor authentication enabled successfully",
                backupCodes = result.BackupCodes // Return backup codes for user to save
            });
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

            var result = await mediator.Send(command, ct);

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

            var status = await mediator.Send(query, ct);

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

    // AUTH-06: OAuth 2.0 endpoints (Google, Discord, GitHub)
    private static void MapOAuthEndpoints(RouteGroupBuilder group, Action<HttpContext, string, DateTime> writeSessionCookie)
    {
        group.MapGet("/auth/oauth/{provider}/login", async (
            string provider,
            IOAuthService oauthService,
            HttpContext context,
            IRateLimitService rateLimiter,
            IConfigurationService configService) =>
        {
            // AUTH-06-P4: Rate limiting to prevent OAuth abuse (configurable via admin UI)
            var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var maxTokens = await configService.GetValueAsync<int?>("RateLimit:OAuth:MaxTokens", 10) ?? 10;
            var refillRate = await configService.GetValueAsync<double?>("RateLimit:OAuth:RefillRate", 0.16667) ?? 0.16667;

            var rateLimitResult = await rateLimiter.CheckRateLimitAsync(
                $"oauth:login:{ipAddress}",
                maxTokens,
                refillRate);

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

        // OAuth Callback - Full CQRS implementation via HandleOAuthCallbackCommand
        group.MapGet("/auth/oauth/{provider}/callback", async (
            string provider,
            string code,
            string state,
            IMediator mediator,
            HttpContext context,
            IConfigurationService configService,
            IConfiguration config,
            IRateLimitService rateLimiter,
            CancellationToken ct) =>
        {
            // AUTH-06-P4: Rate limiting on callback to prevent abuse (configurable via admin UI)
            var callbackIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var maxTokens = await configService.GetValueAsync<int?>("RateLimit:OAuth:MaxTokens", 10) ?? 10;
            var refillRate = await configService.GetValueAsync<double?>("RateLimit:OAuth:RefillRate", 0.16667) ?? 0.16667;

            var rateLimitResult = await rateLimiter.CheckRateLimitAsync(
                $"oauth:callback:{callbackIp}",
                maxTokens,
                refillRate);

            if (!rateLimitResult.Allowed)
            {
                var frontendUrl = config["FrontendUrl"] ?? "http://localhost:3000";
                return Results.Redirect($"{frontendUrl}/auth/callback?error=rate_limit");
            }

            // Execute OAuth callback via CQRS handler
            var sessionIpAddress = context.Connection.RemoteIpAddress?.ToString();
            var userAgent = context.Request.Headers.UserAgent.ToString();

            var command = new HandleOAuthCallbackCommand
            {
                Provider = provider,
                Code = code,
                State = state,
                IpAddress = sessionIpAddress,
                UserAgent = userAgent
            };

            var result = await mediator.Send(command, ct);

            if (!result.Success)
            {
                // Handler returned business logic error
                var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogWarning("OAuth callback failed: {ErrorMessage}", result.ErrorMessage);

                var frontendUrl = config["FrontendUrl"] ?? "http://localhost:3000";
                return Results.Redirect($"{frontendUrl}/auth/callback?error=oauth_failed");
            }

            // Set session cookie - get expiration from configuration
            var sessionExpirationDays = await configService.GetValueAsync<int?>("Authentication:SessionManagement:SessionExpirationDays", 30) ?? 30;
            var expiresAt = DateTime.UtcNow.AddDays(sessionExpirationDays);
            writeSessionCookie(context, result.SessionToken ?? string.Empty, expiresAt);

            // Redirect to frontend with success
            var successFrontendUrl = config["FrontendUrl"] ?? "http://localhost:3000";
            var redirectUrl = $"{successFrontendUrl}/auth/callback?success=true&new={result.IsNewUser}";
            return Results.Redirect(redirectUrl);
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
            IMediator mediator,
            IConfiguration config,
            CancellationToken ct) =>
        {
            // Require authentication
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            var inactivityTimeoutDays = config.GetValue<int>("Authentication:SessionManagement:InactivityTimeoutDays", 30);

            // Get session token hash from cookie
            var sessionCookieName = getSessionCookieName(context);
            if (!context.Request.Cookies.TryGetValue(sessionCookieName, out var token) || string.IsNullOrWhiteSpace(token))
            {
                return Results.Unauthorized();
            }

            var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
            var tokenHash = Convert.ToBase64String(hash);

            // Use CQRS Query to get session status
            var query = new GetSessionStatusQuery(tokenHash, inactivityTimeoutDays);
            var response = await mediator.Send(query, ct);

            if (response == null)
            {
                return Results.Unauthorized();
            }

            return Results.Json(response);
        });

        group.MapPost("/auth/session/extend", async (
            HttpContext context,
            IMediator mediator,
            IConfiguration config,
            CancellationToken ct) =>
        {
            // Require authentication
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            var inactivityTimeoutDays = config.GetValue<int>("Authentication:SessionManagement:InactivityTimeoutDays", 30);

            // Get session token hash from cookie
            var sessionCookieName = getSessionCookieName(context);
            if (!context.Request.Cookies.TryGetValue(sessionCookieName, out var token) || string.IsNullOrWhiteSpace(token))
            {
                return Results.Unauthorized();
            }

            var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
            var tokenHash = Convert.ToBase64String(hash);

            // Use CQRS Command to extend session
            var command = new ExtendSessionCommand(tokenHash, inactivityTimeoutDays);
            var response = await mediator.Send(command, ct);

            if (response == null)
            {
                return Results.Unauthorized();
            }

            return Results.Json(response);
        });

        group.MapGet("/users/me/sessions", async (HttpContext context, IMediator mediator, CancellationToken ct = default) =>
        {
            var (authenticated, session, error) = context.TryGetActiveSession();
            if (!authenticated) return error!;

            var query = new GetUserSessionsQuery(Guid.Parse(session.User.Id));
            var sessions = await mediator.Send(query, ct);
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
            if (string.IsNullOrWhiteSpace(payload.Email))
            {
                return Results.BadRequest(new { error = "Email is required" });
            }

            await passwordResetService.RequestPasswordResetAsync(payload.Email, ct);

            // Always return success to prevent email enumeration
            return Results.Json(new { ok = true, message = "If the email exists, a password reset link has been sent" });
        });

        group.MapGet("/auth/password-reset/verify", async (
            string token,
            IPasswordResetService passwordResetService,
            ILogger<Program> logger,
            CancellationToken ct) =>
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
        });

        group.MapPut("/auth/password-reset/confirm", async (
            PasswordResetConfirmPayload payload,
            HttpContext context,
            IPasswordResetService passwordResetService,
            IMediator mediator,
            ILogger<Program> logger,
            CancellationToken ct) =>
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
        });
    }
}
