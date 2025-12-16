using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Api.BoundedContexts.Authentication.Domain.ValueObjects;

/// <summary>
/// Value Object representing a JWT token for shareable chat links.
/// Encapsulates token generation, validation, and claims management.
/// </summary>
internal sealed class ShareLinkToken
{
    private const string ThreadIdClaimType = "thread_id";
    private const string RoleClaimType = "role";
    private const string CreatorIdClaimType = "creator_id";
    private const string ShareLinkIdClaimType = "share_link_id";

    public string Value { get; }
    public Guid ThreadId { get; }
    public ShareLinkRole Role { get; }
    public Guid CreatorId { get; }
    public Guid ShareLinkId { get; }
    public DateTime ExpiresAt { get; }

    private ShareLinkToken(
        string value,
        Guid threadId,
        ShareLinkRole role,
        Guid creatorId,
        Guid shareLinkId,
        DateTime expiresAt)
    {
        Value = value;
        ThreadId = threadId;
        Role = role;
        CreatorId = creatorId;
        ShareLinkId = shareLinkId;
        ExpiresAt = expiresAt;
    }

    /// <summary>
    /// Generates a new JWT token for a shareable chat link.
    /// </summary>
    /// <param name="shareLinkId">Unique identifier for the share link (for revocation tracking)</param>
    /// <param name="threadId">Chat thread identifier</param>
    /// <param name="role">Access level (view or comment)</param>
    /// <param name="creatorId">User who created the share link</param>
    /// <param name="expiresAt">Token expiration timestamp</param>
    /// <param name="secretKey">Secret key for HMAC-SHA256 signing</param>
    /// <returns>ShareLinkToken instance with generated JWT</returns>
    public static ShareLinkToken Generate(
        Guid shareLinkId,
        Guid threadId,
        ShareLinkRole role,
        Guid creatorId,
        DateTime expiresAt,
        string secretKey)
    {
        if (string.IsNullOrWhiteSpace(secretKey))
            throw new ArgumentException("Secret key cannot be null or empty", nameof(secretKey));

        if (expiresAt <= DateTime.UtcNow)
            throw new ArgumentException("Expiration must be in the future", nameof(expiresAt));

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)),
            new Claim(ShareLinkIdClaimType, shareLinkId.ToString()),
            new Claim(ThreadIdClaimType, threadId.ToString()),
            new Claim(RoleClaimType, role.ToString()),
            new Claim(CreatorIdClaimType, creatorId.ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "MeepleAI",
            audience: "MeepleAI.ShareLinks",
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials
        );

        var tokenValue = new JwtSecurityTokenHandler().WriteToken(token);

        return new ShareLinkToken(
            tokenValue,
            threadId,
            role,
            creatorId,
            shareLinkId,
            expiresAt
        );
    }

    /// <summary>
    /// Validates and parses a JWT token string.
    /// </summary>
    /// <param name="tokenValue">JWT token string</param>
    /// <param name="secretKey">Secret key for signature validation</param>
    /// <returns>ShareLinkToken instance if valid, null otherwise</returns>
    public static ShareLinkToken? Validate(string tokenValue, string secretKey)
    {
        if (string.IsNullOrWhiteSpace(tokenValue) || string.IsNullOrWhiteSpace(secretKey))
            return null;

        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = true,
                ValidIssuer = "MeepleAI",
                ValidateAudience = true,
                ValidAudience = "MeepleAI.ShareLinks",
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            tokenHandler.ValidateToken(tokenValue, validationParameters, out var validatedToken);

            if (validatedToken is not JwtSecurityToken jwtToken)
                return null;

            // Read claims directly from JwtSecurityToken to avoid claim type mapping issues
            var shareLinkId = Guid.Parse(GetClaimFromToken(jwtToken, ShareLinkIdClaimType));
            var threadId = Guid.Parse(GetClaimFromToken(jwtToken, ThreadIdClaimType));
            var role = Enum.Parse<ShareLinkRole>(GetClaimFromToken(jwtToken, RoleClaimType));
            var creatorId = Guid.Parse(GetClaimFromToken(jwtToken, CreatorIdClaimType));
            var expiresAt = jwtToken.ValidTo;

            return new ShareLinkToken(
                tokenValue,
                threadId,
                role,
                creatorId,
                shareLinkId,
                expiresAt
            );
        }
        catch
        {
            return null;
        }
    }

    private static string GetClaimFromToken(JwtSecurityToken token, string claimType)
    {
        return token.Claims.FirstOrDefault(c => string.Equals(c.Type, claimType, StringComparison.Ordinal))?.Value
            ?? throw new InvalidOperationException($"Claim '{claimType}' not found in token");
    }

    public override string ToString() => Value;

    public override bool Equals(object? obj) =>
        obj is ShareLinkToken other && string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);
}
