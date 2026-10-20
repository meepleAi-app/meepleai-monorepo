using Api.BoundedContexts.Authentication.Domain.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;
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

        // Load share link via repository
        var shareLink = await _shareLinkRepository.GetByIdAsync(request.ShareLinkId, cancellationToken).ConfigureAwait(false);

        if (shareLink == null || shareLink.CreatorId != request.UserId)
        {
            // Return false if not found or user is not the creator
            return false;
        }

        // Un link già revocato alla lettura (#4573): la revoca voluta c'è già nel database, ed è il caso del
        // proprietario che riprova perché la prima volta Redis è fallito. Non si rilegge né si riscrive la riga
        // (una revoca non si riscrive, e Revoke() lancerebbe), ma la blacklist si scrive comunque: è la sola cosa
        // che la prima richiesta può aver lasciato a metà.
        if (!shareLink.IsRevoked)
        {
            // Revoke the share link (domain logic)
            shareLink.Revoke();

            // Save changes via repository
            try
            {
                await _shareLinkRepository.UpdateAsync(shareLink, cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                // UpdateAsync scrive solo su un link non ancora revocato (#4573): 0 righe vuol dire che
                // un'altra richiesta l'ha revocato dopo la nostra lettura. La revoca voluta c'è già, e una
                // revoca ripetuta non è un errore. Si prosegue con la riga com'è: la blacklist si scrive
                // comunque, perché se l'altra richiesta è caduta fra il database e Redis il link resterebbe
                // valido.
                foreach (var entry in ex.Entries)
                {
                    entry.State = EntityState.Detached;
                }

                var current = await _shareLinkRepository.GetByIdAsync(request.ShareLinkId, cancellationToken).ConfigureAwait(false);
                if (current is not { IsRevoked: true })
                {
                    throw;
                }

                shareLink = current;
            }
        }

        // Add token to Redis blacklist
        // Key format: "revoked_share_link:{share_link_id}"
        // TTL: Time until expiration (no need to keep blacklisted after natural expiry)
        var blacklistKey = $"revoked_share_link:{shareLink.Id}";
#pragma warning disable MA0132 // Do not convert implicitly to DateTimeOffset - Intentional DateTime/DateTimeOffset comparison for TTL
        var ttl = shareLink.ExpiresAt - DateTime.UtcNow;
#pragma warning restore MA0132

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
