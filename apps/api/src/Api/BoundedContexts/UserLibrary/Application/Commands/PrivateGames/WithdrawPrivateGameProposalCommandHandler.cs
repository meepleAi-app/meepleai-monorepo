using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.UserLibrary.Application.Commands.PrivateGames;

/// <summary>
/// Handler for <see cref="WithdrawPrivateGameProposalCommand"/> (#5079).
/// </summary>
internal sealed class WithdrawPrivateGameProposalCommandHandler : ICommandHandler<WithdrawPrivateGameProposalCommand>
{
    private readonly IPrivateGameRepository _privateGameRepository;
    private readonly IShareRequestRepository _shareRequestRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<WithdrawPrivateGameProposalCommandHandler> _logger;

    public WithdrawPrivateGameProposalCommandHandler(
        IPrivateGameRepository privateGameRepository,
        IShareRequestRepository shareRequestRepository,
        IUnitOfWork unitOfWork,
        ILogger<WithdrawPrivateGameProposalCommandHandler> logger)
    {
        _privateGameRepository = privateGameRepository ?? throw new ArgumentNullException(nameof(privateGameRepository));
        _shareRequestRepository = shareRequestRepository ?? throw new ArgumentNullException(nameof(shareRequestRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(WithdrawPrivateGameProposalCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var privateGame = await _privateGameRepository.GetByIdAsync(command.PrivateGameId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Private game with ID {command.PrivateGameId} not found");

        if (privateGame.OwnerId != command.UserId)
        {
            throw new ForbiddenException("You can only withdraw the proposals of your own private games");
        }

        var open = await _shareRequestRepository.GetPendingProposalForPrivateGameAsync(
            command.PrivateGameId,
            cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Private game {command.PrivateGameId} has no open proposal to the catalog");

        // The domain refuses to withdraw a request an admin holds in review; say so in the user's terms.
        if (open.Status == ShareRequestStatus.InReview)
        {
            throw new ConflictException(
                PrivateGameProposalCodes.InReview,
                "An admin is reviewing the proposal: it can be withdrawn after the decision");
        }

        open.Withdraw();
        _shareRequestRepository.Update(open);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Proposal {ShareRequestId} of private game {PrivateGameId} withdrawn by {UserId}",
            open.Id,
            command.PrivateGameId,
            command.UserId);
    }
}
