using Api.BoundedContexts.Administration.Application.Commands.AlertRules;
using Api.BoundedContexts.Administration.Domain.Aggregates.AlertRules;
using Api.BoundedContexts.Administration.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Handlers.AlertRules;

internal class UpdateAlertRuleCommandHandler : IRequestHandler<UpdateAlertRuleCommand, Unit>
{
    private readonly IAlertRuleRepository _repository;

    public UpdateAlertRuleCommandHandler(IAlertRuleRepository repository) =>
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));

    public async Task<Unit> Handle(UpdateAlertRuleCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var rule = await _repository.GetByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);
        if (rule == null) throw new InvalidOperationException($"AlertRule {request.Id} not found");

        var severity = AlertSeverityExtensions.FromString(request.Severity);
        var threshold = new AlertThreshold(request.ThresholdValue, request.ThresholdUnit);
        var duration = new AlertDuration(request.DurationMinutes);

        rule.Update(request.Name, severity, threshold, duration, request.UpdatedBy, request.Description);
        await _repository.UpdateAsync(rule, cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}

