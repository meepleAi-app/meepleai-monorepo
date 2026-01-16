using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using StackExchange.Redis;
using Xunit;
using Api.Infrastructure;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Shared Testcontainer fixture for PostgreSQL and Redis.
/// Reduces test suite execution time by ~340s (34 test classes × ~10s startup overhead).
///
/// Implementation (Issue #1820):
/// - Single PostgreSQL container shared across all tests
/// - Single Redis container shared across all tests
/// - Proper cleanup between tests (database truncation, not recreation)
/// - Unique database per test class to prevent conflicts
///
/// Usage:
/// [Collection("SharedTestcontainers")]
/// public class YourIntegrationTests : IAsyncLifetime
/// {
///     private readonly SharedTestcontainersFixture _fixture;
///
///     public YourIntegrationTests(SharedTestcontainersFixture fixture)
///     {
///         _fixture = fixture;
///     }
/// }
/// </summary>
public sealed class SharedTestcontainersFixture : IAsyncLifetime
{
    private IContainer? _postgresContainer;
    private IContainer? _redisContainer;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    /// <summary>
    /// PostgreSQL connection string for shared container.
    /// Each test class should create its own database to avoid conflicts.
    /// </summary>
    public string PostgresConnectionString { get; private set; } = string.Empty;

    /// <summary>
    /// Redis connection string for shared container.
    /// Tests must use unique key prefixes (e.g., GUID) to avoid conflicts.
    /// </summary>
    public string RedisConnectionString { get; private set; } = string.Empty;

    /// <summary>
    /// PostgreSQL container for direct access if needed.
    /// </summary>
    public IContainer? PostgresContainer => _postgresContainer;

    /// <summary>
    /// Redis container for direct access if needed.
    /// </summary>
    public IContainer? RedisContainer => _redisContainer;

    public async ValueTask InitializeAsync()
    {
        await _initLock.WaitAsync();
        try
        {
            if (_initialized)
            {
                return; // Already initialized
            }

            // Prefer external infrastructure if provided (faster in CI)
            var externalPostgres = Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNSTRING");
            var externalRedis = Environment.GetEnvironmentVariable("TEST_REDIS_CONNSTRING");

            if (!string.IsNullOrWhiteSpace(externalPostgres))
            {
                PostgresConnectionString = externalPostgres;
            }
            else
            {
                // Issue #2474: Retry logic for container startup (handles port conflicts and transient failures)
                const int maxRetries = 3;
                var retryDelays = new[] { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(8) };

                for (int attempt = 0; attempt < maxRetries; attempt++)
                {
                    try
                    {
                        // Start shared PostgreSQL container
                        _postgresContainer = new ContainerBuilder()
                            .WithImage("postgres:16-alpine")
                            .WithEnvironment("POSTGRES_USER", "postgres")
                            .WithEnvironment("POSTGRES_PASSWORD", "postgres")
                            .WithEnvironment("POSTGRES_DB", "test_shared")
                            .WithPortBinding(5432, true)
                            // Issue #2031: Removed .UntilCommandIsCompleted("pg_isready") to prevent Docker hijack errors
                            // Default TCP port check + retry mechanism is more reliable than hijacked command execution
                            .WithCleanUp(true)
                            .Build();

                        await _postgresContainer.StartAsync();

                        var postgresPort = _postgresContainer.GetMappedPublicPort(5432);
                        PostgresConnectionString = $"Host=localhost;Port={postgresPort};Database=test_shared;Username=postgres;Password=postgres;Ssl Mode=Disable;Trust Server Certificate=true;KeepAlive=30;Pooling=false;Connection Timeout=10;";

                        // Issue #2031: Wait for PostgreSQL to accept connections with retry
                        // Issue #2474: Increased timeout from 5s to 10s for better stability
                        await TestcontainersWaitHelpers.WaitForPostgresReadyAsync(PostgresConnectionString);

                        break; // Success, exit retry loop
                    }
                    catch (Exception ex) when (attempt < maxRetries - 1)
                    {
                        // Log diagnostic information for troubleshooting
                        Console.WriteLine($"⚠️ PostgreSQL container startup attempt {attempt + 1}/{maxRetries} failed: {ex.Message}");

                        // Cleanup failed container before retry
                        if (_postgresContainer != null)
                        {
                            try
                            {
                                await _postgresContainer.DisposeAsync();
                            }
                            catch
                            {
                                // Ignore cleanup errors
                            }
                            _postgresContainer = null;
                        }

                        // Wait before retry with exponential backoff
                        await Task.Delay(retryDelays[attempt]);
                    }
                    catch (Exception ex) when (attempt == maxRetries - 1)
                    {
                        // Final attempt failed, provide detailed diagnostics
                        var diagnostics = $"PostgreSQL container failed to start after {maxRetries} attempts.\n" +
                                        $"Last error: {ex.Message}\n" +
                                        $"Container ID: {_postgresContainer?.Id ?? "null"}\n" +
                                        $"Ensure Docker is running and ports are available.";
                        throw new InvalidOperationException(diagnostics, ex);
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(externalRedis))
            {
                RedisConnectionString = externalRedis;
            }
            else
            {
                // Issue #2474: Retry logic for Redis container startup
                const int maxRetries = 3;
                var retryDelays = new[] { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(8) };

                for (int attempt = 0; attempt < maxRetries; attempt++)
                {
                    try
                    {
                        // Start shared Redis container
                        _redisContainer = new ContainerBuilder()
                            .WithImage("redis:7-alpine")
                            .WithPortBinding(6379, true)
                            // Issue #2031: Removed .UntilCommandIsCompleted("redis-cli", "ping") to prevent Docker hijack errors
                            // Default TCP port check + retry mechanism is more reliable than hijacked command execution
                            .WithCleanUp(true)
                            .Build();

                        await _redisContainer.StartAsync();

                        var redisPort = _redisContainer.GetMappedPublicPort(6379);
                        // Issue #2474: Increased connectTimeout from 5s to 10s for better stability
                        RedisConnectionString = $"localhost:{redisPort},abortConnect=false,connectTimeout=10000,syncTimeout=10000,connectRetry=3";

                        // Issue #2031: Wait for Redis to accept connections with retry
                        await TestcontainersWaitHelpers.WaitForRedisReadyAsync(RedisConnectionString);

                        break; // Success, exit retry loop
                    }
                    catch (Exception ex) when (attempt < maxRetries - 1)
                    {
                        // Log diagnostic information for troubleshooting
                        Console.WriteLine($"⚠️ Redis container startup attempt {attempt + 1}/{maxRetries} failed: {ex.Message}");

                        // Cleanup failed container before retry
                        if (_redisContainer != null)
                        {
                            try
                            {
                                await _redisContainer.DisposeAsync();
                            }
                            catch
                            {
                                // Ignore cleanup errors
                            }
                            _redisContainer = null;
                        }

                        // Wait before retry with exponential backoff
                        await Task.Delay(retryDelays[attempt]);
                    }
                    catch (Exception ex) when (attempt == maxRetries - 1)
                    {
                        // Final attempt failed, provide detailed diagnostics
                        var diagnostics = $"Redis container failed to start after {maxRetries} attempts.\n" +
                                        $"Last error: {ex.Message}\n" +
                                        $"Container ID: {_redisContainer?.Id ?? "null"}\n" +
                                        $"Ensure Docker is running and ports are available.";
                        throw new InvalidOperationException(diagnostics, ex);
                    }
                }
            }

            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _initLock.WaitAsync();
        try
        {
            if (_postgresContainer != null)
            {
                await _postgresContainer.StopAsync();
                await _postgresContainer.DisposeAsync();
            }

            if (_redisContainer != null)
            {
                await _redisContainer.StopAsync();
                await _redisContainer.DisposeAsync();
            }
        }
        finally
        {
            // Issue #2031: Release lock before disposing to avoid ObjectDisposedException
            _initLock.Release();
        }

        // Issue #2031: Dispose lock after try-finally to avoid accessing disposed object in finally
        _initLock.Dispose();
    }

    /// <summary>
    /// Creates a new isolated database for a test class.
    /// Call this in your test class's InitializeAsync method.
    /// </summary>
    /// <param name="databaseName">Unique database name (e.g., "test_auth_{guid}")</param>
    /// <returns>Connection string for the isolated database</returns>
    public async Task<string> CreateIsolatedDatabaseAsync(string databaseName)
    {
        // Validate database name to prevent SQL injection (CA2100 suppression)
        if (!System.Text.RegularExpressions.Regex.IsMatch(databaseName, @"^[a-zA-Z0-9_]+$", System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromSeconds(1)))
        {
            throw new ArgumentException("Database name must contain only alphanumeric characters and underscores", nameof(databaseName));
        }

        // Create database
        var builder = new NpgsqlConnectionStringBuilder(PostgresConnectionString)
        {
            Database = "postgres" // Connect to default db to create new one
        };

        await using var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
#pragma warning disable CA2100 // SQL injection safe: databaseName validated with regex ^[a-zA-Z0-9_]+$
        cmd.CommandText = $"DROP DATABASE IF EXISTS \"{databaseName}\"; CREATE DATABASE \"{databaseName}\";";
#pragma warning restore CA2100
        await cmd.ExecuteNonQueryAsync();

        // Return connection string for new database
        builder.Database = databaseName;
        return builder.ConnectionString;
    }

    /// <summary>
    /// Cleans up an isolated database after test class completion.
    /// Call this in your test class's DisposeAsync method.
    /// </summary>
    /// <param name="databaseName">Database name to drop</param>
    public async Task DropIsolatedDatabaseAsync(string databaseName)
    {
        // Validate database name to prevent SQL injection (CA2100 suppression)
        if (!System.Text.RegularExpressions.Regex.IsMatch(databaseName, @"^[a-zA-Z0-9_]+$", System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromSeconds(1)))
        {
            throw new ArgumentException("Database name must contain only alphanumeric characters and underscores", nameof(databaseName));
        }

        var builder = new NpgsqlConnectionStringBuilder(PostgresConnectionString)
        {
            Database = "postgres"
        };

        await using var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync();

        // Terminate active connections first
        await using var terminateCmd = connection.CreateCommand();
#pragma warning disable CA2100 // SQL injection safe: databaseName validated with regex ^[a-zA-Z0-9_]+$
        terminateCmd.CommandText = $@"
            SELECT pg_terminate_backend(pid)
            FROM pg_stat_activity
            WHERE datname = '{databaseName}' AND pid <> pg_backend_pid();";
#pragma warning restore CA2100
        await terminateCmd.ExecuteNonQueryAsync();

        // Drop database
        await using var dropCmd = connection.CreateCommand();
#pragma warning disable CA2100 // SQL injection safe: databaseName validated with regex ^[a-zA-Z0-9_]+$
        dropCmd.CommandText = $"DROP DATABASE IF EXISTS \"{databaseName}\";";
#pragma warning restore CA2100
        await dropCmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Flushes all Redis keys with the specified prefix.
    /// Use this for test cleanup with unique key prefixes.
    /// </summary>
    /// <param name="keyPrefix">Redis key prefix to flush (e.g., "test:abc123:*")</param>
    public async Task FlushRedisByPrefixAsync(string keyPrefix)
    {
        var redis = await ConnectionMultiplexer.ConnectAsync(RedisConnectionString);
        var db = redis.GetDatabase();

        var server = redis.GetServer(redis.GetEndPoints()[0]);
        var keys = server.Keys(pattern: keyPrefix).ToArray();

        if (keys.Length > 0)
        {
            await db.KeyDeleteAsync(keys);
        }

        await redis.CloseAsync();
        redis.Dispose();
    }
}

/// <summary>
/// Collection definition for shared Testcontainers.
/// All integration tests should use this collection to share container instances.
/// </summary>
[CollectionDefinition("SharedTestcontainers")]
public class SharedTestcontainersCollectionDefinition : ICollectionFixture<SharedTestcontainersFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionFixture<>] and all the
    // ICollectionFixture<> interfaces.
}
