using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Api.BoundedContexts.DocumentProcessing.Application.Jobs;

/// <summary>
/// #3435 (SP1 slice 2): periodic trigger for the automatic image-region hi_res seed. Dispatches
/// <see cref="RunImageRegionSeedBatchCommand"/> on each tick; the handler is a no-op unless
/// <c>PdfProcessing:ImageRegionSeeding:Enabled</c> is true, so this job is safe to register while the
/// feature is flag-gated off (it just runs one cheap config check per tick).
/// </summary>
/// <remarks>
/// <see cref="DisallowConcurrentExecutionAttribute"/> serializes overlapping Quartz fires (a slow
/// ~200s hi_res batch must not stack on the next tick). A manual admin trigger of the same command can
/// still overlap a Quartz run, but both are idempotent (replace-by-pdf + the ImageRegionsSeededAt
/// marker), so the worst case is a rare wasted hi_res pass on one PDF.
/// </remarks>
[DisallowConcurrentExecution]
public sealed class SeedImageRegionsJob : IJob
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SeedImageRegionsJob> _logger;

    public SeedImageRegionsJob(IServiceProvider serviceProvider, ILogger<SeedImageRegionsJob> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var ct = context.CancellationToken;
        _logger.LogDebug("SeedImageRegionsJob started: FireTime={FireTime}", context.FireTimeUtc);

        using var scope = _serviceProvider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new RunImageRegionSeedBatchCommand(), ct).ConfigureAwait(false);

        if (result.Enabled && (result.Processed > 0 || result.Failed > 0))
        {
            _logger.LogInformation(
                "SeedImageRegionsJob: processed={Processed}, regionsSeeded={Seeded}, failed={Failed}",
                result.Processed, result.TotalRegionsSeeded, result.Failed);
        }
    }
}
