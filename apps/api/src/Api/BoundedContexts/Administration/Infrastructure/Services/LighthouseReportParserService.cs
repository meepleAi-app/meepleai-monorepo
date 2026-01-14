using System.Text.Json;
using System.Globalization;
using Api.BoundedContexts.Administration.Application.Interfaces;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.SharedKernel.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Infrastructure.Services;

/// <summary>
/// Service for parsing Lighthouse CI reports (Issue #2139)
/// Reads JSON reports from apps/web/.lighthouseci/
/// </summary>
internal class LighthouseReportParserService : ILighthouseReportParserService
{
    private readonly ILogger<LighthouseReportParserService> _logger;
    private readonly string _reportDirectory;

    public LighthouseReportParserService(
        IConfiguration configuration,
        ILogger<LighthouseReportParserService> logger)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Default to relative path from API project
        _reportDirectory = configuration["Lighthouse:ReportDirectory"]
            ?? Path.Combine("..", "..", "..", "apps", "web", ".lighthouseci");

        _logger.LogInformation("LighthouseReportParserService initialized with directory: {Directory}", _reportDirectory);
    }

    public async Task<AccessibilityMetrics?> ParseAccessibilityMetricsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var latestReport = await GetLatestReportAsync().ConfigureAwait(false);
            if (latestReport == null)
            {
                _logger.LogWarning("No Lighthouse reports found in directory: {Directory}", _reportDirectory);
                return null;
            }

            // Parse accessibility category
            var accessibilityScore = GetNumericValue(latestReport, "categories", "accessibility", "score") * 100;

            // Parse axe violations from audits
            var axeViolations = GetNumericValue(latestReport, "audits", "accesskeys", "details", "items");
            var violationCount = (int)axeViolations;

            // Determine WCAG levels based on score
            var wcagLevels = new List<string>();
            if (accessibilityScore >= 50) wcagLevels.Add("A");
            if (accessibilityScore >= 75) wcagLevels.Add("AA");
            if (accessibilityScore >= 90) wcagLevels.Add("AAA");

            // Determine status based on accessibility score thresholds
            var status = accessibilityScore >= 90
                ? TestExecutionStatus.Pass
                : accessibilityScore >= 75
                    ? TestExecutionStatus.Warning
                    : TestExecutionStatus.Fail;

            var lastRunAt = File.GetLastWriteTimeUtc(latestReport);

            var metrics = AccessibilityMetrics.Create(
                lighthouseScore: (decimal)accessibilityScore,
                axeViolations: violationCount,
                wcagLevels: wcagLevels,
                lastRunAt: lastRunAt,
                status: status);

            _logger.LogInformation(
                "Parsed accessibility metrics: Score={Score}, Violations={Violations}, Status={Status}",
                accessibilityScore,
                violationCount,
                status);

            return metrics;
        }
#pragma warning disable CA1031
#pragma warning disable S125 // Sections of code should not be commented out
        // ADAPTER PATTERN: INFRASTRUCTURE SERVICE PATTERN - Graceful degradation
        // Catches all file I/O and JSON parsing failures. Returns null instead of throwing
        // to allow dashboard to handle missing metrics gracefully. Non-critical data retrieval.
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing Lighthouse accessibility metrics");
            return null;
        }
#pragma warning restore CA1031
    }

    public async Task<PerformanceMetrics?> ParsePerformanceMetricsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var latestReport = await GetLatestReportAsync().ConfigureAwait(false);
            if (latestReport == null)
            {
                _logger.LogWarning("No Lighthouse reports found in directory: {Directory}", _reportDirectory);
                return null;
            }

            // Parse performance category
            var performanceScore = GetNumericValue(latestReport, "categories", "performance", "score") * 100;

            // Parse Core Web Vitals from audits
            var lcp = GetNumericValue(latestReport, "audits", "largest-contentful-paint", "numericValue");
            var fid = GetNumericValue(latestReport, "audits", "max-potential-fid", "numericValue");
            var cls = GetNumericValue(latestReport, "audits", "cumulative-layout-shift", "numericValue");
            var fcp = GetNumericValue(latestReport, "audits", "first-contentful-paint", "numericValue");
            var tti = GetNumericValue(latestReport, "audits", "interactive", "numericValue");
            var tbt = GetNumericValue(latestReport, "audits", "total-blocking-time", "numericValue");
            var speedIndex = GetNumericValue(latestReport, "audits", "speed-index", "numericValue");

            var lastRunAt = File.GetLastWriteTimeUtc(latestReport);

            // Use factory method to create metrics (calculates budgetStatus automatically)
            var metrics = PerformanceMetrics.FromLighthouseReport(
                lcp: (decimal)lcp,
                fid: (decimal)fid,
                cls: (decimal)cls,
                fcp: (decimal)fcp,
                tti: (decimal)tti,
                tbt: (decimal)tbt,
                speedIndex: (decimal)speedIndex,
                performanceScore: (decimal)performanceScore,
                lastRunAt: lastRunAt);

            _logger.LogInformation(
                "Parsed performance metrics: Score={Score}, LCP={Lcp}ms, FID={Fid}ms, CLS={Cls}",
                metrics.PerformanceScore,
                metrics.Lcp,
                metrics.Fid,
                metrics.Cls);

            return metrics;
        }
#pragma warning disable CA1031
#pragma warning disable S125 // Sections of code should not be commented out
        // ADAPTER PATTERN: INFRASTRUCTURE SERVICE PATTERN - Graceful degradation
        // Catches all file I/O and JSON parsing failures. Returns null instead of throwing
        // to allow dashboard to handle missing metrics gracefully. Non-critical data retrieval.
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing Lighthouse performance metrics");
            return null;
        }
#pragma warning restore CA1031
    }

    public async Task<bool> ReportsExistAsync(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);

        if (!Directory.Exists(_reportDirectory))
        {
            _logger.LogDebug("Lighthouse report directory does not exist: {Directory}", _reportDirectory);
            return false;
        }

        var reportFiles = Directory.GetFiles(_reportDirectory, "*.json");
        var exists = reportFiles.Length > 0;

        _logger.LogDebug("Lighthouse reports exist: {Exists} (found {Count} files)", exists, reportFiles.Length);

        return exists;
    }

    /// <summary>
    /// Gets the latest Lighthouse report file path
    /// </summary>
    private async Task<string?> GetLatestReportAsync()
    {
        await Task.CompletedTask.ConfigureAwait(false);

        if (!Directory.Exists(_reportDirectory))
        {
            return null;
        }

        var reportFiles = Directory.GetFiles(_reportDirectory, "*.json")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToList();

        return reportFiles.FirstOrDefault();
    }

    /// <summary>
    /// Safely extracts a numeric value from nested JSON structure
    /// </summary>
    private static double GetNumericValue(string reportPath, params string[] path)
    {
        try
        {
            var json = File.ReadAllText(reportPath);
            using var doc = JsonDocument.Parse(json);

            var element = doc.RootElement;
            foreach (var key in path)
            {
                if (!element.TryGetProperty(key, out var next))
                {
                    return 0;
                }
                element = next;
            }

            return element.ValueKind switch
            {
                JsonValueKind.Number => element.GetDouble(),
                JsonValueKind.String when double.TryParse(element.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var val) => val,
                _ => 0
            };
        }
        catch
        {
            return 0;
        }
    }
}
