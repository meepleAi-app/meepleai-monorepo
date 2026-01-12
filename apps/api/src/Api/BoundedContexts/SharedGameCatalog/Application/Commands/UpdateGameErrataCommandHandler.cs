using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for updating an existing erratum in a shared game.
/// NOTE: This implementation uses GetGameByErrataIdAsync from ISharedGameRepository
/// to locate the game containing the erratum for update.
/// </summary>
internal sealed class UpdateGameErrataCommandHandler : ICommandHandler<UpdateGameErrataCommand, Unit>
{
    private readonly ISharedGameRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateGameErrataCommandHandler> _logger;

    public UpdateGameErrataCommandHandler(
        ISharedGameRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<UpdateGameErrataCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Unit> Handle(UpdateGameErrataCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "Updating erratum: {ErrataId}, PageReference: {PageReference}",
            command.ErrataId, command.PageReference);

        // Fetch the game containing this erratum
        var game = await _repository.GetGameByErrataIdAsync(command.ErrataId, cancellationToken).ConfigureAwait(false);
        if (game is null)
        {
            throw new InvalidOperationException($"Erratum with ID {command.ErrataId} not found");
        }

        // Find the erratum in the game's collection
        var errata = game.Erratas.FirstOrDefault(e => e.Id == command.ErrataId);
        if (errata is null)
        {
            throw new InvalidOperationException($"Erratum with ID {command.ErrataId} not found in game");
        }

        // Update the erratum properties via domain method
        game.UpdateErrata(command.ErrataId, command.Description, command.PageReference, command.PublishedDate);

        // Update the game in repository
        _repository.Update(game);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Erratum updated successfully: {ErrataId}",
            command.ErrataId);

        return Unit.Value;
    }
}
