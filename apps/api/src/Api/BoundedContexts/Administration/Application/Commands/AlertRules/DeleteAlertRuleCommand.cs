using MediatR;

namespace Api.BoundedContexts.Administration.Application.Commands.AlertRules;

internal record DeleteAlertRuleCommand(Guid Id) : IRequest<Unit>;
