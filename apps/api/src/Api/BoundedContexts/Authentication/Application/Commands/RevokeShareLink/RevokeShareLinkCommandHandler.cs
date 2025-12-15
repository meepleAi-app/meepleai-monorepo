using Api.BoundedContexts.Authentication.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;

namespace Api.BoundedContexts.Authentication.Application.Commands.RevokeShareLink;

/// <summary>
/// Handler for revoking shareable chat thread links.
/// Updates database and adds token to Redis blacklist.
/// </summary>
internal sealed class RevokeShareLinkCommandHandler : IRequestHandler<RevokeShareLinkCommand, bool>
{
    private readonly IShareLinkRepository _shareLinkRepository;
    private readonly IDistributedCache _cache;

    public RevokeShareLinkCommandHandler(
        IShareLinkRepository shareLinkRepository,
        IDistributedCache cache)
    {
        _shareLinkRepository = shareLinkRepository ?? throw new ArgumentNullException(nameof(shareLinkRepository));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public async Task<bool> Handle(
        RevokeShareLinkCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request is null) throw new ArgumentNullException(nameof(request));
        // Load share link via repository
        var shareLink = await _shareLinkRepository.GetByIdAsync(request.ShareLinkId, cancellationToken).ConfigureAwait(false);

        if (shareLink == null || shareLink.CreatorId != request.UserId)
        {
            // Return false if not found or user is not the creator
            return false;
        }

        // Revoke the share link (domain logic)
        shareLink.Revoke();

        // Save changes via repository
        await _shareLinkRepository.UpdateAsync(shareLink, cancellationToken).ConfigureAwait(false);

        // Add token to Redis blacklist
        // Key format: "revoked_share_link:{share_link_id}"
        // TTL: Time until expiration (no need to keep blacklisted after natural expiry)
        var blacklistKey = $"revoked_share_link:{shareLink.Id}";
        var ttl = shareLink.ExpiresAt - DateTime.UtcNow;

        if (ttl > TimeSpan.Zero)
        {
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = shareLink.ExpiresAt
            };

            // Store revocation timestamp as value (for debugging/audit)
            await _cache.SetStringAsync(
                blacklistKey,
                shareLink.RevokedAt!.Value.ToString("O"),
                options,
                cancellationToken).ConfigureAwait(false);
        }

        return true;
    }
}
