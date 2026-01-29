using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using StackExchange.Redis;
using Xunit;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;

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
    private IContainer? _unstructuredContainer;
    private IContainer? _smoldoclingContainer;
    private IContainer? _embeddingContainer;
    private IContainer? _rerankerContainer;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;
    private bool _pdfServicesEnabled;

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
    /// Unstructured PDF extraction service URL.
    /// Null if PDF services are not enabled.
    /// </summary>
    public string? UnstructuredServiceUrl { get; private set; }

    /// <summary>
    /// SmolDocling VLM PDF extraction service URL.
    /// Null if PDF services are not enabled.
    /// </summary>
    public string? SmolDoclingServiceUrl { get; private set; }

    /// <summary>
    /// Embedding service URL.
    /// Null if PDF services are not enabled.
    /// </summary>
    public string? EmbeddingServiceUrl { get; private set; }

    /// <summary>
    /// Reranker service URL.
    /// Null if PDF services are not enabled.
    /// </summary>
    public string? RerankerServiceUrl { get; private set; }

    /// <summary>
    /// Indicates whether PDF processing services are enabled.
    /// </summary>
    public bool ArePdfServicesEnabled => _pdfServicesEnabled;

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

            // Issue #2920: Validate configuration before starting containers
            var (isValid, warnings, errors) = TestcontainersConfiguration.Validate();
            if (!isValid)
            {
                throw new InvalidOperationException(
                    $"Testcontainers configuration validation failed:\n{string.Join("\n", errors)}");
            }
            foreach (var warning in warnings)
            {
                Console.WriteLine($"⚠️ Configuration warning: {warning}");
            }

            // Check if PDF services are enabled
            _pdfServicesEnabled = Environment.GetEnvironmentVariable(TestcontainersConfiguration.EnvEnablePdfServices)?.ToLowerInvariant() == "true";

            // Prefer external infrastructure if provided (faster in CI)
            var externalPostgres = Environment.GetEnvironmentVariable(TestcontainersConfiguration.EnvPostgresConnectionString);
            var externalRedis = Environment.GetEnvironmentVariable(TestcontainersConfiguration.EnvRedisConnectionString);

            // Issue #2920: Parallel container startup for faster initialization
            var postgresTask = string.IsNullOrWhiteSpace(externalPostgres)
                ? StartPostgresContainerAsync()
                : Task.FromResult(externalPostgres);

            var redisTask = string.IsNullOrWhiteSpace(externalRedis)
                ? StartRedisContainerAsync()
                : Task.FromResult(externalRedis);

            // Start PDF services if enabled
            Task<string?>? unstructuredTask = null;
            Task<string?>? smoldoclingTask = null;
            Task<string?>? embeddingTask = null;
            Task<string?>? rerankerTask = null;

            if (_pdfServicesEnabled)
            {
                Console.WriteLine("📄 PDF services enabled - starting Python service containers...");

                var externalUnstructured = Environment.GetEnvironmentVariable(TestcontainersConfiguration.EnvUnstructuredServiceUrl);
                var externalSmoldocling = Environment.GetEnvironmentVariable(TestcontainersConfiguration.EnvSmolDoclingServiceUrl);
                var externalEmbedding = Environment.GetEnvironmentVariable(TestcontainersConfiguration.EnvEmbeddingServiceUrl);
                var externalReranker = Environment.GetEnvironmentVariable(TestcontainersConfiguration.EnvRerankerServiceUrl);

                unstructuredTask = string.IsNullOrWhiteSpace(externalUnstructured)
                    ? StartUnstructuredServiceAsync()
                    : Task.FromResult<string?>(externalUnstructured);

                smoldoclingTask = string.IsNullOrWhiteSpace(externalSmoldocling)
                    ? StartSmolDoclingServiceAsync()
                    : Task.FromResult<string?>(externalSmoldocling);

                embeddingTask = string.IsNullOrWhiteSpace(externalEmbedding)
                    ? StartEmbeddingServiceAsync()
                    : Task.FromResult<string?>(externalEmbedding);

                rerankerTask = string.IsNullOrWhiteSpace(externalReranker)
                    ? StartRerankerServiceAsync()
                    : Task.FromResult<string?>(externalReranker);
            }

            // Wait for all containers to start in parallel
            var startTime = DateTime.UtcNow;
            var allTasks = new List<Task> { postgresTask, redisTask };
            if (unstructuredTask != null) allTasks.Add(unstructuredTask);
            if (smoldoclingTask != null) allTasks.Add(smoldoclingTask);
            if (embeddingTask != null) allTasks.Add(embeddingTask);
            if (rerankerTask != null) allTasks.Add(rerankerTask);

            await Task.WhenAll(allTasks);
            var duration = (DateTime.UtcNow - startTime).TotalSeconds;

            PostgresConnectionString = await postgresTask;
            RedisConnectionString = await redisTask;

            if (_pdfServicesEnabled && unstructuredTask != null && smoldoclingTask != null && embeddingTask != null && rerankerTask != null)
            {
                UnstructuredServiceUrl = await unstructuredTask;
                SmolDoclingServiceUrl = await smoldoclingTask;
                EmbeddingServiceUrl = await embeddingTask;
                RerankerServiceUrl = await rerankerTask;
                Console.WriteLine($"✅ All containers initialized in {duration:F2}s (parallel startup with PDF services)");
            }
            else
            {
                Console.WriteLine($"✅ Containers initialized in {duration:F2}s (parallel startup)");
            }

            // Issue #2920: Pre-warm connection pools with health check queries
            await PreWarmConnectionPoolsAsync();

            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// Starts PostgreSQL container with retry logic.
    /// Issue #2920: Extracted for parallel startup optimization.
    /// </summary>
    private async Task<string> StartPostgresContainerAsync()
    {
        for (int attempt = 0; attempt < TestcontainersConfiguration.ContainerStartupMaxRetries; attempt++)
        {
            try
            {
                // Start shared PostgreSQL container
                _postgresContainer = new ContainerBuilder()
                    .WithImage(TestcontainersConfiguration.PostgresImage)
                    .WithEnvironment("POSTGRES_USER", TestcontainersConfiguration.PostgresUsername)
                    .WithEnvironment("POSTGRES_PASSWORD", TestcontainersConfiguration.PostgresPassword)
                    .WithEnvironment("POSTGRES_DB", TestcontainersConfiguration.PostgresDefaultDatabase)
                    .WithPortBinding(5432, true)
                    .WithTmpfsMount("/var/lib/postgresql/data")
                    .WithCommand(
                        "-c", $"max_connections={TestcontainersConfiguration.PostgresMaxConnections}",
                        "-c", $"shared_buffers={TestcontainersConfiguration.PostgresSharedBuffers}")
                    .WithCleanUp(true)
                    .Build();

                await _postgresContainer.StartAsync();

                var postgresPort = _postgresContainer.GetMappedPublicPort(5432);
                var connectionString = TestcontainersConfiguration.BuildPostgresConnectionString(
                    "localhost", postgresPort, TestcontainersConfiguration.PostgresDefaultDatabase);

                // Wait for PostgreSQL to accept connections with retry
                await TestcontainersWaitHelpers.WaitForPostgresReadyAsync(connectionString);

                return connectionString;
            }
            catch (Exception ex) when (attempt < TestcontainersConfiguration.ContainerStartupMaxRetries - 1)
            {
                Console.WriteLine($"⚠️ PostgreSQL container startup attempt {attempt + 1}/{TestcontainersConfiguration.ContainerStartupMaxRetries} failed: {ex.Message}");

                // Cleanup failed container before retry
                if (_postgresContainer != null)
                {
                    try { await _postgresContainer.DisposeAsync(); }
                    catch { /* Ignore cleanup errors */ }
                    _postgresContainer = null;
                }

                await Task.Delay(TestcontainersConfiguration.ContainerStartupRetryDelays[attempt]);
            }
            catch (Exception ex) when (attempt == TestcontainersConfiguration.ContainerStartupMaxRetries - 1)
            {
                var diagnostics = $"PostgreSQL container failed to start after {TestcontainersConfiguration.ContainerStartupMaxRetries} attempts.\n" +
                                $"Last error: {ex.Message}\n" +
                                $"Container ID: {_postgresContainer?.Id ?? "null"}\n" +
                                $"Ensure Docker is running and ports are available.";
                throw new InvalidOperationException(diagnostics, ex);
            }
        }

        throw new InvalidOperationException("Unreachable code: PostgreSQL container startup failed");
    }

    /// <summary>
    /// Starts Redis container with retry logic.
    /// Issue #2920: Extracted for parallel startup optimization.
    /// </summary>
    private async Task<string> StartRedisContainerAsync()
    {
        for (int attempt = 0; attempt < TestcontainersConfiguration.ContainerStartupMaxRetries; attempt++)
        {
            try
            {
                // Start shared Redis container
                _redisContainer = new ContainerBuilder()
                    .WithImage(TestcontainersConfiguration.RedisImage)
                    .WithPortBinding(6379, true)
                    .WithTmpfsMount("/data")
                    .WithCleanUp(true)
                    .Build();

                await _redisContainer.StartAsync();

                var redisPort = _redisContainer.GetMappedPublicPort(6379);
                var connectionString = TestcontainersConfiguration.BuildRedisConnectionString("localhost", redisPort);

                // Wait for Redis to accept connections with retry
                await TestcontainersWaitHelpers.WaitForRedisReadyAsync(connectionString);

                return connectionString;
            }
            catch (Exception ex) when (attempt < TestcontainersConfiguration.ContainerStartupMaxRetries - 1)
            {
                Console.WriteLine($"⚠️ Redis container startup attempt {attempt + 1}/{TestcontainersConfiguration.ContainerStartupMaxRetries} failed: {ex.Message}");

                // Cleanup failed container before retry
                if (_redisContainer != null)
                {
                    try { await _redisContainer.DisposeAsync(); }
                    catch { /* Ignore cleanup errors */ }
                    _redisContainer = null;
                }

                await Task.Delay(TestcontainersConfiguration.ContainerStartupRetryDelays[attempt]);
            }
            catch (Exception ex) when (attempt == TestcontainersConfiguration.ContainerStartupMaxRetries - 1)
            {
                var diagnostics = $"Redis container failed to start after {TestcontainersConfiguration.ContainerStartupMaxRetries} attempts.\n" +
                                $"Last error: {ex.Message}\n" +
                                $"Container ID: {_redisContainer?.Id ?? "null"}\n" +
                                $"Ensure Docker is running and ports are available.";
                throw new InvalidOperationException(diagnostics, ex);
            }
        }

        throw new InvalidOperationException("Unreachable code: Redis container startup failed");
    }

    /// <summary>
    /// Starts Unstructured PDF extraction service container with retry logic.
    /// Only called when TEST_PDF_SERVICES=true environment variable is set.
    /// </summary>
    private async Task<string?> StartUnstructuredServiceAsync()
    {
        for (int attempt = 0; attempt < TestcontainersConfiguration.ContainerStartupMaxRetries; attempt++)
        {
            try
            {
                _unstructuredContainer = new ContainerBuilder()
                    .WithImage(TestcontainersConfiguration.UnstructuredImage)
                    .WithPortBinding(TestcontainersConfiguration.UnstructuredServicePort, true)
                    .WithWaitStrategy(Wait.ForUnixContainer()
                        .UntilHttpRequestIsSucceeded(r => r
                            .ForPath("/health")
                            .ForPort(TestcontainersConfiguration.UnstructuredServicePort)
                            .ForStatusCode(System.Net.HttpStatusCode.OK)))
                    .WithCleanUp(true)
                    .Build();

                await _unstructuredContainer.StartAsync();

                var port = _unstructuredContainer.GetMappedPublicPort(TestcontainersConfiguration.UnstructuredServicePort);
                var serviceUrl = $"http://localhost:{port}";

                Console.WriteLine($"✅ Unstructured service started at {serviceUrl}");
                return serviceUrl;
            }
            catch (Exception ex) when (attempt < TestcontainersConfiguration.ContainerStartupMaxRetries - 1)
            {
                Console.WriteLine($"⚠️ Unstructured service startup attempt {attempt + 1}/{TestcontainersConfiguration.ContainerStartupMaxRetries} failed: {ex.Message}");

                if (_unstructuredContainer != null)
                {
                    try { await _unstructuredContainer.DisposeAsync(); }
                    catch { /* Ignore cleanup errors */ }
                    _unstructuredContainer = null;
                }

                await Task.Delay(TestcontainersConfiguration.ContainerStartupRetryDelays[attempt]);
            }
            catch (Exception ex) when (attempt == TestcontainersConfiguration.ContainerStartupMaxRetries - 1)
            {
                Console.WriteLine($"❌ Unstructured service failed to start after {TestcontainersConfiguration.ContainerStartupMaxRetries} attempts: {ex.Message}");
                Console.WriteLine($"   Ensure Docker image '{TestcontainersConfiguration.UnstructuredImage}' is built: cd apps/unstructured-service && docker build -t {TestcontainersConfiguration.UnstructuredImage} .");
                return null; // Return null instead of throwing to allow tests to skip gracefully
            }
        }

        return null;
    }

    /// <summary>
    /// Starts SmolDocling VLM PDF extraction service container with retry logic.
    /// Only called when TEST_PDF_SERVICES=true environment variable is set.
    /// </summary>
    private async Task<string?> StartSmolDoclingServiceAsync()
    {
        for (int attempt = 0; attempt < TestcontainersConfiguration.ContainerStartupMaxRetries; attempt++)
        {
            try
            {
                _smoldoclingContainer = new ContainerBuilder()
                    .WithImage(TestcontainersConfiguration.SmolDoclingImage)
                    .WithPortBinding(TestcontainersConfiguration.SmolDoclingServicePort, true)
                    .WithWaitStrategy(Wait.ForUnixContainer()
                        .UntilHttpRequestIsSucceeded(r => r
                            .ForPath("/health")
                            .ForPort(TestcontainersConfiguration.SmolDoclingServicePort)
                            .ForStatusCode(System.Net.HttpStatusCode.OK)))
                    .WithCleanUp(true)
                    .Build();

                await _smoldoclingContainer.StartAsync();

                var port = _smoldoclingContainer.GetMappedPublicPort(TestcontainersConfiguration.SmolDoclingServicePort);
                var serviceUrl = $"http://localhost:{port}";

                Console.WriteLine($"✅ SmolDocling service started at {serviceUrl}");
                return serviceUrl;
            }
            catch (Exception ex) when (attempt < TestcontainersConfiguration.ContainerStartupMaxRetries - 1)
            {
                Console.WriteLine($"⚠️ SmolDocling service startup attempt {attempt + 1}/{TestcontainersConfiguration.ContainerStartupMaxRetries} failed: {ex.Message}");

                if (_smoldoclingContainer != null)
                {
                    try { await _smoldoclingContainer.DisposeAsync(); }
                    catch { /* Ignore cleanup errors */ }
                    _smoldoclingContainer = null;
                }

                await Task.Delay(TestcontainersConfiguration.ContainerStartupRetryDelays[attempt]);
            }
            catch (Exception ex) when (attempt == TestcontainersConfiguration.ContainerStartupMaxRetries - 1)
            {
                Console.WriteLine($"❌ SmolDocling service failed to start after {TestcontainersConfiguration.ContainerStartupMaxRetries} attempts: {ex.Message}");
                Console.WriteLine($"   Ensure Docker image '{TestcontainersConfiguration.SmolDoclingImage}' is built: cd apps/smoldocling-service && docker build -t {TestcontainersConfiguration.SmolDoclingImage} .");
                return null;
            }
        }

        return null;
    }

    /// <summary>
    /// Starts Embedding service container with retry logic.
    /// Only called when TEST_PDF_SERVICES=true environment variable is set.
    /// </summary>
    private async Task<string?> StartEmbeddingServiceAsync()
    {
        for (int attempt = 0; attempt < TestcontainersConfiguration.ContainerStartupMaxRetries; attempt++)
        {
            try
            {
                _embeddingContainer = new ContainerBuilder()
                    .WithImage(TestcontainersConfiguration.EmbeddingImage)
                    .WithPortBinding(TestcontainersConfiguration.EmbeddingServicePort, true)
                    .WithWaitStrategy(Wait.ForUnixContainer()
                        .UntilHttpRequestIsSucceeded(r => r
                            .ForPath("/health")
                            .ForPort(TestcontainersConfiguration.EmbeddingServicePort)
                            .ForStatusCode(System.Net.HttpStatusCode.OK)))
                    .WithCleanUp(true)
                    .Build();

                await _embeddingContainer.StartAsync();

                var port = _embeddingContainer.GetMappedPublicPort(TestcontainersConfiguration.EmbeddingServicePort);
                var serviceUrl = $"http://localhost:{port}";

                Console.WriteLine($"✅ Embedding service started at {serviceUrl}");
                return serviceUrl;
            }
            catch (Exception ex) when (attempt < TestcontainersConfiguration.ContainerStartupMaxRetries - 1)
            {
                Console.WriteLine($"⚠️ Embedding service startup attempt {attempt + 1}/{TestcontainersConfiguration.ContainerStartupMaxRetries} failed: {ex.Message}");

                if (_embeddingContainer != null)
                {
                    try { await _embeddingContainer.DisposeAsync(); }
                    catch { /* Ignore cleanup errors */ }
                    _embeddingContainer = null;
                }

                await Task.Delay(TestcontainersConfiguration.ContainerStartupRetryDelays[attempt]);
            }
            catch (Exception ex) when (attempt == TestcontainersConfiguration.ContainerStartupMaxRetries - 1)
            {
                Console.WriteLine($"❌ Embedding service failed to start after {TestcontainersConfiguration.ContainerStartupMaxRetries} attempts: {ex.Message}");
                Console.WriteLine($"   Ensure Docker image '{TestcontainersConfiguration.EmbeddingImage}' is built: cd apps/embedding-service && docker build -t {TestcontainersConfiguration.EmbeddingImage} .");
                return null;
            }
        }

        return null;
    }

    /// <summary>
    /// Starts Reranker service container with retry logic.
    /// Only called when TEST_PDF_SERVICES=true environment variable is set.
    /// </summary>
    private async Task<string?> StartRerankerServiceAsync()
    {
        for (int attempt = 0; attempt < TestcontainersConfiguration.ContainerStartupMaxRetries; attempt++)
        {
            try
            {
                _rerankerContainer = new ContainerBuilder()
                    .WithImage(TestcontainersConfiguration.RerankerImage)
                    .WithPortBinding(TestcontainersConfiguration.RerankerServicePort, true)
                    .WithWaitStrategy(Wait.ForUnixContainer()
                        .UntilHttpRequestIsSucceeded(r => r
                            .ForPath("/health")
                            .ForPort(TestcontainersConfiguration.RerankerServicePort)
                            .ForStatusCode(System.Net.HttpStatusCode.OK)))
                    .WithCleanUp(true)
                    .Build();

                await _rerankerContainer.StartAsync();

                var port = _rerankerContainer.GetMappedPublicPort(TestcontainersConfiguration.RerankerServicePort);
                var serviceUrl = $"http://localhost:{port}";

                Console.WriteLine($"✅ Reranker service started at {serviceUrl}");
                return serviceUrl;
            }
            catch (Exception ex) when (attempt < TestcontainersConfiguration.ContainerStartupMaxRetries - 1)
            {
                Console.WriteLine($"⚠️ Reranker service startup attempt {attempt + 1}/{TestcontainersConfiguration.ContainerStartupMaxRetries} failed: {ex.Message}");

                if (_rerankerContainer != null)
                {
                    try { await _rerankerContainer.DisposeAsync(); }
                    catch { /* Ignore cleanup errors */ }
                    _rerankerContainer = null;
                }

                await Task.Delay(TestcontainersConfiguration.ContainerStartupRetryDelays[attempt]);
            }
            catch (Exception ex) when (attempt == TestcontainersConfiguration.ContainerStartupMaxRetries - 1)
            {
                Console.WriteLine($"❌ Reranker service failed to start after {TestcontainersConfiguration.ContainerStartupMaxRetries} attempts: {ex.Message}");
                Console.WriteLine($"   Ensure Docker image '{TestcontainersConfiguration.RerankerImage}' is built: cd apps/reranker-service && docker build -t {TestcontainersConfiguration.RerankerImage} .");
                return null;
            }
        }

        return null;
    }

    /// <summary>
    /// Pre-warms connection pools with health check queries.
    /// Issue #2920: Reduces first-test latency by establishing initial connections.
    /// </summary>
    private async Task PreWarmConnectionPoolsAsync()
    {
        var warmupStart = DateTime.UtcNow;

        try
        {
            // PostgreSQL pool warmup
            await using var pgConnection = new NpgsqlConnection(PostgresConnectionString);
            await pgConnection.OpenAsync();
            await using var pgCommand = pgConnection.CreateCommand();
            pgCommand.CommandText = "SELECT 1;";
            await pgCommand.ExecuteScalarAsync();

            // Redis pool warmup
            var redis = await ConnectionMultiplexer.ConnectAsync(RedisConnectionString);
            var db = redis.GetDatabase();
            await db.PingAsync();
            await redis.CloseAsync();
            redis.Dispose();

            var warmupDuration = (DateTime.UtcNow - warmupStart).TotalMilliseconds;
            Console.WriteLine($"🔥 Connection pools pre-warmed in {warmupDuration:F0}ms");
        }
        catch (Exception ex)
        {
            // Non-fatal: warmup failure doesn't prevent tests from running
            Console.WriteLine($"⚠️ Connection pool warmup warning: {ex.Message}");
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

            // Dispose PDF service containers if they were started
            if (_unstructuredContainer != null)
            {
                await _unstructuredContainer.StopAsync();
                await _unstructuredContainer.DisposeAsync();
            }

            if (_smoldoclingContainer != null)
            {
                await _smoldoclingContainer.StopAsync();
                await _smoldoclingContainer.DisposeAsync();
            }

            if (_embeddingContainer != null)
            {
                await _embeddingContainer.StopAsync();
                await _embeddingContainer.DisposeAsync();
            }

            if (_rerankerContainer != null)
            {
                await _rerankerContainer.StopAsync();
                await _rerankerContainer.DisposeAsync();
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
    /// Issue #2706: Added retry logic to handle 57P01 connection termination errors in parallel tests.
    /// </summary>
    /// <param name="databaseName">Unique database name (e.g., "test_auth_{guid}")</param>
    /// <returns>Connection string for the isolated database</returns>
    public async Task<string> CreateIsolatedDatabaseAsync(string databaseName)
    {
        // Issue #2577: Add diagnostics for connection troubleshooting
        var startTime = DateTime.UtcNow;

        // Validate database name to prevent SQL injection (CA2100 suppression)
        if (!System.Text.RegularExpressions.Regex.IsMatch(databaseName, @"^[a-zA-Z0-9_]+$", System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromSeconds(1)))
        {
            throw new ArgumentException("Database name must contain only alphanumeric characters and underscores", nameof(databaseName));
        }

        // Issue #2706: Retry logic for handling 57P01 errors in parallel test execution
        // Issue #2920: Use centralized configuration
        for (int attempt = 0; attempt < TestcontainersConfiguration.DatabaseOperationMaxRetries; attempt++)
        {
            try
            {
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

                // Issue #2577: Log successful database creation with timing
                var duration = (DateTime.UtcNow - startTime).TotalSeconds;
                Console.WriteLine($"✅ Database '{databaseName}' created in {duration:F2}s");

                return builder.ConnectionString;
            }
            catch (NpgsqlException ex) when (ex.SqlState == "57P01" && attempt < TestcontainersConfiguration.DatabaseOperationMaxRetries - 1)
            {
                // Issue #2706: Handle 57P01 "terminating connection due to administrator command"
                // This happens when another test's cleanup terminates our connection during parallel execution
                Console.WriteLine($"⚠️ Database creation attempt {attempt + 1}/{TestcontainersConfiguration.DatabaseOperationMaxRetries} hit 57P01, retrying...");
                await Task.Delay(TestcontainersConfiguration.DatabaseOperationRetryDelays[attempt]);
            }
            catch (Exception ex)
            {
                // Issue #2577: Log connection failures with detailed context
                var duration = (DateTime.UtcNow - startTime).TotalSeconds;
                Console.WriteLine($"❌ Database creation failed after {duration:F2}s: {ex.Message}");
                Console.WriteLine($"   Connection string (sanitized): {new NpgsqlConnectionStringBuilder(PostgresConnectionString) { Password = "[REDACTED]" }}");
                throw;
            }
        }

        // Should never reach here, but compiler needs a return
        throw new InvalidOperationException($"Failed to create database '{databaseName}' after {TestcontainersConfiguration.DatabaseOperationMaxRetries} attempts");
    }

    /// <summary>
    /// Cleans up an isolated database after test class completion.
    /// Call this in your test class's DisposeAsync method.
    /// Issue #2706: Added retry logic to handle 57P01 connection termination errors in parallel tests.
    /// </summary>
    /// <param name="databaseName">Database name to drop</param>
    public async Task DropIsolatedDatabaseAsync(string databaseName)
    {
        // Issue #2577: Add diagnostics for cleanup troubleshooting
        var startTime = DateTime.UtcNow;

        // Validate database name to prevent SQL injection (CA2100 suppression)
        if (!System.Text.RegularExpressions.Regex.IsMatch(databaseName, @"^[a-zA-Z0-9_]+$", System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromSeconds(1)))
        {
            throw new ArgumentException("Database name must contain only alphanumeric characters and underscores", nameof(databaseName));
        }

        // Issue #2706: Retry logic for handling 57P01 errors in parallel test execution
        // Issue #2920: Use centralized configuration
        for (int attempt = 0; attempt < TestcontainersConfiguration.DatabaseOperationMaxRetries; attempt++)
        {
            try
            {
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
                var terminatedCount = await terminateCmd.ExecuteNonQueryAsync();

                // Issue #2706: Brief delay to allow terminated connections to fully close
                // This prevents race conditions where drop runs before connections are fully terminated
                // Issue #2920: Use centralized configuration
                if (terminatedCount > 0)
                {
                    await Task.Delay(TestcontainersConfiguration.DatabaseDropDelay);
                }

                // Drop database
                await using var dropCmd = connection.CreateCommand();
#pragma warning disable CA2100 // SQL injection safe: databaseName validated with regex ^[a-zA-Z0-9_]+$
                dropCmd.CommandText = $"DROP DATABASE IF EXISTS \"{databaseName}\";";
#pragma warning restore CA2100
                await dropCmd.ExecuteNonQueryAsync();

                // Issue #2577: Log successful cleanup with timing
                var duration = (DateTime.UtcNow - startTime).TotalSeconds;
                Console.WriteLine($"🧹 Database '{databaseName}' dropped in {duration:F2}s ({terminatedCount} connections terminated)");
                return; // Success, exit retry loop
            }
            catch (NpgsqlException ex) when (ex.SqlState == "57P01" && attempt < TestcontainersConfiguration.DatabaseOperationMaxRetries - 1)
            {
                // Issue #2706: Handle 57P01 "terminating connection due to administrator command"
                // This happens when another test's cleanup terminates our connection during parallel execution
                Console.WriteLine($"⚠️ Database cleanup attempt {attempt + 1}/{TestcontainersConfiguration.DatabaseOperationMaxRetries} hit 57P01, retrying...");
                await Task.Delay(TestcontainersConfiguration.DatabaseOperationRetryDelays[attempt]);
            }
            catch (Exception ex)
            {
                // Issue #2577: Log cleanup failures (non-fatal, suppress)
                var duration = (DateTime.UtcNow - startTime).TotalSeconds;
                Console.WriteLine($"⚠️ Database cleanup warning after {duration:F2}s: {ex.Message}");
                // Don't throw - cleanup failures are non-fatal
                return;
            }
        }
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

    /// <summary>
    /// Creates a mock MediatR instance for integration tests.
    /// Issue #2541: Centralized mock Mediator for shared fixture tests.
    /// Uses Mock to avoid MediatR licensing and configuration complexity.
    /// </summary>
    /// <returns>Mock IMediator instance</returns>
    public IMediator CreateMediator()
    {
        var mockMediator = new Moq.Mock<IMediator>();
        return mockMediator.Object;
    }

    /// <summary>
    /// Creates a configured DbContext for integration tests.
    /// Issue #2541: Centralized DbContext creation for shared fixture tests.
    /// </summary>
    /// <param name="connectionString">Database connection string</param>
    /// <param name="mediator">Optional mediator instance for domain events</param>
    /// <returns>Configured MeepleAiDbContext</returns>
    public MeepleAiDbContext CreateDbContext(string connectionString, IMediator? mediator = null)
    {
        var optionsBuilder = new DbContextOptionsBuilder<MeepleAiDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        // Use provided mediator or create a new one
        var contextMediator = mediator ?? CreateMediator();

        // Create mock event collector that returns empty list
        var mockEventCollector = new Moq.Mock<IDomainEventCollector>();
        mockEventCollector
            .Setup(e => e.GetAndClearEvents())
            .Returns(new List<Api.SharedKernel.Domain.Interfaces.IDomainEvent>().AsReadOnly());

        return new MeepleAiDbContext(optionsBuilder.Options, contextMediator, mockEventCollector.Object);
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