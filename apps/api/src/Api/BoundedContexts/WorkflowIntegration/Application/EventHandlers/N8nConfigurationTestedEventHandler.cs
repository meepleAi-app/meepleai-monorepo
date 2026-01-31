using Api.BoundedContexts.WorkflowIntegration.Domain.Events;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.WorkflowIntegration.Application.EventHandlers;

/// <summary>
/// Handler for N8NConfigurationTestedEvent domain event.
/// </summary>
internal sealed class N8NConfigurationTestedEventHandler : DomainEventHandlerBase<N8NConfigurationTestedEvent>
{
    public N8NConfigurationTestedEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<N8NConfigurationTestedEventHandler> logger)
        : base(dbContext, logger)
    {
    }

    protected override async Task HandleEventAsync(N8NConfigurationTestedEvent domainEvent, CancellationToken cancellationToken)
    {
        // Future: Send alert if test failed
        // Future: Update health monitoring dashboard
        await Task.CompletedTask.ConfigureAwait(false);
    }

    protected override Dictionary<string, object?>? GetAuditMetadata(N8NConfigurationTestedEvent domainEvent)
    {
        return new Dictionary<string, object?>
(StringComparer.Ordinal)
        {
            ["ConfigurationId"] = domainEvent.ConfigurationId,
            ["TestSuccess"] = domainEvent.TestSuccess,
            ["TestResult"] = domainEvent.TestResult,
            ["TestedAt"] = domainEvent.TestedAt,
            ["Action"] = "N8NConfigurationTested"
        };
    }
}
