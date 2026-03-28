using Api.BoundedContexts.Administration.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Api.BoundedContexts.Administration.Infrastructure.Scheduling;

/// <summary>
/// Quartz.NET job for cleaning up service call log entries older than 7 days.
/// </summary>
[DisallowConcurrentExecution]
internal sealed class ServiceCallLogRetentionJob : IJob
{
    internal const int RetentionDays = 7;

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ServiceCallLogRetentionJob> _logger;

    public ServiceCallLogRetentionJob(
        IServiceProvider serviceProvider,
        ILogger<ServiceCallLogRetentionJob> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var cutoff = DateTime.UtcNow.AddDays(-RetentionDays);
        _logger.LogInformation(
            "Starting service call log retention cleanup: RetentionDays={RetentionDays}, Cutoff={Cutoff}",
            RetentionDays, cutoff);

        using var scope = _serviceProvider.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IServiceCallLogRepository>();

        try
        {
            await repo.DeleteOlderThanAsync(cutoff, context.CancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Service call log retention completed");
        }
#pragma warning disable CA1031 // Background task: errors must not propagate
        catch (Exception ex)
        {
            _logger.LogError(ex, "Service call log retention failed");
        }
#pragma warning restore CA1031
    }
}
