using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for adding a new erratum to an existing shared game.
/// </summary>
internal sealed class AddGameErrataCommandHandler : ICommandHandler<AddGameErrataCommand, Guid>
{
    private readonly ISharedGameRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AddGameErrataCommandHandler> _logger;

    public AddGameErrataCommandHandler(
        ISharedGameRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<AddGameErrataCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Guid> Handle(AddGameErrataCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "Adding erratum to shared game: {SharedGameId}, PageReference: {PageReference}",
            command.SharedGameId, command.PageReference);

        // Fetch the game
        var game = await _repository.GetByIdAsync(command.SharedGameId, cancellationToken).ConfigureAwait(false);
        if (game is null)
        {
            throw new InvalidOperationException($"Shared game with ID {command.SharedGameId} not found");
        }

        // Create the erratum
        var errata = GameErrata.Create(command.SharedGameId, command.Description, command.PageReference, command.PublishedDate);

        // Add erratum to game via domain method
        game.AddErrata(errata);

        // Update the game in repository
        _repository.Update(game);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Erratum added successfully: {ErrataId} to game {SharedGameId}",
            errata.Id, command.SharedGameId);

        return errata.Id;
    }
}
