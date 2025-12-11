using Api.Configuration;
using Api.Extensions;
using Api.Services;
using MediatR;

// DDD CQRS imports
using InitiateOAuthLoginCommand = Api.BoundedContexts.Authentication.Application.Commands.OAuth.InitiateOAuthLoginCommand;
using HandleOAuthCallbackCommand = Api.BoundedContexts.Authentication.Application.Commands.OAuth.HandleOAuthCallbackCommand;

namespace Api.Routing;

/// <summary>
/// OAuth 2.0 authentication endpoints.
/// Handles OAuth provider integrations (Google, Discord, GitHub), account linking, and unlinking.
/// </summary>
public static class OAuthEndpoints
{
    // AUTH-06: OAuth 2.0 endpoints (Google, Discord, GitHub)
    public static RouteGroupBuilder MapOAuthEndpoints(this RouteGroupBuilder group, Action<HttpContext, string, DateTime> writeSessionCookie)
    {
        group.MapGet("/auth/oauth/{provider}/login", async (
            string provider,
            IMediator mediator,
            HttpContext context,
            IRateLimitService rateLimiter,
            IConfigurationService configService,
            CancellationToken ct) =>
        {
            // AUTH-06-P4: Rate limiting to prevent OAuth abuse (configurable via admin UI)
            var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var maxTokens = (await configService.GetValueAsync<int?>("RateLimit:OAuth:MaxTokens", 10).ConfigureAwait(false)) ?? 10;
            var refillRate = (await configService.GetValueAsync<double?>("RateLimit:OAuth:RefillRate", 0.16667).ConfigureAwait(false)) ?? 0.16667;

            var rateLimitResult = await rateLimiter.CheckRateLimitAsync(
                $"oauth:login:{ipAddress}",
                maxTokens,
                refillRate).ConfigureAwait(false);

            if (!rateLimitResult.Allowed)
            {
                context.Response.Headers["Retry-After"] = "60";
                return Results.StatusCode(429); // Too Many Requests
            }

            // Execute OAuth login initiation via CQRS handler
            var command = new InitiateOAuthLoginCommand
            {
                Provider = provider,
                IpAddress = ipAddress
            };

            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            if (!result.Success)
            {
                return Results.BadRequest(new { error = result.ErrorMessage });
            }

            return Results.Redirect(result.AuthorizationUrl!);
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
            var maxTokens = (await configService.GetValueAsync<int?>("RateLimit:OAuth:MaxTokens", 10).ConfigureAwait(false)) ?? 10;
            var refillRate = (await configService.GetValueAsync<double?>("RateLimit:OAuth:RefillRate", 0.16667).ConfigureAwait(false)) ?? 0.16667;

            var rateLimitResult = await rateLimiter.CheckRateLimitAsync(
                $"oauth:callback:{callbackIp}",
                maxTokens,
                refillRate).ConfigureAwait(false);

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

            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            if (!result.Success)
            {
                // Handler returned business logic error
                var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogWarning("OAuth callback failed: {ErrorMessage}", result.ErrorMessage);

                var frontendUrl = config["FrontendUrl"] ?? "http://localhost:3000";
                return Results.Redirect($"{frontendUrl}/auth/callback?error=oauth_failed");
            }

            // Set session cookie - get expiration from configuration
            var sessionExpirationDays = (await configService.GetValueAsync<int?>("Authentication:SessionManagement:SessionExpirationDays", 30).ConfigureAwait(false)) ?? 30;
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

            var userId = session!.User!.Id;
            var command = new Api.BoundedContexts.Authentication.Application.Commands.OAuth.UnlinkOAuthAccountCommand
            {
                UserId = userId,
                Provider = provider
            };

            var result = await mediator.Send(command).ConfigureAwait(false);

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

            var userId = session!.User!.Id;
            var query = new Api.BoundedContexts.Authentication.Application.Queries.OAuth.GetLinkedOAuthAccountsQuery
            {
                UserId = userId
            };

            var result = await mediator.Send(query).ConfigureAwait(false);

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

        return group;
    }
}
