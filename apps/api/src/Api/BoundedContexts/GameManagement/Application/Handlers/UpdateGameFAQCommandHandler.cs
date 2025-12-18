using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Handles UpdateGameFAQCommand.
/// Issue #2028: Backend FAQ system for game-specific FAQs.
/// </summary>
internal class UpdateGameFAQCommandHandler : ICommandHandler<UpdateGameFAQCommand, GameFAQDto>
{
    private readonly IGameFAQRepository _faqRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateGameFAQCommandHandler(
        IGameFAQRepository faqRepository,
        IUnitOfWork unitOfWork)
    {
        _faqRepository = faqRepository ?? throw new ArgumentNullException(nameof(faqRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<GameFAQDto> Handle(UpdateGameFAQCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        // Retrieve existing FAQ
        var faq = await _faqRepository.GetByIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (faq == null)
            throw new InvalidOperationException($"FAQ with ID {command.Id} not found");

        // Update domain entity
        var question = new FAQQuestion(command.Question);
        var answer = new FAQAnswer(command.Answer);
        faq.Update(question, answer);

        // Persist
        await _faqRepository.UpdateAsync(faq, cancellationToken).ConfigureAwait(false);
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
