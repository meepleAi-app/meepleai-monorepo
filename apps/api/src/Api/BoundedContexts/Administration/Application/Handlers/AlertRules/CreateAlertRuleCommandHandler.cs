using Api.BoundedContexts.Administration.Application.Commands.AlertRules;
using Api.BoundedContexts.Administration.Domain.Aggregates.AlertRules;
using Api.BoundedContexts.Administration.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Handlers.AlertRules;

internal class CreateAlertRuleCommandHandler : IRequestHandler<CreateAlertRuleCommand, Guid>
{
    private readonly IAlertRuleRepository _repository;

    public CreateAlertRuleCommandHandler(IAlertRuleRepository repository) =>
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));

    public async Task<Guid> Handle(CreateAlertRuleCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var severity = AlertSeverityExtensions.FromString(request.Severity);
        var threshold = new AlertThreshold(request.ThresholdValue, request.ThresholdUnit);
        var duration = new AlertDuration(request.DurationMinutes);

        var rule = AlertRule.Create(request.Name, request.AlertType, severity, threshold, duration, request.CreatedBy, request.Description);
        await _repository.AddAsync(rule, cancellationToken).ConfigureAwait(false);
        return rule.Id;
    }
}

