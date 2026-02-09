using Api.BoundedContexts.GameManagement.Application.Commands.RuleConflictFAQs;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameManagement.Application.Handlers.RuleConflictFAQs;

/// <summary>
/// Handles FAQ deletion.
/// Issue #3966: CQRS handlers for conflict FAQ management.
/// </summary>
internal sealed class DeleteRuleConflictFaqCommandHandler : ICommandHandler<DeleteRuleConflictFaqCommand>
{
    private readonly IRuleConflictFaqRepository _faqRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteRuleConflictFaqCommandHandler(
        IRuleConflictFaqRepository faqRepository,
        IUnitOfWork unitOfWork)
    {
        _faqRepository = faqRepository ?? throw new ArgumentNullException(nameof(faqRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task Handle(DeleteRuleConflictFaqCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Delete (throws NotFoundException if not found)
        await _faqRepository.DeleteAsync(command.Id, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
