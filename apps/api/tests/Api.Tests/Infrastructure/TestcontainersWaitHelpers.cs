using Npgsql;
using StackExchange.Redis;
using Xunit.Abstractions;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Issue #2031: Shared helper methods for waiting on Testcontainers services.
/// Replaces Docker exec-based wait strategies that cause "cannot hijack" errors.
///
/// Why this exists:
/// - Docker Desktop on Windows has issues with container exec hijacking
/// - Testcontainers' UntilCommandIsCompleted() uses exec which fails intermittently
/// - TCP-based retry with exponential backoff is more reliable
///
/// Usage:
/// await TestcontainersWaitHelpers.WaitForPostgresReadyAsync(connectionString);
/// await TestcontainersWaitHelpers.WaitForRedisReadyAsync(connectionString);
/// </summary>
public static class TestcontainersWaitHelpers
{
    /// <summary>
    /// Wait for PostgreSQL to be fully ready with exponential backoff.
    /// </summary>
    /// <param name="connectionString">PostgreSQL connection string</param>
    /// <param name="output">Optional test output for logging retry attempts</param>
    /// <param name="maxRetries">Maximum number of retry attempts (default: 10)</param>
    /// <param name="initialDelayMs">Initial delay between retries in milliseconds (default: 500)</param>
    /// <exception cref="InvalidOperationException">Thrown when PostgreSQL is not ready after max retries</exception>
    public static async Task WaitForPostgresReadyAsync(
        string connectionString,
        ITestOutputHelper? output = null,
        int maxRetries = 10,
        int initialDelayMs = 500)
    {
        var delay = initialDelayMs;
        Exception? lastException = null;

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync();
                output?.WriteLine($"[Testcontainers] PostgreSQL ready after {i + 1} attempt(s)");
                return; // Connection successful
            }
            catch (NpgsqlException ex)
            {
                // Transient error - retry
                lastException = ex;
                output?.WriteLine($"[Testcontainers] PostgreSQL not ready (attempt {i + 1}/{maxRetries}): {ex.Message}");
                await Task.Delay(delay);
                delay = Math.Min(delay * 2, 5000); // Exponential backoff, max 5s
            }
            catch (Exception ex) when (ex is TimeoutException || ex is System.Net.Sockets.SocketException)
            {
                // Network-level transient error - retry
                lastException = ex;
                output?.WriteLine($"[Testcontainers] PostgreSQL connection failed (attempt {i + 1}/{maxRetries}): {ex.Message}");
                await Task.Delay(delay);
                delay = Math.Min(delay * 2, 5000);
            }
        }

        throw new InvalidOperationException(
            $"PostgreSQL not ready after {maxRetries} retries. Last error: {lastException?.Message}",
            lastException);
    }

    /// <summary>
    /// Wait for Redis to be fully ready with exponential backoff.
    /// </summary>
    /// <param name="connectionString">Redis connection string</param>
    /// <param name="output">Optional test output for logging retry attempts</param>
    /// <param name="maxRetries">Maximum number of retry attempts (default: 10)</param>
    /// <param name="initialDelayMs">Initial delay between retries in milliseconds (default: 500)</param>
    /// <exception cref="InvalidOperationException">Thrown when Redis is not ready after max retries</exception>
    public static async Task WaitForRedisReadyAsync(
        string connectionString,
        ITestOutputHelper? output = null,
        int maxRetries = 10,
        int initialDelayMs = 500)
    {
        var delay = initialDelayMs;
        Exception? lastException = null;

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                var redis = await ConnectionMultiplexer.ConnectAsync(connectionString);
                await redis.CloseAsync();
                redis.Dispose();
                output?.WriteLine($"[Testcontainers] Redis ready after {i + 1} attempt(s)");
                return; // Connection successful
            }
            catch (RedisConnectionException ex)
            {
                // Transient error - retry
                lastException = ex;
                output?.WriteLine($"[Testcontainers] Redis not ready (attempt {i + 1}/{maxRetries}): {ex.Message}");
                await Task.Delay(delay);
                delay = Math.Min(delay * 2, 5000); // Exponential backoff, max 5s
            }
            catch (Exception ex) when (ex is TimeoutException || ex is System.Net.Sockets.SocketException)
            {
                // Network-level transient error - retry
                lastException = ex;
                output?.WriteLine($"[Testcontainers] Redis connection failed (attempt {i + 1}/{maxRetries}): {ex.Message}");
                await Task.Delay(delay);
                delay = Math.Min(delay * 2, 5000);
            }
        }

        throw new InvalidOperationException(
            $"Redis not ready after {maxRetries} retries. Last error: {lastException?.Message}",
            lastException);
    }
}
