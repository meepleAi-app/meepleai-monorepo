using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Handles UpvoteGameFAQCommand.
/// Issue #2028: Backend FAQ system for game-specific FAQs.
/// </summary>
internal class UpvoteGameFAQCommandHandler : ICommandHandler<UpvoteGameFAQCommand, GameFAQDto>
{
    private readonly IGameFAQRepository _faqRepository;

    public UpvoteGameFAQCommandHandler(IGameFAQRepository faqRepository)
    {
        _faqRepository = faqRepository ?? throw new ArgumentNullException(nameof(faqRepository));
    }

    public async Task<GameFAQDto> Handle(UpvoteGameFAQCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Atomic increment to prevent race conditions
        // Issue #2186: Fixed concurrent upvote handling
        var faq = await _faqRepository.IncrementUpvoteAsync(command.Id, cancellationToken).ConfigureAwait(false);

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
