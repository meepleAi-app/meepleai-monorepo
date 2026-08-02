using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Epic #3470 — persists an admin's per-context cover choice through the aggregate
/// (<see cref="Domain.Aggregates.SharedGame.AssignCover"/>) and the child-safe
/// <see cref="ISharedGameRepository.ReconcileCoverAssignmentsAsync"/>. Returns the
/// persisted assignment so the picker can reflect the new state.
/// </summary>
internal sealed class AssignCoverCommandHandler : ICommandHandler<AssignCoverCommand, CoverAssignmentDto>
{
    private readonly ISharedGameRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AssignCoverCommandHandler> _logger;

    public AssignCoverCommandHandler(
        ISharedGameRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<AssignCoverCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<CoverAssignmentDto> Handle(AssignCoverCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var game = await _repository.GetByIdAsync(command.GameId, cancellationToken).ConfigureAwait(false);
        if (game is null)
        {
            throw new NotFoundException("SharedGame", command.GameId.ToString());
        }

        var assignment = game.AssignCover(command.Context, command.Source, command.AdminId, command.FocalX, command.FocalY);

        await _repository.ReconcileCoverAssignmentsAsync(game, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Assigned {Source} cover to {Context} for game {GameId} by {AdminId}",
            command.Source, command.Context, command.GameId, command.AdminId);

        return new CoverAssignmentDto(assignment.Context, assignment.Source, assignment.FocalX, assignment.FocalY);
    }
}
