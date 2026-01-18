using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for submitting a shared game for approval.
/// Issue #2514: Approval workflow implementation
/// </summary>
internal sealed class SubmitSharedGameForApprovalCommandHandler : ICommandHandler<SubmitSharedGameForApprovalCommand, Unit>
{
    private readonly ISharedGameRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SubmitSharedGameForApprovalCommandHandler> _logger;

    public SubmitSharedGameForApprovalCommandHandler(
        ISharedGameRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<SubmitSharedGameForApprovalCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Unit> Handle(SubmitSharedGameForApprovalCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "Submitting shared game for approval: {GameId}, SubmittedBy: {UserId}",
            command.GameId, command.SubmittedBy);

        var game = await _repository.GetByIdAsync(command.GameId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Shared game with ID {command.GameId} not found");

        // Call domain method (validates status and raises event)
        game.SubmitForApproval(command.SubmittedBy);

        _repository.Update(game);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Shared game submitted for approval successfully: {GameId}",
            command.GameId);

        return Unit.Value;
    }
}
