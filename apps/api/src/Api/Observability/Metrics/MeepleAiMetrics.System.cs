// OPS-02: System/API error metrics and background job metrics
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Api.Observability;

internal static partial class MeepleAiMetrics
{
    /// <summary>
    /// Counter for API errors with detailed categorization.
    /// Tracks all API errors by endpoint, status code, exception type, and severity.
    /// Complements OpenTelemetry's http_server_request_duration_count with exception-level detail.
    /// </summary>
    public static readonly Counter<long> ApiErrorsTotal = Meter.CreateCounter<long>(
        name: "meepleai.api.errors.total",
        unit: "errors",
        description: "Total number of API errors by endpoint, status code, exception type, and severity");

    /// <summary>
    /// Counter for unhandled exceptions that bubble to exception middleware.
    /// These are the most critical errors - unexpected failures not caught by endpoint handlers.
    /// High values indicate code quality issues or missing error handling.
    /// </summary>
    public static readonly Counter<long> UnhandledErrorsTotal = Meter.CreateCounter<long>(
        name: "meepleai.api.errors.unhandled",
        unit: "errors",
        description: "Total number of unhandled exceptions caught by exception middleware");

    /// <summary>
    /// Counter for orphaned analysis tasks cleaned up by background job.
    /// ISSUE-2528: Tracks automatic cleanup of stale failed/cancelled tasks.
    /// </summary>
    public static readonly Counter<long> OrphanedTasksCleanedTotal = Meter.CreateCounter<long>(
        name: "meepleai.background.orphaned_tasks_cleaned.total",
        unit: "tasks",
        description: "Total number of orphaned analysis tasks cleaned up by background job");

    /// <summary>
    /// Records an API error with detailed categorization for OPS-05 error monitoring.
    /// Captures endpoint, HTTP status code, exception type, severity, and error category.
    /// </summary>
    /// <param name="exception">The exception that occurred</param>
    /// <param name="httpStatusCode">HTTP status code to be returned</param>
    /// <param name="endpoint">API endpoint path (route template, not actual path with IDs)</param>
    /// <param name="isUnhandled">True if this is an unhandled exception caught by middleware</param>
    public static void RecordApiError(
        Exception exception,
        int httpStatusCode,
        string? endpoint = null,
        bool isUnhandled = false)
    {
        ArgumentNullException.ThrowIfNull(exception);
        // Determine severity based on HTTP status code
        var severity = httpStatusCode switch
        {
            >= 500 => "critical", // Server errors
            >= 400 => "warning",  // Client errors
            _ => "info"           // Unexpected, but defensive
        };

        // Categorize error by exception type for better alerting
        var errorCategory = ClassifyException(exception, httpStatusCode);

        // Build tags for the error counter
        var tags = new TagList
        {
            { "http.status_code", httpStatusCode },
            { "exception.type", exception.GetType().Name },
            { "severity", severity },
            { "error.category", errorCategory }
        };

        // Include endpoint if available (use route template to avoid high cardinality)
        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            tags.Add("http.route", endpoint);
        }

        // Record general API error counter
        ApiErrorsTotal.Add(1, tags);

        // Record unhandled error counter if applicable
        if (isUnhandled)
        {
            UnhandledErrorsTotal.Add(1, tags);
        }
    }

    /// <summary>
    /// Classifies an exception into a category for error monitoring.
    /// Categories: validation, system, dependency, timeout, authorization, notfound.
    /// </summary>
    private static string ClassifyException(Exception exception, int httpStatusCode)
    {
        // Classification based on exception type
        return exception switch
        {
            ArgumentException or ArgumentNullException => "validation",
            UnauthorizedAccessException => "authorization",
            InvalidOperationException when exception.Message.Contains("not found", StringComparison.OrdinalIgnoreCase) => "notfound",
            InvalidOperationException => "system",
            TimeoutException => "timeout",
            HttpRequestException or TaskCanceledException => "dependency",
            _ when httpStatusCode == 404 => "notfound",
            _ when httpStatusCode >= 500 => "system",
            _ when httpStatusCode >= 400 => "validation",
            _ => "unknown"
        };
    }
}
