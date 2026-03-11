using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Api.BoundedContexts.UserNotifications.Application.Commands;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Api.Routing;

/// <summary>
/// Public endpoint for email unsubscribe (no auth required - token-based).
/// Issue #38: GDPR-compliant unsubscribe with JWT token.
/// </summary>
internal static class UnsubscribeEndpoints
{
    public static void MapUnsubscribeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/notifications/unsubscribe", HandleUnsubscribe)
            .WithTags("Notifications - Unsubscribe")
            .WithName("UnsubscribeFromEmail")
            .WithSummary("One-click email unsubscribe (GDPR)")
            .AllowAnonymous();
    }

    private static async Task<IResult> HandleUnsubscribe(
        string token,
        IMediator mediator,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
            return Results.Content(GenerateHtmlPage("Invalid Request", "Missing unsubscribe token."), "text/html");

        try
        {
            // Validate JWT token
            var jwtSecret = configuration["Jwt:Secret"] ?? configuration["Authentication:JwtSecret"] ?? "";
            if (string.IsNullOrEmpty(jwtSecret))
            {
                return Results.Content(GenerateHtmlPage("Error", "Service configuration error."), "text/html");
            }

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(jwtSecret);

            var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(5)
            }, out _);

            // Validate purpose claim to prevent cross-token attacks (e.g., auth JWT used as unsubscribe token)
            var purposeClaim = principal.FindFirst("purpose")?.Value;
            if (!string.Equals(purposeClaim, "unsubscribe", StringComparison.Ordinal))
                return Results.Content(GenerateHtmlPage("Invalid Token", "The unsubscribe link is invalid."), "text/html");

            var userIdClaim = principal.FindFirst("userId")?.Value ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var notificationTypeClaim = principal.FindFirst("notificationType")?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                return Results.Content(GenerateHtmlPage("Invalid Token", "The unsubscribe link is invalid."), "text/html");

            var notificationType = notificationTypeClaim ?? "all";

            var result = await mediator.Send(
                new UnsubscribeEmailCommand(userId, notificationType),
                cancellationToken).ConfigureAwait(false);

            return result
                ? Results.Content(GenerateHtmlPage("Unsubscribed", $"You have been unsubscribed from {notificationType.Replace('_', ' ')} email notifications."), "text/html")
                : Results.Content(GenerateHtmlPage("Error", "Could not process your unsubscribe request."), "text/html");
        }
        catch (SecurityTokenExpiredException)
        {
            return Results.Content(GenerateHtmlPage("Link Expired", "This unsubscribe link has expired. Please use a more recent email or manage your preferences in the app."), "text/html");
        }
#pragma warning disable CA1031
        catch (Exception)
#pragma warning restore CA1031
        {
            return Results.Content(GenerateHtmlPage("Error", "Could not process your unsubscribe request. The link may be invalid."), "text/html");
        }
    }

    private static string GenerateHtmlPage(string title, string message)
    {
        return $$"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
            <meta charset="UTF-8">
            <meta name="viewport" content="width=device-width, initial-scale=1.0">
            <title>MeepleAI - {{title}}</title>
            <style>
                body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; display: flex; justify-content: center; align-items: center; min-height: 100vh; margin: 0; background: #f8f9fa; color: #333; }
                .card { background: white; border-radius: 12px; padding: 48px; max-width: 480px; text-align: center; box-shadow: 0 2px 8px rgba(0,0,0,0.08); }
                h1 { font-size: 24px; margin-bottom: 16px; }
                p { color: #666; line-height: 1.6; }
                .logo { font-size: 32px; margin-bottom: 24px; }
            </style>
        </head>
        <body>
            <div class="card">
                <div class="logo">&#127922;</div>
                <h1>{{title}}</h1>
                <p>{{message}}</p>
            </div>
        </body>
        </html>
        """;
    }
}
