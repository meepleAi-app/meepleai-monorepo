namespace Api.Middleware;

/// <summary>
/// Middleware that catches unhandled exceptions in API endpoints and returns JSON error responses.
/// Only processes /api/* paths - other paths fall through to default exception handling.
/// Returns structured error responses with correlation IDs for debugging.
/// </summary>
public class ApiExceptionHandlerMiddleware
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
            await _next(context);
        }
        catch (Exception ex)
        {
            // Only handle /api/* paths
            if (!context.Request.Path.StartsWithSegments("/api"))
            {
                throw;
            }

            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        // Log the exception with full details
        _logger.LogError(ex,
            "Unhandled exception in API endpoint. Path: {Path}, Method: {Method}, TraceId: {TraceId}",
            context.Request.Path,
            context.Request.Method,
            context.TraceIdentifier);

        // Determine status code and error type based on exception type
        var (statusCode, errorType, message) = MapExceptionToResponse(ex);

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

        await context.Response.WriteAsJsonAsync(errorResponse);
    }

    private static (int StatusCode, string ErrorType, string Message) MapExceptionToResponse(Exception ex)
    {
        return ex switch
        {
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
            InvalidOperationException when ex.Message.Contains("not found") => (
                StatusCodes.Status404NotFound,
                "not_found",
                "Resource not found"
            ),
            TimeoutException => (
                StatusCodes.Status504GatewayTimeout,
                "timeout",
                "Request timed out"
            ),
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
public static class ApiExceptionHandlerMiddlewareExtensions
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
