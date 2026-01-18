using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for rejecting a shared game publication.
/// Issue #2514: Approval workflow implementation
/// </summary>
internal sealed class RejectSharedGamePublicationCommandHandler : ICommandHandler<RejectSharedGamePublicationCommand, Unit>
{
    private readonly ISharedGameRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RejectSharedGamePublicationCommandHandler> _logger;

    public RejectSharedGamePublicationCommandHandler(
        ISharedGameRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<RejectSharedGamePublicationCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Unit> Handle(RejectSharedGamePublicationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "Rejecting shared game publication: {GameId}, RejectedBy: {UserId}, Reason: {Reason}",
            command.GameId, command.RejectedBy, command.Reason);

        var game = await _repository.GetByIdAsync(command.GameId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Shared game with ID {command.GameId} not found");

        // Call domain method (validates status and raises event)
        game.RejectPublication(command.RejectedBy, command.Reason);

        _repository.Update(game);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Shared game publication rejected successfully: {GameId}",
            command.GameId);

        return Unit.Value;
    }
}
