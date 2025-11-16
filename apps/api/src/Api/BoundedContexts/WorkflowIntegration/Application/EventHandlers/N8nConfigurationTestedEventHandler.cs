using Api.BoundedContexts.WorkflowIntegration.Domain.Events;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.WorkflowIntegration.Application.EventHandlers;

/// <summary>
/// Handler for N8nConfigurationTestedEvent domain event.
/// </summary>
public sealed class N8nConfigurationTestedEventHandler : DomainEventHandlerBase<N8nConfigurationTestedEvent>
{
    public N8nConfigurationTestedEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<DomainEventHandlerBase<N8nConfigurationTestedEvent>> logger)
        : base(dbContext, logger)
    {
    }

    protected override async Task HandleEventAsync(N8nConfigurationTestedEvent domainEvent, CancellationToken cancellationToken)
    {
        // Future: Send alert if test failed
        // Future: Update health monitoring dashboard
        await Task.CompletedTask;
    }

    protected override Dictionary<string, object?>? GetAuditMetadata(N8nConfigurationTestedEvent domainEvent)
    {
        return new Dictionary<string, object?>
        {
            ["ConfigurationId"] = domainEvent.ConfigurationId,
            ["TestSuccess"] = domainEvent.TestSuccess,
            ["TestResult"] = domainEvent.TestResult,
            ["TestedAt"] = domainEvent.TestedAt,
            ["Action"] = "N8nConfigurationTested"
        };
    }
}
