using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Handles DeleteGameFAQCommand.
/// Issue #2028: Backend FAQ system for game-specific FAQs.
/// </summary>
internal class DeleteGameFAQCommandHandler : ICommandHandler<DeleteGameFAQCommand>
{
    private readonly IGameFAQRepository _faqRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteGameFAQCommandHandler(
        IGameFAQRepository faqRepository,
        IUnitOfWork unitOfWork)
    {
        _faqRepository = faqRepository ?? throw new ArgumentNullException(nameof(faqRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task Handle(DeleteGameFAQCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        // Retrieve existing FAQ
        var faq = await _faqRepository.GetByIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (faq == null)
            throw new InvalidOperationException($"FAQ with ID {command.Id} not found");

        // Delete
        await _faqRepository.DeleteAsync(faq, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
