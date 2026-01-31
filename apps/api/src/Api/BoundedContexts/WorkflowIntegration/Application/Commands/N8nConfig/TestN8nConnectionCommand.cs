using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.WorkflowIntegration.Application.Commands.N8NConfig;

/// <summary>
/// Command to test an n8n workflow configuration connection.
/// Tests connectivity to n8n instance and updates test results in database.
/// </summary>
internal sealed record TestN8NConnectionCommand : ICommand<N8NTestResult>
{
    public Guid ConfigId { get; init; }
}
