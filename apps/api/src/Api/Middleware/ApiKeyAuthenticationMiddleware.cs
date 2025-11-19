using System.Security.Claims;
using Api.Services;

namespace Api.Middleware;

/// <summary>
/// Middleware that authenticates requests using API keys from the secure cookie, Authorization header, or legacy X-API-Key header.
/// Runs before authorization middleware and sets up ClaimsPrincipal for API key-based requests.
/// Falls through to cookie authentication if no API key is provided.
/// </summary>
public class ApiKeyAuthenticationMiddleware
{
    private const string AuthorizationHeaderName = "Authorization";
    private const string LegacyApiKeyHeaderName = "X-API-Key";
    private const string ApiKeyCookieName = "meeple_apikey";
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyAuthenticationMiddleware> _logger;

    public ApiKeyAuthenticationMiddleware(
        RequestDelegate next,
        ILogger<ApiKeyAuthenticationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ApiKeyAuthenticationService apiKeyService)
    {
        // Only process /api/* paths (skip health checks, swagger, etc.)
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await _next(context);
            return;
        }

        string? apiKey = null;
        string source = "none";

        // Priority 1: httpOnly cookie (highest protection for browsers)
        if (context.Request.Cookies.TryGetValue(ApiKeyCookieName, out var cookieApiKey))
        {
            if (!string.IsNullOrWhiteSpace(cookieApiKey))
            {
                apiKey = cookieApiKey;
                source = "cookie";
            }
        }

        // Priority 2: Authorization header (ApiKey scheme)
        if (apiKey == null && context.Request.Headers.TryGetValue(AuthorizationHeaderName, out var authValues))
        {
            var authValue = authValues.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(authValue) && authValue.StartsWith("ApiKey ", StringComparison.OrdinalIgnoreCase))
            {
                apiKey = authValue["ApiKey ".Length..].Trim();
                source = "authorization-header";
            }
        }

        // Priority 3: Legacy X-API-Key header
        if (apiKey == null && context.Request.Headers.TryGetValue(LegacyApiKeyHeaderName, out var apiKeyValues))
        {
            var headerApiKey = apiKeyValues.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(headerApiKey))
            {
                apiKey = headerApiKey;
                source = "legacy-header";
            }
        }

        // If we have an API key, validate it
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            var result = await apiKeyService.ValidateApiKeyAsync(apiKey);

            if (result.IsValid)
            {
                // Set ClaimsPrincipal for API key authentication
                var claims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, result.UserId!),
                    new(ClaimTypes.Email, result.UserEmail!),
                    new(ClaimTypes.Role, result.UserRole!),
                    new("ApiKeyId", result.ApiKeyId!),
                    new("AuthType", "ApiKey"),
                    new("AuthSource", source) // Track where the API key came from
                };

                // Add display name if available
                if (!string.IsNullOrWhiteSpace(result.UserDisplayName))
                {
                    claims.Add(new Claim(ClaimTypes.Name, result.UserDisplayName));
                }

                // Add scope claims for fine-grained authorization
                foreach (var scope in result.Scopes)
                {
                    claims.Add(new Claim("scope", scope));
                }

                var identity = new ClaimsIdentity(claims, "ApiKey");
                context.User = new ClaimsPrincipal(identity);

                _logger.LogInformation(
                    "API key authentication successful from {Source}. UserId: {UserId}, ApiKeyId: {ApiKeyId}, Path: {Path}",
                    source,
                    result.UserId,
                    result.ApiKeyId,
                    LogValueSanitizer.SanitizePath(context.Request.Path));

                await _next(context);
                return;
            }
            else
            {
                // Invalid API key
                _logger.LogWarning(
                    "API key authentication failed from {Source}: {Reason}. Path: {Path}, IP: {IP}",
                    source,
                    result.InvalidReason,
                    LogValueSanitizer.SanitizePath(context.Request.Path),
                    context.Connection.RemoteIpAddress);

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "invalid_api_key",
                    message = result.InvalidReason ?? "Invalid or expired API key",
                    source = source,
                    correlationId = context.TraceIdentifier
                });
                return;
            }
        }

        // No API key provided - fall through to session cookie authentication
        await _next(context);
    }
}

/// <summary>
/// Extension methods for registering API key authentication middleware.
/// </summary>
public static class ApiKeyAuthenticationMiddlewareExtensions
{
    /// <summary>
    /// Adds API key authentication middleware to the pipeline.
    /// Should be called before UseAuthorization().
    /// </summary>
    public static IApplicationBuilder UseApiKeyAuthentication(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ApiKeyAuthenticationMiddleware>();
    }
}
