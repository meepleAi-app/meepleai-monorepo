using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.WorkflowIntegration.Application.Commands;

/// <summary>
/// Command to delete an n8n configuration.
/// Returns true if deleted, false if not found.
/// </summary>
public record DeleteN8nConfigCommand(Guid ConfigId) : ICommand<bool>;
