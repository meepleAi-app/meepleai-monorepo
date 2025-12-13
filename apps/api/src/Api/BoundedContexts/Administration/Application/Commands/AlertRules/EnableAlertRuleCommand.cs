using MediatR;

namespace Api.BoundedContexts.Administration.Application.Commands.AlertRules;

public record EnableAlertRuleCommand(Guid Id, string UpdatedBy) : IRequest<Unit>;
