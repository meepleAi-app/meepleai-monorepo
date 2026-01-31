using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Command to resolve active alerts of a specific type.
/// </summary>
internal record ResolveAlertCommand(string AlertType) : ICommand<bool>;
