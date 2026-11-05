using Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Jobs;

/// <summary>
/// Recurring job (#4503): sends <see cref="ExpireStaleMechanicAnalysisDraftsCommand"/> to reject the mechanic analyses
/// left in <c>Draft</c> by a pipeline that died with the process, so they stop holding their (game, PDF, prompt) slot.
/// </summary>
[DisallowConcurrentExecution]
public sealed class MechanicAnalysisStaleDraftExpirationJob : IJob
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MechanicAnalysisStaleDraftExpirationJob> _logger;

    public MechanicAnalysisStaleDraftExpirationJob(
        IServiceProvider serviceProvider,
        ILogger<MechanicAnalysisStaleDraftExpirationJob> logger)
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
                .Send(new ExpireStaleMechanicAnalysisDraftsCommand(), ct)
                .ConfigureAwait(false);
            _logger.LogInformation(
                "MechanicAnalysisStaleDraftExpirationJob: found={Found}, expired={Expired}.",
                result.Found, result.Expired);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "MechanicAnalysisStaleDraftExpirationJob failed.");
        }
    }
}
