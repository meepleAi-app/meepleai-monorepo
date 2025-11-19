using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.WorkflowIntegration.Application.Commands.N8nConfig;

/// <summary>
/// Command to test an n8n workflow configuration connection.
/// Tests connectivity to n8n instance and updates test results in database.
/// </summary>
public sealed record TestN8nConnectionCommand : ICommand<N8nTestResult>
{
    public Guid ConfigId { get; init; }
}
