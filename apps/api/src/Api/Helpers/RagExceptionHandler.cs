using System.Diagnostics;
using Api.Observability;
using Microsoft.Extensions.Logging;

namespace Api.Helpers;

/// <summary>
/// Centralized exception handling for RAG service operations.
/// Eliminates duplicate exception handling code across multiple methods.
/// </summary>
public static class RagExceptionHandler
{
    /// <summary>
    /// Handles RAG operation exceptions with consistent logging, metrics, and tracing.
    /// </summary>
    /// <typeparam name="TResponse">The response type to return</typeparam>
    /// <param name="exception">The exception that occurred</param>
    /// <param name="logger">Logger for recording the error</param>
    /// <param name="logAction">Action to log the error with context-specific message</param>
    /// <param name="gameId">Game ID for metrics tagging</param>
    /// <param name="operation">Operation name for metrics (e.g., "qa", "explain")</param>
    /// <param name="activity">OpenTelemetry activity span for tracing</param>
    /// <param name="stopwatch">Stopwatch for measuring duration</param>
    /// <param name="errorResponseFactory">Factory function to create the error response</param>
    /// <returns>Error response of type TResponse</returns>
    public static TResponse HandleException<TResponse>(
        Exception exception,
        ILogger logger,
        Action<ILogger, Exception> logAction,
        string gameId,
        string operation,
        Activity? activity,
        Stopwatch stopwatch,
        Func<TResponse> errorResponseFactory)
    {
        // Log the error with context
        logAction(logger, exception);

        // OPS-02: Record exception in trace span
        if (activity != null)
        {
            activity.SetTag("success", false);
            activity.SetTag("error.type", exception.GetType().Name);
            activity.SetTag("error.message", exception.Message);
            activity.SetStatus(ActivityStatusCode.Error, exception.Message);
        }

        // OPS-02: Record error metrics
        stopwatch.Stop();
        MeepleAiMetrics.RagErrorsTotal.Add(1, new TagList
        {
            { "game.id", gameId },
            { "operation", operation },
            { "error.type", exception.GetType().Name }
        });
        MeepleAiMetrics.RecordRagRequest(stopwatch.Elapsed.TotalMilliseconds, gameId, success: false);

        // Return error response
        return errorResponseFactory();
    }

    /// <summary>
    /// Gets a standardized log action for common exception types.
    /// </summary>
    public static Action<ILogger, Exception> GetLogAction(string exceptionType, string context, string gameId, string? additionalInfo = null)
    {
        return exceptionType switch
        {
            "HttpRequestException" => (logger, ex) =>
                logger.LogError(ex, $"HTTP request failed during {context} for game {{GameId}}" +
                    (additionalInfo != null ? $" - {additionalInfo}" : ""), gameId),

            "TaskCanceledException" => (logger, ex) =>
                logger.LogError(ex, $"{context} timed out for game {{GameId}}" +
                    (additionalInfo != null ? $" - {additionalInfo}" : ""), gameId),

            "InvalidOperationException" => (logger, ex) =>
                logger.LogError(ex, $"Invalid operation during {context} for game {{GameId}}" +
                    (additionalInfo != null ? $" - {additionalInfo}" : ""), gameId),

            "DbUpdateException" => (logger, ex) =>
                logger.LogError(ex, $"Database error during {context} for game {{GameId}}" +
                    (additionalInfo != null ? $" - {additionalInfo}" : ""), gameId),

            _ => (logger, ex) =>
                logger.LogError(ex, $"Unexpected error during {context} for game {{GameId}}" +
                    (additionalInfo != null ? $" - {additionalInfo}" : ""), gameId)
        };
    }
}
