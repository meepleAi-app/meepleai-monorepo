using MediatR;

namespace Api.BoundedContexts.Administration.Application.Commands.AlertRules;

internal record EnableAlertRuleCommand(Guid Id, string UpdatedBy) : IRequest<Unit>;
