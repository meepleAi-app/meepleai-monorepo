using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.BoundedContexts.SystemConfiguration.Domain.Services;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.UserLibrary.Application.Commands.PrivateGames;

/// <summary>
/// Handler for <see cref="ProposePrivateGameCommand"/>. Issue #3665 (Phase 4), restored by #5074.
/// </summary>
internal sealed class ProposePrivateGameCommandHandler : ICommandHandler<ProposePrivateGameCommand, CreateShareRequestResponse>
{
    private readonly IPrivateGameRepository _privateGameRepository;
    private readonly IShareRequestRepository _shareRequestRepository;
    private readonly IRateLimitEvaluator _rateLimitEvaluator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ProposePrivateGameCommandHandler> _logger;

    /// <summary>
    /// #5079: at most one open or approved proposal per private game, enforced by the database
    /// (migration <c>ShareRequestsOneProposalPerPrivateGame</c>): two concurrent requests both passed the check below.
    /// </summary>
    internal const string OneProposalIndex = "ux_share_requests_one_proposal_per_private_game";

    public ProposePrivateGameCommandHandler(
        IPrivateGameRepository privateGameRepository,
        IShareRequestRepository shareRequestRepository,
        IRateLimitEvaluator rateLimitEvaluator,
        IUnitOfWork unitOfWork,
        ILogger<ProposePrivateGameCommandHandler> logger)
    {
        _privateGameRepository = privateGameRepository ?? throw new ArgumentNullException(nameof(privateGameRepository));
        _shareRequestRepository = shareRequestRepository ?? throw new ArgumentNullException(nameof(shareRequestRepository));
        _rateLimitEvaluator = rateLimitEvaluator ?? throw new ArgumentNullException(nameof(rateLimitEvaluator));
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
                PrivateGameProposalCodes.AlreadyOpen,
                $"A proposal for this private game is already open (ShareRequest ID: {pending.Id})");
        }

        // #5079, decision of 2026-11-19: once approved, the game is in the catalog and is not proposed again. After a
        // rejection or a withdrawal it can be.
        if (await _shareRequestRepository.HasApprovedProposalForPrivateGameAsync(command.PrivateGameId, cancellationToken)
                .ConfigureAwait(false))
        {
            throw new ConflictException(
                PrivateGameProposalCodes.AlreadyApproved,
                "This private game's proposal was approved: the game is already in the catalog");
        }

        // #5079, decision of 2026-11-19: a proposal is a share request, and the counters already count it; it takes the
        // same limits (pending requests, monthly cap, the pause after a rejection).
        if (!await _rateLimitEvaluator.CanUserCreateRequestAsync(command.UserId, cancellationToken).ConfigureAwait(false))
        {
            throw new TooManyRequestsException(
                PrivateGameProposalCodes.LimitReached,
                "You have reached the limit of share requests for now: wait for a review, or for next month");
        }

        var shareRequest = ShareRequest.CreateGameProposal(command.UserId, command.PrivateGameId, command.Notes);

        await _shareRequestRepository.AddAsync(shareRequest, cancellationToken).ConfigureAwait(false);
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (IsOneProposalViolation(ex))
        {
            // A concurrent proposal for the same game was stored between the check above and this insert.
            throw new ConflictException(
                PrivateGameProposalCodes.AlreadyOpen,
                "A proposal for this private game is already open",
                ex);
        }

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

    private static bool IsOneProposalViolation(DbUpdateException ex) =>
        ex.InnerException is Npgsql.PostgresException { SqlState: "23505" } pg &&
        string.Equals(pg.ConstraintName, OneProposalIndex, StringComparison.Ordinal);
}
