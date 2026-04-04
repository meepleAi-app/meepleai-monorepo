using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Api.BoundedContexts.UserNotifications.Application.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Api.BoundedContexts.UserNotifications.Infrastructure.Services;

/// <summary>
/// JWT-based unsubscribe token generator.
/// Tokens expire after 30 days.
/// Issue #38: GDPR-compliant unsubscribe.
/// </summary>
internal class UnsubscribeTokenService : IUnsubscribeTokenService
{
    private readonly IConfiguration _configuration;
    private static readonly TimeSpan TokenExpiry = TimeSpan.FromDays(30);

    public UnsubscribeTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateToken(Guid userId, string notificationType)
    {
        var jwtSecret = _configuration["Jwt:Secret"] ?? _configuration["Authentication:JwtSecret"] ?? "";
        if (string.IsNullOrEmpty(jwtSecret))
            throw new InvalidOperationException("JWT secret not configured");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim("userId", userId.ToString()),
            new Claim("notificationType", notificationType),
            new Claim("purpose", "unsubscribe")
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.Add(TokenExpiry),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateUnsubscribeUrl(Guid userId, string notificationType)
    {
        var token = GenerateToken(userId, notificationType);
#pragma warning disable S1075 // URIs should not be hardcoded - Default/Fallback value
        var baseUrl = _configuration["Authentication:OAuth:CallbackBaseUrl"] ?? _configuration["App:BaseUrl"] ?? "http://localhost:8080";
#pragma warning restore S1075
        return $"{baseUrl}/api/v1/notifications/unsubscribe?token={Uri.EscapeDataString(token)}";
    }
}
