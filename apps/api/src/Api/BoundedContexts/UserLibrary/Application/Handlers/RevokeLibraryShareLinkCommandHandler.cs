using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.UserLibrary.Application.Handlers;

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
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Revoked share link {LinkId} for user {UserId}", shareLink.Id, command.UserId);
    }
}
