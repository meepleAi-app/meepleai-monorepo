using MediatR;

namespace Api.BoundedContexts.Administration.Application.Queries.AlertRules;

internal record GetAlertRuleByIdQuery(Guid Id) : IRequest<AlertRuleDto?>;
