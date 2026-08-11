using Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Jobs;

/// <summary>
/// Hourly recurring job (#534 ME-M3.2): sends <see cref="RunMechanicCardAutoSuppressionCommand"/> to
/// aggregate mechanic-card feedback and auto-suppress cards breaching the admin-tunable thresholds.
/// </summary>
[DisallowConcurrentExecution]
public sealed class MechanicCardAutoSuppressionJob : IJob
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MechanicCardAutoSuppressionJob> _logger;

    public MechanicCardAutoSuppressionJob(
        IServiceProvider serviceProvider,
        ILogger<MechanicCardAutoSuppressionJob> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var ct = context.CancellationToken;

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var result = await mediator
                .Send(new RunMechanicCardAutoSuppressionCommand(), ct)
                .ConfigureAwait(false);
            _logger.LogInformation(
                "MechanicCardAutoSuppressionJob: evaluated={Evaluated}, suppressed={Suppressed}.",
                result.Evaluated, result.Suppressed);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "MechanicCardAutoSuppressionJob failed.");
        }
    }
}
