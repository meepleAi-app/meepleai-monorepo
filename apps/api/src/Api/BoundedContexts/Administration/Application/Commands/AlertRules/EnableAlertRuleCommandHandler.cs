using Api.BoundedContexts.Administration.Application.Commands.AlertRules;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.SharedKernel.Guards;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Commands.AlertRules;

internal class EnableAlertRuleCommandHandler : IRequestHandler<EnableAlertRuleCommand, Unit>
{
    private readonly IAlertRuleRepository _repository;

    public EnableAlertRuleCommandHandler(IAlertRuleRepository repository) =>
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));

    public async Task<Unit> Handle(EnableAlertRuleCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Validate input before repository operations
        Guard.AgainstEmptyGuid(request.Id, nameof(request.Id));
        Guard.AgainstNullOrWhiteSpace(request.UpdatedBy, nameof(request.UpdatedBy));

        var rule = await _repository.GetByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);
        if (rule == null) throw new InvalidOperationException($"AlertRule {request.Id} not found");

        if (rule.IsEnabled) rule.Disable(request.UpdatedBy);
        else rule.Enable(request.UpdatedBy);

        await _repository.UpdateAsync(rule, cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}

