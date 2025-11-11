using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Command to resolve active alerts of a specific type.
/// </summary>
public record ResolveAlertCommand(string AlertType) : ICommand<bool>;
