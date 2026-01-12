using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for adding a new FAQ to an existing shared game.
/// </summary>
internal sealed class AddGameFaqCommandHandler : ICommandHandler<AddGameFaqCommand, Guid>
{
    private readonly ISharedGameRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AddGameFaqCommandHandler> _logger;

    public AddGameFaqCommandHandler(
        ISharedGameRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<AddGameFaqCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Guid> Handle(AddGameFaqCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "Adding FAQ to shared game: {SharedGameId}, Order: {Order}",
            command.SharedGameId, command.Order);

        // Fetch the game
        var game = await _repository.GetByIdAsync(command.SharedGameId, cancellationToken).ConfigureAwait(false);
        if (game is null)
        {
            throw new InvalidOperationException($"Shared game with ID {command.SharedGameId} not found");
        }

        // Create the FAQ
        var faq = GameFaq.Create(command.SharedGameId, command.Question, command.Answer, command.Order);

        // Add FAQ to game via domain method
        game.AddFaq(faq);

        // Update the game in repository
        _repository.Update(game);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "FAQ added successfully: {FaqId} to game {SharedGameId}",
            faq.Id, command.SharedGameId);

        return faq.Id;
    }
}
