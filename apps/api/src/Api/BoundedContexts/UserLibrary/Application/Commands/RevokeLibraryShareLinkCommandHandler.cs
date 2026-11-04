using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.UserLibrary.Application.Commands;

/// <summary>
/// Handler for revoking (deleting) a library share link.
/// </summary>
internal class RevokeLibraryShareLinkCommandHandler : ICommandHandler<RevokeLibraryShareLinkCommand>
{
    private readonly ILibraryShareLinkRepository _shareLinkRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RevokeLibraryShareLinkCommandHandler> _logger;

    public RevokeLibraryShareLinkCommandHandler(
        ILibraryShareLinkRepository shareLinkRepository,
        IUnitOfWork unitOfWork,
        ILogger<RevokeLibraryShareLinkCommandHandler> logger)
    {
        _shareLinkRepository = shareLinkRepository ?? throw new ArgumentNullException(nameof(shareLinkRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(RevokeLibraryShareLinkCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var shareLink = await _shareLinkRepository.GetByShareTokenAsync(command.ShareToken, cancellationToken).ConfigureAwait(false);

        if (shareLink == null)
        {
            throw new NotFoundException($"Share link with token '{command.ShareToken}' not found");
        }

        // Verify ownership
        if (shareLink.UserId != command.UserId)
        {
            _logger.LogWarning(
                "User {UserId} attempted to revoke share link owned by {OwnerId}",
                command.UserId, shareLink.UserId);
            throw new NotFoundException($"Share link with token '{command.ShareToken}' not found");
        }

        if (shareLink.IsRevoked)
        {
            _logger.LogInformation("Share link {LinkId} is already revoked", shareLink.Id);
            return;
        }

        shareLink.Revoke();
        await _shareLinkRepository.UpdateAsync(shareLink, cancellationToken).ConfigureAwait(false);
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // UpdateAsync scrive solo su un link non ancora revocato (#4558): 0 righe vuol dire che un'altra
            // richiesta l'ha revocato dopo la nostra lettura. La revoca voluta c'è già, e DELETE è idempotente.
            foreach (var entry in ex.Entries)
            {
                entry.State = EntityState.Detached;
            }

            var current = await _shareLinkRepository.GetByShareTokenAsync(command.ShareToken, cancellationToken).ConfigureAwait(false);
            if (current is not { IsRevoked: true })
            {
                throw;
            }

            _logger.LogInformation(ex, "Share link {LinkId} was revoked by a concurrent request", shareLink.Id);
            return;
        }

        _logger.LogInformation("Revoked share link {LinkId} for user {UserId}", shareLink.Id, command.UserId);
    }
}
