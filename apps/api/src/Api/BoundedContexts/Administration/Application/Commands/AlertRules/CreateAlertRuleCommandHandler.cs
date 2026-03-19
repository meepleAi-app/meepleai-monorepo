using Api.BoundedContexts.Administration.Application.Commands.AlertRules;
using Api.BoundedContexts.Administration.Domain.Aggregates.AlertRules;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.SharedKernel.Guards;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Commands.AlertRules;

internal class CreateAlertRuleCommandHandler : IRequestHandler<CreateAlertRuleCommand, Guid>
{
    private static readonly string[] AllowedSeverities = { "Info", "Warning", "Error", "Critical" };

    private readonly IAlertRuleRepository _repository;

    public CreateAlertRuleCommandHandler(IAlertRuleRepository repository) =>
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));

    public async Task<Guid> Handle(CreateAlertRuleCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Validate input before domain operations
        Guard.AgainstNullOrWhiteSpace(request.Name, nameof(request.Name));
        Guard.AgainstNullOrWhiteSpace(request.Severity, nameof(request.Severity));
        Guard.AgainstInvalidValue(request.Severity, AllowedSeverities, nameof(request.Severity));
        Guard.AgainstOutOfRange(request.ThresholdValue, nameof(request.ThresholdValue), 0.01, 100.0);
        Guard.AgainstTooSmall(request.DurationMinutes, nameof(request.DurationMinutes), 1);
        Guard.AgainstNullOrWhiteSpace(request.CreatedBy, nameof(request.CreatedBy));

        var severity = AlertSeverityExtensions.FromString(request.Severity);
        var threshold = new AlertThreshold(request.ThresholdValue, request.ThresholdUnit);
        var duration = new AlertDuration(request.DurationMinutes);

        var rule = AlertRule.Create(request.Name, request.AlertType, severity, threshold, duration, request.CreatedBy, request.Description);
        await _repository.AddAsync(rule, cancellationToken).ConfigureAwait(false);
        return rule.Id;
    }
}

