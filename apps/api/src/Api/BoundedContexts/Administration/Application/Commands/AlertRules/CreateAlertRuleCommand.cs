using MediatR;

namespace Api.BoundedContexts.Administration.Application.Commands.AlertRules;

public record CreateAlertRuleCommand(string Name, string AlertType, string Severity, double ThresholdValue, string ThresholdUnit, int DurationMinutes, string? Description, string CreatedBy) : IRequest<Guid>;
