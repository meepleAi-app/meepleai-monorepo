using Api.BoundedContexts.Administration.Application.Interfaces;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Application.Queries.Testing;

/// <summary>
/// Handler for GetAccessibilityMetricsQuery (Issue #2139)
/// Retrieves accessibility metrics from Lighthouse reports
/// </summary>
public class GetAccessibilityMetricsQueryHandler : IQueryHandler<GetAccessibilityMetricsQuery, AccessibilityMetricsDto>
{
    private readonly ILighthouseReportParserService _lighthouseParser;
    private readonly ILogger<GetAccessibilityMetricsQueryHandler> _logger;

    public GetAccessibilityMetricsQueryHandler(
        ILighthouseReportParserService lighthouseParser,
        ILogger<GetAccessibilityMetricsQueryHandler> logger)
    {
        _lighthouseParser = lighthouseParser ?? throw new ArgumentNullException(nameof(lighthouseParser));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AccessibilityMetricsDto> Handle(GetAccessibilityMetricsQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving accessibility metrics from Lighthouse reports");

        var metrics = await _lighthouseParser.ParseAccessibilityMetricsAsync(cancellationToken).ConfigureAwait(false);

        if (metrics == null)
        {
            _logger.LogWarning("No Lighthouse reports found, returning default accessibility metrics");

            // Return default metrics when reports are not available
            return new AccessibilityMetricsDto(
                LighthouseScore: 0,
                AxeViolations: 0,
                WcagLevels: Array.Empty<string>(),
                LastRunAt: DateTime.UtcNow,
                Status: "no-data",
                MeetsQualityStandards: false);
        }

        _logger.LogInformation(
            "Successfully retrieved accessibility metrics: Score={Score}, Violations={Violations}, Status={Status}",
            metrics.LighthouseScore,
            metrics.AxeViolations,
            metrics.Status);

        return AccessibilityMetricsDto.FromValueObject(metrics);
    }
}
