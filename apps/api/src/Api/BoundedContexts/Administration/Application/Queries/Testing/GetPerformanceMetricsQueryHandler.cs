using Api.BoundedContexts.Administration.Application.Interfaces;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Enums;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Application.Queries.Testing;

/// <summary>
/// Handler for GetPerformanceMetricsQuery (Issue #2139)
/// Retrieves performance metrics from Lighthouse reports
/// </summary>
internal class GetPerformanceMetricsQueryHandler : IQueryHandler<GetPerformanceMetricsQuery, PerformanceMetricsDto>
{
    private readonly ILighthouseReportParserService _lighthouseParser;
    private readonly ILogger<GetPerformanceMetricsQueryHandler> _logger;

    public GetPerformanceMetricsQueryHandler(
        ILighthouseReportParserService lighthouseParser,
        ILogger<GetPerformanceMetricsQueryHandler> logger)
    {
        _lighthouseParser = lighthouseParser ?? throw new ArgumentNullException(nameof(lighthouseParser));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PerformanceMetricsDto> Handle(GetPerformanceMetricsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        _logger.LogInformation("Retrieving performance metrics from Lighthouse reports");

        var metrics = await _lighthouseParser.ParsePerformanceMetricsAsync(cancellationToken).ConfigureAwait(false);

        if (metrics == null)
        {
            _logger.LogWarning("No Lighthouse reports found, returning default performance metrics");

            // Return default metrics when reports are not available
            return new PerformanceMetricsDto(
                Lcp: 0,
                Fid: 0,
                Cls: 0,
                Fcp: 0,
                Tti: 0,
                Tbt: 0,
                SpeedIndex: 0,
                PerformanceScore: 0,
                BudgetStatus: PerformanceBudgetStatus.NoData,
                LastRunAt: DateTime.UtcNow,
                MeetsCoreWebVitals: false);
        }

        _logger.LogInformation(
            "Successfully retrieved performance metrics: Score={Score}, LCP={Lcp}ms, FID={Fid}ms, CLS={Cls}, Budget={Budget}",
            metrics.PerformanceScore,
            metrics.Lcp,
            metrics.Fid,
            metrics.Cls,
            metrics.BudgetStatus);

        return PerformanceMetricsDto.FromValueObject(metrics);
    }
}
