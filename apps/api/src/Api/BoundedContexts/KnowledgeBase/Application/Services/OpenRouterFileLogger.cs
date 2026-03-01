using System.Security.Cryptography;
using System.Text;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Serilog;
using Serilog.Formatting.Json;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Rotating JSONL file logger for OpenRouter requests and events.
/// Issue #5073: Daily rotation, 30-file retention, 100MB max size per file.
/// </summary>
/// <remarks>
/// Uses a dedicated Serilog sub-logger (separate from the main app logger) so OpenRouter logs
/// remain isolated in their own file without polluting other sinks.
/// User IDs are SHA-256 hashed before writing to avoid storing PII in log files.
/// Message content (prompts/completions) is never logged.
/// </remarks>
internal sealed class OpenRouterFileLogger : IOpenRouterFileLogger, IDisposable
{
    private readonly Serilog.ILogger _fileLogger;
    private bool _disposed;

    private const int FileSizeLimitBytes = 100 * 1024 * 1024; // 100 MB
    private const int RetainedFileCount = 30;

    public OpenRouterFileLogger(string logDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logDirectory);

        // Build path: {logDirectory}/openrouter-.log → Serilog appends date: openrouter-YYYY-MM-DD.log
        var logFilePath = Path.Combine(logDirectory, "openrouter-.log");

        _fileLogger = new Serilog.LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.File(
                formatter: new JsonFormatter(renderMessage: false),
                path: logFilePath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: RetainedFileCount,
                fileSizeLimitBytes: FileSizeLimitBytes,
                rollOnFileSizeLimit: true,
                shared: false,
                flushToDiskInterval: TimeSpan.FromSeconds(5))
            .CreateLogger();
    }

    /// <inheritdoc/>
    public void LogRequest(
        string requestId,
        string model,
        string provider,
        string source,
        Guid? userId,
        int promptTokens,
        int completionTokens,
        decimal costUsd,
        long latencyMs,
        bool success,
        bool isFreeModel,
        string? sessionId,
        string? errorMessage = null)
    {
        if (_disposed) return;

        _fileLogger.Information(
            "{@OpenRouterRequest}",
            new
            {
                request_id = requestId,
                model,
                provider,
                source,
                user_id = HashUserId(userId),
                prompt_tokens = promptTokens,
                completion_tokens = completionTokens,
                total_tokens = promptTokens + completionTokens,
                cost_usd = costUsd,
                latency_ms = latencyMs,
                success,
                is_free_model = isFreeModel,
                session_id = sessionId,
                error_message = errorMessage
            });
    }

    /// <inheritdoc/>
    public void LogRateLimitHit(
        string model,
        int httpStatus,
        int retryAfterMs,
        int currentRpm,
        int limitRpm)
    {
        if (_disposed) return;

        _fileLogger.Warning(
            "{@RateLimitHit}",
            new
            {
                @event = "rate_limit_hit",
                model,
                http_status = httpStatus,
                retry_after_ms = retryAfterMs,
                current_rpm = currentRpm,
                limit_rpm = limitRpm
            });
    }

    /// <inheritdoc/>
    public void LogCircuitBreakerEvent(
        string provider,
        string newState,
        int consecutiveFailures)
    {
        if (_disposed) return;

        _fileLogger.Warning(
            "{@CircuitBreakerEvent}",
            new
            {
                @event = "circuit_breaker_state_change",
                provider,
                new_state = newState,
                consecutive_failures = consecutiveFailures
            });
    }

    /// <summary>
    /// SHA-256 hashes a user ID to avoid storing PII in log files.
    /// Returns null when userId is null, or the first 16 hex chars of the hash.
    /// </summary>
    private static string? HashUserId(Guid? userId)
    {
        if (userId is null) return null;

        var bytes = Encoding.UTF8.GetBytes(userId.Value.ToString());
        var hash = SHA256.HashData(bytes);
        // Use first 16 hex chars (8 bytes = 64-bit prefix) — enough for log correlation
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        (_fileLogger as IDisposable)?.Dispose();
    }
}
