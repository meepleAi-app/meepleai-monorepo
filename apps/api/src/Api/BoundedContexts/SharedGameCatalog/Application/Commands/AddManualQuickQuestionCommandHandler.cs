using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for manually adding a quick question to a game.
/// </summary>
internal sealed class AddManualQuickQuestionCommandHandler : ICommandHandler<AddManualQuickQuestionCommand, Guid>
{
    private readonly ISharedGameRepository _gameRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AddManualQuickQuestionCommandHandler> _logger;

    public AddManualQuickQuestionCommandHandler(
        ISharedGameRepository gameRepository,
        IUnitOfWork unitOfWork,
        ILogger<AddManualQuickQuestionCommandHandler> logger)
    {
        _gameRepository = gameRepository ?? throw new ArgumentNullException(nameof(gameRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Guid> Handle(AddManualQuickQuestionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation("Adding manual quick question to game: {SharedGameId}", command.SharedGameId);

        // Fetch aggregate
        var game = await _gameRepository.GetByIdAsync(command.SharedGameId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Shared game with ID {command.SharedGameId} not found");

        // Create domain entity
        var question = QuickQuestion.CreateManual(
            command.SharedGameId,
            command.Text,
            command.Emoji,
            command.Category,
            command.DisplayOrder);

        // Add to aggregate via domain method
        game.AddQuickQuestion(question);

        // Persist
        _gameRepository.Update(game);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Quick question added: {QuestionId} to game {SharedGameId}",
            question.Id,
            command.SharedGameId);

        return question.Id;
    }
}