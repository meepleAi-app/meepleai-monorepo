using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;

namespace Api.BoundedContexts.Authentication.Application.Queries.ValidateShareLink;

/// <summary>
/// Handler for validating share link JWT tokens.
/// Performs signature validation, expiry check, and revocation check via Redis blacklist.
/// </summary>
internal sealed class ValidateShareLinkQueryHandler : IRequestHandler<ValidateShareLinkQuery, ValidateShareLinkResult?>
{
    private readonly IDistributedCache _cache;
    private readonly IConfiguration _configuration;

    public ValidateShareLinkQueryHandler(
        IDistributedCache cache,
        IConfiguration configuration)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public async Task<ValidateShareLinkResult?> Handle(
        ValidateShareLinkQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        // Get JWT secret key from configuration
        var secretKey = _configuration["Jwt:ShareLinks:SecretKey"]
            ?? throw new InvalidOperationException("JWT secret key not configured");

        // Validate JWT token (signature, expiry, claims)
        var token = ShareLinkToken.Validate(request.TokenValue, secretKey);

        if (token == null)
        {
            // Invalid token (bad signature, expired, or malformed)
            return null;
        }

        // Check Redis blacklist for revocation
        var blacklistKey = $"revoked_share_link:{token.ShareLinkId}";
        var isRevoked = await _cache.GetStringAsync(blacklistKey, cancellationToken).ConfigureAwait(false);

        if (isRevoked != null)
        {
            // Token has been revoked
            return new ValidateShareLinkResult(
                ShareLinkId: token.ShareLinkId,
                ThreadId: token.ThreadId,
                Role: token.Role,
                CreatorId: token.CreatorId,
                ExpiresAt: token.ExpiresAt,
                IsValid: false
            );
        }

        // Token is valid and not revoked
        return new ValidateShareLinkResult(
            ShareLinkId: token.ShareLinkId,
            ThreadId: token.ThreadId,
            Role: token.Role,
            CreatorId: token.CreatorId,
            ExpiresAt: token.ExpiresAt,
            IsValid: true
        );
    }
}
