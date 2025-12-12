using MediatR;

namespace Api.BoundedContexts.Administration.Application.Commands.AlertRules;

public record UpdateAlertRuleCommand(Guid Id, string Name, string Severity, double ThresholdValue, string ThresholdUnit, int DurationMinutes, string? Description, string UpdatedBy) : IRequest<Unit>;
