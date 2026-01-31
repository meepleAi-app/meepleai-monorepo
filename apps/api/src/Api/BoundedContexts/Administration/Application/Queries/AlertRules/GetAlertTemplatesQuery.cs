using MediatR;

namespace Api.BoundedContexts.Administration.Application.Queries.AlertRules;

internal record GetAlertTemplatesQuery : IRequest<List<AlertTemplateDto>>;

internal record AlertTemplateDto(string Name, string AlertType, string Severity, double ThresholdValue, string ThresholdUnit, int DurationMinutes, string Description, string Category);
