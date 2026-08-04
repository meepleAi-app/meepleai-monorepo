using System;
using System.Threading.Tasks;
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Api.BoundedContexts.DocumentProcessing.Application.Jobs;

/// <summary>
/// #3435 (SP4): periodic Quartz trigger for the async VLM table-extraction batch. Thin dispatcher —
/// resolves <see cref="IMediator"/> in a scope, sends <see cref="RunTableExtractionBatchCommand"/>,
/// and swallows non-cancellation exceptions so a transient failure doesn't suspend the trigger. A
/// cheap no-op (one config check) while <c>PdfProcessing:TableExtraction:Enabled</c> is off. Mirrors
/// <see cref="SeedImageRegionsJob"/>.
/// </summary>
[DisallowConcurrentExecution]
public sealed class RunTableExtractionJob : IJob
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RunTableExtractionJob> _logger;

    public RunTableExtractionJob(IServiceProvider serviceProvider, ILogger<RunTableExtractionJob> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var result = await mediator.Send(new RunTableExtractionBatchCommand(), ct).ConfigureAwait(false);
            if (result.Enabled && (result.Processed > 0 || result.Failed > 0))
            {
                _logger.LogInformation(
                    "RunTableExtractionJob: processed={Processed}, extracted={Extracted}, notTable={NotTable}, failed={Failed}",
                    result.Processed, result.Extracted, result.NotTable, result.Failed);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "RunTableExtractionJob failed; will retry on the next trigger");
        }
    }
}
