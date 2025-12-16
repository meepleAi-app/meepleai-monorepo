using System.Diagnostics;
using System.Text.Json;
using Api.Observability;
using Microsoft.Extensions.Logging;

namespace Api.Helpers;

/// <summary>
/// Centralized exception handling for RAG service operations.
/// Eliminates duplicate exception handling code across multiple methods.
/// </summary>
internal static class RagExceptionHandler
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
        // Validate arguments
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(logAction);
        ArgumentNullException.ThrowIfNull(errorResponseFactory);

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
                logger.LogError(ex, "HTTP request failed during {Context} for game {GameId} - {AdditionalInfo}", context, gameId, additionalInfo ?? string.Empty),

            "TaskCanceledException" => (logger, ex) =>
                logger.LogError(ex, "{Context} timed out for game {GameId} - {AdditionalInfo}", context, gameId, additionalInfo ?? string.Empty),

            "InvalidOperationException" => (logger, ex) =>
                logger.LogError(ex, "Invalid operation during {Context} for game {GameId} - {AdditionalInfo}", context, gameId, additionalInfo ?? string.Empty),

            "DbUpdateException" => (logger, ex) =>
                logger.LogError(ex, "Database error during {Context} for game {GameId} - {AdditionalInfo}", context, gameId, additionalInfo ?? string.Empty),

            _ => (logger, ex) =>
                logger.LogError(ex, "Unexpected error during {Context} for game {GameId} - {AdditionalInfo}", context, gameId, additionalInfo ?? string.Empty)
        };
    }

    /// <summary>
    /// Issue #1441: Centralized exception handling dispatcher that routes exceptions to appropriate handlers.
    /// Eliminates duplicate catch blocks across RAG service methods.
    /// </summary>
    /// <typeparam name="TResponse">The response type to return</typeparam>
    /// <param name="exception">The exception that occurred</param>
    /// <param name="logger">Logger for recording the error</param>
    /// <param name="context">Context description (e.g., "RAG query", "RAG explain")</param>
    /// <param name="gameId">Game ID for metrics tagging</param>
    /// <param name="operation">Operation name for metrics (e.g., "qa", "explain")</param>
    /// <param name="activity">OpenTelemetry activity span for tracing</param>
    /// <param name="stopwatch">Stopwatch for measuring duration</param>
    /// <param name="errorResponseFactories">Dictionary mapping exception type names to error response factories</param>
    /// <returns>Error response of type TResponse</returns>
    public static TResponse HandleExceptionDispatch<TResponse>(
        Exception exception,
        ILogger logger,
        string context,
        string gameId,
        string operation,
        Activity? activity,
        Stopwatch stopwatch,
        Dictionary<string, Func<TResponse>> errorResponseFactories,
        string? additionalInfo = null)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(errorResponseFactories);

        var exceptionTypeName = exception.GetType().Name;

        // Get the appropriate error response factory, defaulting to generic Exception handler
        if (errorResponseFactories.Count == 0)
            throw new ArgumentException("Error response factories dictionary cannot be empty", nameof(errorResponseFactories));

        var errorResponseFactory = errorResponseFactories.TryGetValue(exceptionTypeName, out var factory)
            ? factory
            : errorResponseFactories.GetValueOrDefault("Exception") ?? errorResponseFactories.Values.ToList()[0];

        var logAction = GetLogAction(exceptionTypeName, context, gameId, additionalInfo);

        return HandleException(
            exception,
            logger,
            logAction,
            gameId,
            operation,
            activity,
            stopwatch,
            errorResponseFactory);
    }

    /// <summary>
    /// Issue #1444: Generic service exception handler for Result pattern.
    /// Handles exceptions from service operations that return result objects with Success/ErrorMessage properties.
    /// </summary>
    /// <typeparam name="TResult">Result type that supports CreateFailure(string) static method</typeparam>
    /// <param name="exception">The exception that occurred</param>
    /// <param name="logger">Logger for recording the error</param>
    /// <param name="context">Context description (e.g., "vector search", "embedding generation")</param>
    /// <param name="failureFactory">Factory function to create failure result from error message</param>
    /// <param name="activity">Optional OpenTelemetry activity span for tracing</param>
    /// <param name="useUserFriendlyMessage">If true, uses user-friendly messages instead of technical details</param>
    /// <returns>Failure result of type TResult</returns>
    public static TResult HandleServiceException<TResult>(
        Exception exception,
        ILogger logger,
        string context,
        Func<string, TResult> failureFactory,
        Activity? activity = null,
        bool useUserFriendlyMessage = true)
    {
        // Log the error with context (always technical for diagnostics)
        logger.LogError(exception, "Error during {Context}: {ErrorType} - {ErrorMessage}",
            context, exception.GetType().Name, exception.Message);

        // Record exception in trace span if available
        if (activity != null)
        {
            activity.SetTag("success", false);
            activity.SetTag("error.type", exception.GetType().Name);
            activity.SetTag("error.message", exception.Message);
            activity.SetStatus(ActivityStatusCode.Error, exception.Message);
        }

        // Generate user-facing error message
        var userMessage = useUserFriendlyMessage
            ? GetUserFriendlyMessage(exception, $"{context} failed: {exception.Message}")
            : $"{context} failed: {exception.Message}";

        // Return failure result with appropriate message
        return failureFactory(userMessage);
    }

    /// <summary>
    /// Issue #1444: Log and re-throw pattern for service boundaries.
    /// Logs exception with context before re-throwing for upstream handling.
    /// </summary>
    /// <param name="exception">The exception to log and re-throw</param>
    /// <param name="logger">Logger for recording the error</param>
    /// <param name="context">Context description for logging</param>
    /// <param name="additionalContext">Optional additional context data</param>
    public static void LogAndRethrow(
        Exception exception,
        ILogger logger,
        string context,
        params object?[] additionalContext)
    {
        // Log with context before re-throwing
        if (additionalContext.Length > 0)
        {
            logger.LogError(exception, "Error during {Context} - Additional context: {@AdditionalContext}", context, additionalContext);
        }
        else
        {
            logger.LogError(exception, "Error during {Context}", context);
        }

        // Re-throw for upstream handling (use 'throw;' to preserve stack trace)
#pragma warning disable S2139 // Log and rethrow is intentional for this helper
        throw exception;
#pragma warning restore S2139
    }

    /// <summary>
    /// Issue #1444: Get user-friendly error message based on exception type.
    /// Maps technical exceptions to user-facing messages while preserving detailed logging.
    /// </summary>
    /// <param name="exception">The exception to map</param>
    /// <param name="defaultMessage">Default message if no mapping exists</param>
    /// <returns>User-friendly error message</returns>
    public static string GetUserFriendlyMessage(Exception exception, string defaultMessage)
    {
        return exception switch
        {
            TaskCanceledException => "Request timed out. Please try again.",
            TimeoutException => "Request timed out. Please try again.",
            HttpRequestException httpEx => $"Network error: {(httpEx.StatusCode.HasValue ? $"HTTP {(int)httpEx.StatusCode}" : "Connection failed")}",
            JsonException => "Invalid response format from service.",
            InvalidOperationException invEx when invEx.Message.Contains("configuration", StringComparison.OrdinalIgnoreCase)
                => "Configuration error. Please contact support.",
            UnauthorizedAccessException => "Authentication failed. Please check your credentials.",
            ArgumentException => "Invalid request parameters.",
            _ => defaultMessage
        };
    }
}
