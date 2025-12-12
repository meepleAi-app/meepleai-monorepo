using MediatR;

namespace Api.BoundedContexts.Administration.Application.Queries.AlertRules;

public record GetAlertTemplatesQuery : IRequest<List<AlertTemplateDto>>;

public record AlertTemplateDto(string Name, string AlertType, string Severity, double ThresholdValue, string ThresholdUnit, int DurationMinutes, string Description, string Category);
