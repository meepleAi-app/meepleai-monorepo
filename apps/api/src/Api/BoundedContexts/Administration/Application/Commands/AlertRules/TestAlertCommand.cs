using MediatR;

namespace Api.BoundedContexts.Administration.Application.Commands.AlertRules;

internal record TestAlertCommand(string AlertType, string Channel) : IRequest<bool>;
