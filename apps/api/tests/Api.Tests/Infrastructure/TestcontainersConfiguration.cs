using System;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Centralized configuration for Testcontainers infrastructure.
/// Issue #2920: Consolidates all container settings for maintainability and optimization.
///
/// Performance Targets:
/// - Container startup: &lt;20s for both PostgreSQL + Redis
/// - Database creation: &lt;1s per isolated database
/// - Connection pool warmup: &lt;500ms per database
/// - Full test suite: &lt;3 min (90% improvement vs sequential)
/// </summary>
public static class TestcontainersConfiguration
{
    #region Container Images

    /// <summary>
    /// PostgreSQL container image.
    /// Alpine variant for smaller size and faster pull.
    /// </summary>
    public const string PostgresImage = "postgres:16-alpine";

    /// <summary>
    /// Redis container image.
    /// Alpine variant for smaller size and faster pull.
    /// </summary>
    public const string RedisImage = "redis:7-alpine";

    #endregion

    #region PostgreSQL Configuration

    /// <summary>
    /// Maximum connections for PostgreSQL server.
    /// Issue #2693: Increased from 100 (default) to 500 for parallel CI tests.
    /// Calculation: 94 test classes × 5 max pool size = 470 connections &lt; 500 limit.
    /// </summary>
    public const int PostgresMaxConnections = 500;

    /// <summary>
    /// Shared buffers for PostgreSQL server.
    /// Issue #2576: 256MB optimal for test workload with 4GB memory limit.
    /// </summary>
    public const string PostgresSharedBuffers = "256MB";

    /// <summary>
    /// Default PostgreSQL database name for shared container.
    /// </summary>
    public const string PostgresDefaultDatabase = "test_shared";

    /// <summary>
    /// Default PostgreSQL username.
    /// </summary>
    public const string PostgresUsername = "postgres";

    /// <summary>
    /// Default PostgreSQL password.
    /// Note: Secure for local/CI test environments only.
    /// </summary>
    public const string PostgresPassword = "postgres";

    #endregion

    #region Connection Pooling Configuration

    /// <summary>
    /// Minimum pool size per test database.
    /// Issue #2902: Conservative setting (1) for 94+ test classes.
    /// Baseline connections: 94 classes × 1 = 94 connections.
    /// </summary>
    public const int ConnectionPoolMinSize = 1;

    /// <summary>
    /// Maximum pool size per test database.
    /// Issue #2902: Reduced from 50 to 5 to prevent connection exhaustion.
    /// Maximum connections: 94 classes × 5 = 470 &lt; 500 server limit.
    /// </summary>
    public const int ConnectionPoolMaxSize = 5;

    /// <summary>
    /// Connection establishment timeout in seconds.
    /// Issue #2577: Increased from 10s to 30s for CI environment stability.
    /// </summary>
    public const int ConnectionTimeoutSeconds = 30;

    /// <summary>
    /// Command execution timeout in seconds.
    /// Issue #2577: 60s to handle long-running test operations.
    /// </summary>
    public const int CommandTimeoutSeconds = 60;

    /// <summary>
    /// Keep-alive interval in seconds.
    /// Issue #2902: Reduced from 30s to 10s to prevent idle connection closure.
    /// </summary>
    public const int ConnectionKeepAliveSeconds = 10;

    /// <summary>
    /// Connection idle lifetime in seconds.
    /// Issue #2902: 30s for faster recycling in large test suites.
    /// </summary>
    public const int ConnectionIdleLifetimeSeconds = 30;

    /// <summary>
    /// Connection pruning interval in seconds.
    /// Issue #2902: 5s for aggressive cleanup in parallel execution.
    /// </summary>
    public const int ConnectionPruningIntervalSeconds = 5;

    #endregion

    #region Redis Configuration

    /// <summary>
    /// Redis connection timeout in milliseconds.
    /// Issue #2474: Increased from 5s to 10s for CI stability.
    /// </summary>
    public const int RedisConnectTimeoutMs = 10000;

    /// <summary>
    /// Redis sync operation timeout in milliseconds.
    /// Issue #2474: 10s to handle slow synchronous operations.
    /// </summary>
    public const int RedisSyncTimeoutMs = 10000;

    /// <summary>
    /// Redis connection retry attempts.
    /// </summary>
    public const int RedisConnectRetry = 3;

    #endregion

    #region Retry Configuration

    /// <summary>
    /// Maximum retry attempts for container startup.
    /// Issue #2474: 3 attempts with exponential backoff.
    /// </summary>
    public const int ContainerStartupMaxRetries = 3;

    /// <summary>
    /// Retry delays for container startup (exponential backoff).
    /// Issue #2474: 2s, 4s, 8s progression.
    /// </summary>
    public static readonly TimeSpan[] ContainerStartupRetryDelays = new[]
    {
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(4),
        TimeSpan.FromSeconds(8)
    };

    /// <summary>
    /// Maximum retry attempts for database operations.
    /// Issue #2706: 3 attempts for handling 57P01 connection termination errors.
    /// </summary>
    public const int DatabaseOperationMaxRetries = 3;

    /// <summary>
    /// Retry delays for database operations (exponential backoff).
    /// Issue #2706: 100ms, 500ms, 1s progression.
    /// </summary>
    public static readonly TimeSpan[] DatabaseOperationRetryDelays = new[]
    {
        TimeSpan.FromMilliseconds(100),
        TimeSpan.FromMilliseconds(500),
        TimeSpan.FromSeconds(1)
    };

    /// <summary>
    /// Maximum retry attempts for connection readiness checks.
    /// Issue #2031: 10 attempts with exponential backoff.
    /// </summary>
    public const int ConnectionReadinessMaxRetries = 10;

    /// <summary>
    /// Initial delay for connection readiness retries.
    /// Issue #2031: 500ms starting point, grows to 5s max.
    /// </summary>
    public static readonly TimeSpan ConnectionReadinessInitialDelay = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Maximum delay for connection readiness retries.
    /// Issue #2031: 5s cap on exponential backoff.
    /// </summary>
    public static readonly TimeSpan ConnectionReadinessMaxDelay = TimeSpan.FromSeconds(5);

    #endregion

    #region Wait Strategy Configuration

    /// <summary>
    /// Delay after terminating connections before dropping database.
    /// Issue #2706: 50ms to allow terminated connections to fully close.
    /// </summary>
    public static readonly TimeSpan DatabaseDropDelay = TimeSpan.FromMilliseconds(50);

    #endregion

    #region Environment Variables

    /// <summary>
    /// Environment variable for external PostgreSQL connection string.
    /// Set this to use external PostgreSQL instead of Testcontainer (faster in CI).
    /// </summary>
    public const string EnvPostgresConnectionString = "TEST_POSTGRES_CONNSTRING";

    /// <summary>
    /// Environment variable for external Redis connection string.
    /// Set this to use external Redis instead of Testcontainer (faster in CI).
    /// </summary>
    public const string EnvRedisConnectionString = "TEST_REDIS_CONNSTRING";

    #endregion

    #region Validation

    /// <summary>
    /// Validates Testcontainers configuration.
    /// Issue #2920: Ensures settings are within safe operational limits.
    /// </summary>
    /// <returns>Validation result with any warnings or errors</returns>
    public static (bool IsValid, string[] Warnings, string[] Errors) Validate()
    {
        var warnings = new List<string>();
        var errors = new List<string>();

        // Validate connection pool settings
        if (ConnectionPoolMaxSize * 100 > PostgresMaxConnections) // Assuming ~100 test classes max
        {
            warnings.Add($"Connection pool may exceed server limit: {ConnectionPoolMaxSize} × 100 classes = {ConnectionPoolMaxSize * 100} > {PostgresMaxConnections} max_connections");
        }

        if (ConnectionPoolMinSize > ConnectionPoolMaxSize)
        {
            errors.Add($"MinPoolSize ({ConnectionPoolMinSize}) cannot exceed MaxPoolSize ({ConnectionPoolMaxSize})");
        }

        // Validate timeout settings
        if (ConnectionTimeoutSeconds < 10)
        {
            warnings.Add($"ConnectionTimeout ({ConnectionTimeoutSeconds}s) may be too low for CI environments");
        }

        if (CommandTimeoutSeconds < ConnectionTimeoutSeconds)
        {
            warnings.Add($"CommandTimeout ({CommandTimeoutSeconds}s) should be >= ConnectionTimeout ({ConnectionTimeoutSeconds}s)");
        }

        // Validate retry settings
        if (ContainerStartupMaxRetries < 1)
        {
            errors.Add($"ContainerStartupMaxRetries ({ContainerStartupMaxRetries}) must be >= 1");
        }

        if (DatabaseOperationMaxRetries < 1)
        {
            errors.Add($"DatabaseOperationMaxRetries ({DatabaseOperationMaxRetries}) must be >= 1");
        }

        return (errors.Count == 0, warnings.ToArray(), errors.ToArray());
    }

    /// <summary>
    /// Builds optimized PostgreSQL connection string.
    /// Issue #2920: Centralizes connection string construction with all optimizations.
    /// </summary>
    public static string BuildPostgresConnectionString(string host, int port, string database)
    {
        return $"Host={host};Port={port};Database={database};" +
               $"Username={PostgresUsername};Password={PostgresPassword};" +
               $"Ssl Mode=Disable;Trust Server Certificate=true;" +
               $"KeepAlive={ConnectionKeepAliveSeconds};Pooling=true;" +
               $"MinPoolSize={ConnectionPoolMinSize};MaxPoolSize={ConnectionPoolMaxSize};" +
               $"Timeout={ConnectionTimeoutSeconds};CommandTimeout={CommandTimeoutSeconds};" +
               $"ConnectionIdleLifetime={ConnectionIdleLifetimeSeconds};" +
               $"ConnectionPruningInterval={ConnectionPruningIntervalSeconds};";
    }

    /// <summary>
    /// Builds optimized Redis connection string.
    /// Issue #2920: Centralizes Redis connection string construction.
    /// </summary>
    public static string BuildRedisConnectionString(string host, int port)
    {
        return $"{host}:{port}," +
               $"abortConnect=false," +
               $"connectTimeout={RedisConnectTimeoutMs}," +
               $"syncTimeout={RedisSyncTimeoutMs}," +
               $"connectRetry={RedisConnectRetry}";
    }

    #endregion
}
