using MediatR;

namespace Api.BoundedContexts.Administration.Application.Queries.AlertRules;

public record GetAlertRuleByIdQuery(Guid Id) : IRequest<AlertRuleDto?>;
