using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Handles CreateGameFAQCommand.
/// Issue #2028: Backend FAQ system for game-specific FAQs.
/// </summary>
internal class CreateGameFAQCommandHandler : ICommandHandler<CreateGameFAQCommand, GameFAQDto>
{
    private readonly IGameFAQRepository _faqRepository;
    private readonly IGameRepository _gameRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateGameFAQCommandHandler(
        IGameFAQRepository faqRepository,
        IGameRepository gameRepository,
        IUnitOfWork unitOfWork)
    {
        _faqRepository = faqRepository ?? throw new ArgumentNullException(nameof(faqRepository));
        _gameRepository = gameRepository ?? throw new ArgumentNullException(nameof(gameRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<GameFAQDto> Handle(CreateGameFAQCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        // Validate game exists
        var gameExists = await _gameRepository.ExistsAsync(command.GameId, cancellationToken).ConfigureAwait(false);
        if (!gameExists)
            throw new InvalidOperationException($"Game with ID {command.GameId} not found");

        // Create domain entity
        var question = new FAQQuestion(command.Question);
        var answer = new FAQAnswer(command.Answer);

        var faq = new GameFAQ(
            id: Guid.NewGuid(),
            gameId: command.GameId,
            question: question,
            answer: answer
        );

        // Persist
        await _faqRepository.AddAsync(faq, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Return DTO
        return new GameFAQDto(
            Id: faq.Id,
            GameId: faq.GameId,
            Question: faq.Question.Value,
            Answer: faq.Answer.Value,
            Upvotes: faq.Upvotes,
            CreatedAt: faq.CreatedAt,
            UpdatedAt: faq.UpdatedAt
        );
    }
}
