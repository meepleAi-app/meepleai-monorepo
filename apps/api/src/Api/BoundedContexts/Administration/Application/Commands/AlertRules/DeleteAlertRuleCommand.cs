using MediatR;

namespace Api.BoundedContexts.Administration.Application.Commands.AlertRules;

public record DeleteAlertRuleCommand(Guid Id) : IRequest<Unit>;
