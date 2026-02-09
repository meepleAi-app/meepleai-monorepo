using Api.BoundedContexts.GameManagement.Application.Commands.RuleConflictFAQs;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameManagement.Application.Handlers.RuleConflictFAQs;

/// <summary>
/// Handles FAQ usage recording when ArbitroAgent applies a resolution.
/// Issue #3966: CQRS handlers for conflict FAQ management.
/// </summary>
internal sealed class RecordFaqUsageCommandHandler : ICommandHandler<RecordFaqUsageCommand>
{
    private readonly IRuleConflictFaqRepository _faqRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public RecordFaqUsageCommandHandler(
        IRuleConflictFaqRepository faqRepository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _faqRepository = faqRepository ?? throw new ArgumentNullException(nameof(faqRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task Handle(RecordFaqUsageCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Get FAQ
        var faq = await _faqRepository.GetByIdAsync(command.Id, cancellationToken)
            .ConfigureAwait(false);

        if (faq == null)
        {
            throw new NotFoundException("RuleConflictFAQ", command.Id.ToString());
        }

        // Record usage
        faq.RecordUsage(_timeProvider);

        // Persist
        await _faqRepository.UpdateAsync(faq, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
