using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

/// <summary>
/// Query returning the raw per-step duration statistics. Unlike
/// <see cref="GetProcessingMetricsQuery"/> (which returns a transformed ProcessingMetricsDto that
/// splits averages and percentiles), this preserves the <see cref="StepDurationStats"/> records the
/// RAG pipeline health endpoint consumes. Issue #3176 (CQRS conformity: endpoints must use
/// IMediator, not services).
/// </summary>
internal record GetStepDurationStatsQuery : IQuery<Dictionary<string, StepDurationStats>>;

internal sealed class GetStepDurationStatsQueryHandler
    : IRequestHandler<GetStepDurationStatsQuery, Dictionary<string, StepDurationStats>>
{
    private readonly IProcessingMetricsService _metricsService;

    public GetStepDurationStatsQueryHandler(IProcessingMetricsService metricsService)
    {
        _metricsService = metricsService ?? throw new ArgumentNullException(nameof(metricsService));
    }

    public Task<Dictionary<string, StepDurationStats>> Handle(
        GetStepDurationStatsQuery request,
        CancellationToken cancellationToken)
        => _metricsService.GetAllStepStatisticsAsync(cancellationToken);
}
