using Api.BoundedContexts.Administration.Application.Interfaces;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Enums;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Application.Queries.Testing;

/// <summary>
/// Handler for GetE2EMetricsQuery (Issue #2139)
/// Retrieves E2E test metrics from Playwright reports
/// </summary>
internal class GetE2EMetricsQueryHandler : IQueryHandler<GetE2EMetricsQuery, E2EMetricsDto>
{
    private readonly IPlaywrightReportParserService _playwrightParser;
    private readonly ILogger<GetE2EMetricsQueryHandler> _logger;

    public GetE2EMetricsQueryHandler(
        IPlaywrightReportParserService playwrightParser,
        ILogger<GetE2EMetricsQueryHandler> logger)
    {
        _playwrightParser = playwrightParser ?? throw new ArgumentNullException(nameof(playwrightParser));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<E2EMetricsDto> Handle(GetE2EMetricsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        _logger.LogInformation("Retrieving E2E test metrics from Playwright reports");

        var metrics = await _playwrightParser.ParseE2EMetricsAsync(cancellationToken).ConfigureAwait(false);

        if (metrics == null)
        {
            _logger.LogWarning("No Playwright reports found, returning default E2E metrics");

            // Return default metrics when reports are not available
            return new E2EMetricsDto(
                Coverage: 0,
                PassRate: 0,
                FlakyRate: 0,
                ExecutionTime: 0,
                TotalTests: 0,
                PassedTests: 0,
                FailedTests: 0,
                SkippedTests: 0,
                FlakyTests: 0,
                LastRunAt: DateTime.UtcNow,
                Status: TestExecutionStatus.NoData,
                MeetsQualityStandards: false);
        }

        _logger.LogInformation(
            "Successfully retrieved E2E metrics: Coverage={Coverage}%, Pass={PassRate}%, Flaky={FlakyRate}%, Total={Total}, Status={Status}",
            metrics.Coverage,
            metrics.PassRate,
            metrics.FlakyRate,
            metrics.TotalTests,
            metrics.Status);

        return E2EMetricsDto.FromValueObject(metrics);
    }
}
