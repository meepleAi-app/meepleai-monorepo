using Api.BoundedContexts.GameManagement.Application.Commands.RuleConflictFAQs;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameManagement.Application.Handlers.RuleConflictFAQs;

/// <summary>
/// Handles RuleConflictFAQ creation.
/// Issue #3966: CQRS handlers for conflict FAQ management.
/// </summary>
internal sealed class CreateRuleConflictFaqCommandHandler : ICommandHandler<CreateRuleConflictFaqCommand, Guid>
{
    private readonly IRuleConflictFaqRepository _faqRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public CreateRuleConflictFaqCommandHandler(
        IRuleConflictFaqRepository faqRepository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider)
    {
        _faqRepository = faqRepository ?? throw new ArgumentNullException(nameof(faqRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<Guid> Handle(CreateRuleConflictFaqCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Check for duplicate pattern (composite unique constraint)
        var existing = await _faqRepository.FindByPatternAsync(
            command.GameId,
            command.Pattern,
            cancellationToken)
            .ConfigureAwait(false);

        if (existing != null)
        {
            throw new ConflictException(
                $"FAQ with pattern '{command.Pattern}' already exists for this game");
        }

        // Create aggregate
        var faq = RuleConflictFAQ.Create(
            Guid.NewGuid(),
            command.GameId,
            command.ConflictType,
            command.Pattern,
            command.Resolution,
            command.Priority,
            _timeProvider);

        // Persist
        await _faqRepository.AddAsync(faq, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return faq.Id;
    }
}
