using System;
using Api.Middleware.Exceptions;
using Api.Observability;
using Api.SharedKernel.Domain.Exceptions;
using FluentValidation;

#pragma warning disable MA0048 // File name must match type name - Contains middleware and extension methods
namespace Api.Middleware;

/// <summary>
/// Middleware that catches unhandled exceptions in API endpoints and returns JSON error responses.
/// Only processes /api/* paths - other paths fall through to default exception handling.
/// Returns structured error responses with correlation IDs for debugging.
/// OPS-05: Records error metrics for monitoring and alerting.
/// </summary>
internal class ApiExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiExceptionHandlerMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ApiExceptionHandlerMiddleware(
        RequestDelegate next,
        ILogger<ApiExceptionHandlerMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // MIDDLEWARE BOUNDARY PATTERN: Global exception handler
#pragma warning restore S125
        catch (Exception ex)
        {
            // Only handle /api/* paths
            if (!context.Request.Path.StartsWithSegments("/api", StringComparison.Ordinal))
            {
                throw;
            }

            await HandleExceptionAsync(context, ex).ConfigureAwait(false);
        }
#pragma warning restore CA1031
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        // Log the exception with full details
        var sanitizedPath = SanitizePath(context.Request.Path);

        _logger.LogError(ex,
            "Unhandled exception in API endpoint. Path: {Path}, Method: {Method}, TraceId: {TraceId}",
            sanitizedPath,
            context.Request.Method,
            context.TraceIdentifier);

        // Special handling for FluentValidation exceptions (Issue #1449)
        if (ex is FluentValidation.ValidationException fluentValidationEx)
        {
            await HandleFluentValidationExceptionAsync(context, fluentValidationEx).ConfigureAwait(false);
            return;
        }

        // Determine status code and error type based on exception type
        var (statusCode, errorType, message) = MapExceptionToResponse(ex);

        // OPS-05: Record error metrics for monitoring and alerting
        // Use route pattern for endpoint to avoid high cardinality (e.g., /api/v1/games instead of /api/v1/games/{id})
        var endpoint = GetRoutePattern(context) ?? context.Request.Path.ToString();
        MeepleAiMetrics.RecordApiError(
            exception: ex,
            httpStatusCode: statusCode,
            endpoint: endpoint,
            isUnhandled: true); // This is unhandled since it reached middleware

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var errorResponse = new
        {
            error = errorType,
            message = message,
            correlationId = context.TraceIdentifier,
            timestamp = DateTime.UtcNow,
            // Include stack trace only in development
            stackTrace = _environment.IsDevelopment() ? ex.StackTrace : null
        };

        await context.Response.WriteAsJsonAsync(errorResponse).ConfigureAwait(false);
    }

    /// <summary>
    /// Handles FluentValidation exceptions with HTTP 422 and structured error messages.
    /// Issue #1449: Returns validation errors in a structured format for client consumption.
    /// </summary>
    private async Task HandleFluentValidationExceptionAsync(
        HttpContext context,
        FluentValidation.ValidationException validationException)
    {
        var endpoint = GetRoutePattern(context) ?? context.Request.Path.ToString();

        // Record validation error metrics
        MeepleAiMetrics.RecordApiError(
            exception: validationException,
            httpStatusCode: StatusCodes.Status422UnprocessableEntity,
            endpoint: endpoint,
            isUnhandled: true);

        context.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
        context.Response.ContentType = "application/json";

        // Group validation errors by property name
        var errors = validationException.Errors
            .GroupBy(e => e.PropertyName, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray()
, StringComparer.Ordinal);

        var errorResponse = new
        {
            error = "validation_error",
            message = "One or more validation errors occurred",
            errors = errors,
            correlationId = context.TraceIdentifier,
            timestamp = DateTime.UtcNow
        };

        await context.Response.WriteAsJsonAsync(errorResponse).ConfigureAwait(false);
    }

    /// <summary>
    /// Extracts the route pattern from the HttpContext to avoid high cardinality metrics.
    /// Returns route template like "/api/v1/games/{id}" instead of "/api/v1/games/abc-123".
    /// </summary>
    private static string? GetRoutePattern(HttpContext context)
    {
        // Try to get the route pattern from endpoint metadata
        var endpoint = context.GetEndpoint();
        if (endpoint is RouteEndpoint routeEndpoint)
        {
            return routeEndpoint.RoutePattern.RawText;
        }

        // Fallback to request path if route pattern not available
        return null;
    }

    private static string SanitizePath(PathString path)
    {
        var rawPath = path.ToString();

        if (string.IsNullOrEmpty(rawPath))
        {
            return rawPath;
        }

        return rawPath.Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal);
    }

    private static (int StatusCode, string ErrorType, string Message) MapExceptionToResponse(Exception ex)
    {
        return ex switch
        {
            // Custom HTTP exceptions (from Middleware/Exceptions)
            HttpException httpEx => (
                httpEx.StatusCode,
                httpEx.ErrorCode,
                httpEx.Message
            ),
            NotFoundException notFoundEx => (
                StatusCodes.Status404NotFound,
                "not_found",
                notFoundEx.Message
            ),

            // Domain exceptions (from SharedKernel)
            Api.SharedKernel.Domain.Exceptions.ValidationException validationEx => (
                StatusCodes.Status400BadRequest,
                "validation_error",
                validationEx.Message
            ),
            DomainException domainEx => (
                StatusCodes.Status400BadRequest,
                "domain_error",
                domainEx.Message
            ),

            // System exceptions with specific mappings
            ArgumentException or ArgumentNullException => (
                StatusCodes.Status400BadRequest,
                "bad_request",
                "Invalid request parameters"
            ),
            UnauthorizedAccessException => (
                StatusCodes.Status403Forbidden,
                "forbidden",
                "Access denied"
            ),
            KeyNotFoundException => (
                StatusCodes.Status404NotFound,
                "not_found",
                "Resource not found"
            ),
            InvalidOperationException when ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase) => (
                StatusCodes.Status404NotFound,
                "not_found",
                "Resource not found"
            ),
            TimeoutException => (
                StatusCodes.Status504GatewayTimeout,
                "timeout",
                "Request timed out"
            ),

            // Default fallback for unknown exceptions
            _ => (
                StatusCodes.Status500InternalServerError,
                "internal_server_error",
                "An unexpected error occurred"
            )
        };
    }
}

/// <summary>
/// Extension methods for registering API exception handler middleware.
/// </summary>
internal static class ApiExceptionHandlerMiddlewareExtensions
{
    /// <summary>
    /// Adds API exception handler middleware to the pipeline.
    /// Should be called early in the pipeline, before other middleware.
    /// </summary>
    public static IApplicationBuilder UseApiExceptionHandler(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ApiExceptionHandlerMiddleware>();
    }
}