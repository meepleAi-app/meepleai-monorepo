using Api.BoundedContexts.Administration.Application.Commands.AlertRules;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Guards;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Commands.AlertRules;

internal class DeleteAlertRuleCommandHandler : IRequestHandler<DeleteAlertRuleCommand, Unit>
{
    private readonly IAlertRuleRepository _repository;

    public DeleteAlertRuleCommandHandler(IAlertRuleRepository repository) =>
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));

    public async Task<Unit> Handle(DeleteAlertRuleCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Validate input before repository operations
        Guard.AgainstEmptyGuid(request.Id, nameof(request.Id));

        // Verify rule exists before deletion
        var existingRule = await _repository.GetByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);
        if (existingRule == null)
            throw new DomainException($"AlertRule {request.Id} not found");

        await _repository.DeleteAsync(request.Id, cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}

