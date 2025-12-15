using System.Text.Json;
using Api.BoundedContexts.Administration.Application.Interfaces;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Infrastructure.Services;

/// <summary>
/// Service for parsing Playwright test reports (Issue #2139)
/// Reads JSON reports from apps/web/playwright-report/
/// </summary>
internal class PlaywrightReportParserService : IPlaywrightReportParserService
{
    private readonly ILogger<PlaywrightReportParserService> _logger;
    private readonly string _reportDirectory;

    public PlaywrightReportParserService(
        IConfiguration configuration,
        ILogger<PlaywrightReportParserService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Default to relative path from API project
        _reportDirectory = configuration["Playwright:ReportDirectory"]
            ?? Path.Combine("..", "..", "..", "apps", "web", "playwright-report");

        _logger.LogInformation("PlaywrightReportParserService initialized with directory: {Directory}", _reportDirectory);
    }

    public async Task<E2EMetrics?> ParseE2EMetricsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var latestReport = await GetLatestReportAsync(cancellationToken).ConfigureAwait(false);
            if (latestReport == null)
            {
                _logger.LogWarning("No Playwright reports found in directory: {Directory}", _reportDirectory);
                return null;
            }

            var json = await File.ReadAllTextAsync(latestReport, cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Parse test results from Playwright JSON report
            var stats = root.GetProperty("stats");

            var totalTests = stats.TryGetProperty("total", out var total) ? total.GetInt32() : 0;
            var passedTests = stats.TryGetProperty("expected", out var passed) ? passed.GetInt32() : 0;
            var failedTests = stats.TryGetProperty("unexpected", out var failed) ? failed.GetInt32() : 0;
            var skippedTests = stats.TryGetProperty("skipped", out var skipped) ? skipped.GetInt32() : 0;
            var flakyTests = stats.TryGetProperty("flaky", out var flaky) ? flaky.GetInt32() : 0;

            // Calculate metrics
            var passRate = totalTests > 0 ? (decimal)passedTests / totalTests * 100 : 0;
            var flakyRate = totalTests > 0 ? (decimal)flakyTests / totalTests * 100 : 0;

            // Parse execution time (milliseconds)
            var duration = stats.TryGetProperty("duration", out var dur) ? dur.GetInt64() : 0;
            var executionTime = totalTests > 0 ? (decimal)duration / totalTests : 0;

            // Calculate coverage (placeholder - actual coverage requires separate coverage report)
            // For now, use a simple heuristic based on number of tests
            var coverage = totalTests > 0 ? Math.Min(100, totalTests * 0.5m) : 0;

            // Determine status
            var status = passRate >= 95 && flakyRate <= 5 ? "pass" :
                        passRate >= 80 && flakyRate <= 10 ? "warning" : "fail";

            var lastRunAt = File.GetLastWriteTimeUtc(latestReport);

            var metrics = new E2EMetrics(
                coverage: coverage,
                passRate: passRate,
                flakyRate: flakyRate,
                executionTime: executionTime,
                totalTests: totalTests,
                passedTests: passedTests,
                failedTests: failedTests,
                skippedTests: skippedTests,
                flakyTests: flakyTests,
                lastRunAt: lastRunAt,
                status: status);

            _logger.LogInformation(
                "Parsed E2E metrics: Total={Total}, Passed={Passed}, Failed={Failed}, PassRate={PassRate}%, Status={Status}",
                totalTests,
                passedTests,
                failedTests,
                passRate,
                status);

            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing Playwright E2E metrics");
            return null;
        }
    }

    public async Task<bool> ReportsExistAsync(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);

        if (!Directory.Exists(_reportDirectory))
        {
            _logger.LogDebug("Playwright report directory does not exist: {Directory}", _reportDirectory);
            return false;
        }

        var reportFiles = Directory.GetFiles(_reportDirectory, "*.json");
        var exists = reportFiles.Length > 0;

        _logger.LogDebug("Playwright reports exist: {Exists} (found {Count} files)", exists, reportFiles.Length);

        return exists;
    }

    /// <summary>
    /// Gets the latest Playwright report file path
    /// </summary>
    private async Task<string?> GetLatestReportAsync()
    {
        await Task.CompletedTask.ConfigureAwait(false);

        if (!Directory.Exists(_reportDirectory))
        {
            return null;
        }

        var reportFiles = Directory.GetFiles(_reportDirectory, "*.json")
            .Where(f => !f.EndsWith("manifest.json", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToList();

        return reportFiles.FirstOrDefault();
    }
}
