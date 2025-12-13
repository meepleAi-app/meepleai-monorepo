using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Handles UpvoteGameFAQCommand.
/// Issue #2028: Backend FAQ system for game-specific FAQs.
/// </summary>
public class UpvoteGameFAQCommandHandler : ICommandHandler<UpvoteGameFAQCommand, GameFAQDto>
{
    private readonly IGameFAQRepository _faqRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpvoteGameFAQCommandHandler(
        IGameFAQRepository faqRepository,
        IUnitOfWork unitOfWork)
    {
        _faqRepository = faqRepository ?? throw new ArgumentNullException(nameof(faqRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<GameFAQDto> Handle(UpvoteGameFAQCommand command, CancellationToken cancellationToken)
    {
        // Retrieve existing FAQ
        var faq = await _faqRepository.GetByIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (faq == null)
            throw new InvalidOperationException($"FAQ with ID {command.Id} not found");

        // Add upvote
        faq.AddUpvote();

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
