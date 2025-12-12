using MediatR;

namespace Api.BoundedContexts.Administration.Application.Queries.AlertRules;

public record GetAllAlertRulesQuery : IRequest<List<AlertRuleDto>>;

public record AlertRuleDto(Guid Id, string Name, string AlertType, string Severity, double ThresholdValue, string ThresholdUnit, int DurationMinutes, bool IsEnabled, string? Description, DateTime CreatedAt, DateTime UpdatedAt);
