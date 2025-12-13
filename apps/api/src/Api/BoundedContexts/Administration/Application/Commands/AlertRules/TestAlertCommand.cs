using MediatR;

namespace Api.BoundedContexts.Administration.Application.Commands.AlertRules;

public record TestAlertCommand(string AlertType, string Channel) : IRequest<bool>;
