using System.Security.Claims;
using Api.Services;

namespace Api.Middleware;

/// <summary>
/// Middleware that authenticates requests using API keys from the X-API-Key header.
/// Runs before authorization middleware and sets up ClaimsPrincipal for API key-based requests.
/// Falls through to cookie authentication if no API key is provided.
/// </summary>
public class ApiKeyAuthenticationMiddleware
{
    private const string ApiKeyHeaderName = "X-API-Key";
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

        // Check for API key in header
        if (context.Request.Headers.TryGetValue(ApiKeyHeaderName, out var apiKeyValues))
        {
            var apiKey = apiKeyValues.FirstOrDefault();
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
                        new("AuthType", "ApiKey")
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
                        "API key authentication successful. UserId: {UserId}, ApiKeyId: {ApiKeyId}, Path: {Path}",
                        result.UserId,
                        result.ApiKeyId,
                        context.Request.Path);

                    await _next(context);
                    return;
                }
                else
                {
                    // Invalid API key
                    _logger.LogWarning(
                        "API key authentication failed: {Reason}. Path: {Path}, IP: {IP}",
                        result.InvalidReason,
                        context.Request.Path,
                        context.Connection.RemoteIpAddress);

                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsJsonAsync(new
                    {
                        error = "invalid_api_key",
                        message = result.InvalidReason ?? "Invalid or expired API key",
                        correlationId = context.TraceIdentifier
                    });
                    return;
                }
            }
        }

        // No API key provided - fall through to cookie authentication
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
