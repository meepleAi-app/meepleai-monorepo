using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using FluentValidation;
using FluentValidation.Results;

namespace Api.BoundedContexts.UserLibrary.Application.Commands.PrivateGames;

/// <summary>
/// Handler for <see cref="ProposePrivateGameCommand"/>. Issue #3665 (Phase 4), restored by #5074.
/// </summary>
internal sealed class ProposePrivateGameCommandHandler : ICommandHandler<ProposePrivateGameCommand, CreateShareRequestResponse>
{
    private readonly IPrivateGameRepository _privateGameRepository;
    private readonly IShareRequestRepository _shareRequestRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ProposePrivateGameCommandHandler> _logger;

    public ProposePrivateGameCommandHandler(
        IPrivateGameRepository privateGameRepository,
        IShareRequestRepository shareRequestRepository,
        IUnitOfWork unitOfWork,
        ILogger<ProposePrivateGameCommandHandler> logger)
    {
        _privateGameRepository = privateGameRepository ?? throw new ArgumentNullException(nameof(privateGameRepository));
        _shareRequestRepository = shareRequestRepository ?? throw new ArgumentNullException(nameof(shareRequestRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<CreateShareRequestResponse> Handle(
        ProposePrivateGameCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var privateGame = await _privateGameRepository.GetByIdAsync(command.PrivateGameId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Private game with ID {command.PrivateGameId} not found");

        if (privateGame.OwnerId != command.UserId)
        {
            _logger.LogWarning(
                "User {UserId} attempted to propose private game {GameId} owned by {OwnerId}",
                command.UserId,
                command.PrivateGameId,
                privateGame.OwnerId);
            throw new ForbiddenException("You can only propose your own private games");
        }

        // #5074, decision of 2026-11-19: the shared catalog requires a description, so the proposal does too.
        // Approving a proposal without one used to fail at the admin's step, with a 400.
        if (!privateGame.CanBeProposedToCatalog)
        {
            throw new ValidationException(
            [
                new ValidationFailure(
                    nameof(privateGame.Description),
                    "Add a description to the game before proposing it to the catalog."),
            ]);
        }

        var pending = await _shareRequestRepository.GetPendingProposalForPrivateGameAsync(
            command.PrivateGameId,
            cancellationToken).ConfigureAwait(false);

        if (pending != null)
        {
            throw new ConflictException(
                $"A pending proposal already exists for this private game (ShareRequest ID: {pending.Id})");
        }

        var shareRequest = ShareRequest.CreateGameProposal(command.UserId, command.PrivateGameId, command.Notes);

        await _shareRequestRepository.AddAsync(shareRequest, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Game proposal {ShareRequestId} created for private game {PrivateGameId}",
            shareRequest.Id,
            command.PrivateGameId);

        return new CreateShareRequestResponse(
            shareRequest.Id,
            shareRequest.Status,
            ContributionType.NewGameProposal,
            shareRequest.CreatedAt);
    }
}
