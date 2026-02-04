using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.Tests.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Moq;
using Pgvector.EntityFrameworkCore; // Issue #3547: Enable pgvector type mapping
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

#pragma warning disable S1144 // Unused private types or members should be removed (DTOs for deserialization)

namespace Api.Tests.E2E.Infrastructure;

/// <summary>
/// Base class for E2E (End-to-End) tests using WebApplicationFactory with Testcontainers.
/// Provides full API integration testing with real database and services.
///
/// Issue #3023: Backend E2E Test Suite
///
/// Architecture:
/// - Static WebApplicationFactory shared across ALL E2E tests (avoids TestServer disposal issues)
/// - Static containers and database shared for all tests
/// - Each test gets its own HttpClient and DbContext instance
/// - Tests designed to be independent (unique emails, GUIDs per test)
/// - Migrations run once at first test initialization
///
/// Features:
/// - Full API pipeline testing (HTTP → Endpoints → Handlers → Database)
/// - Real PostgreSQL and Redis via Testcontainers
/// - Cookie-based and API key authentication helpers
/// - Automatic test data with unique identifiers
///
/// Usage:
/// [Collection("E2ETests")]
/// public class AuthenticationE2ETests : E2ETestBase
/// {
///     public AuthenticationE2ETests(E2ETestFixture fixture) : base(fixture) { }
///
///     [Fact]
///     public async Task RegisterAndLogin_ValidCredentials_Succeeds()
///     {
///         var email = $"test_{Guid.NewGuid():N}@example.com"; // Unique per test
///         var response = await Client.PostAsJsonAsync("/api/v1/auth/register", new { email, ... });
///         response.EnsureSuccessStatusCode();
///     }
/// }
/// </summary>
public abstract class E2ETestBase : IAsyncLifetime
{
#pragma warning disable S4487 // Field is stored for potential future fixture access patterns
    private readonly E2ETestFixture _fixture;
#pragma warning restore S4487
    private HttpClient? _client;
    private MeepleAiDbContext? _dbContext;

    protected HttpClient Client => _client ?? throw new InvalidOperationException("Client not initialized");
    protected WebApplicationFactory<Program> Factory => E2ESharedInfrastructure.Factory;

    /// <summary>
    /// Per-test DbContext for test assertions and data seeding.
    /// Uses the same database as the API for consistency.
    /// Each test gets its own instance to avoid change tracker issues.
    /// </summary>
    protected MeepleAiDbContext DbContext => _dbContext ?? throw new InvalidOperationException("DbContext not initialized");

    /// <summary>
    /// Unique test identifier for debugging and logging.
    /// </summary>
    protected virtual string TestClassName => GetType().Name.ToLowerInvariant();

    protected E2ETestBase(E2ETestFixture fixture)
    {
        _fixture = fixture;
    }

    public virtual async ValueTask InitializeAsync()
    {
        // Ensure static infrastructure is initialized
        await E2ESharedInfrastructure.EnsureInitializedAsync();

        // Create HttpClient from shared factory
        _client = E2ESharedInfrastructure.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        // Create per-test DbContext (connected to shared database)
        _dbContext = E2ESharedInfrastructure.CreateDbContext();

        // Seed test data if needed by derived classes
        await SeedTestDataAsync();
    }

    public virtual async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        _client = null;

        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
            _dbContext = null;
        }
    }

    /// <summary>
    /// Override to seed test data before tests run.
    /// Called after initialization in InitializeAsync.
    /// Data persists in shared database between tests.
    /// </summary>
    protected virtual Task SeedTestDataAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Registers a test user and returns the session token.
    /// Handles both cookie-based and response-based session extraction.
    /// </summary>
    protected async Task<(string SessionToken, Guid UserId)> RegisterUserAsync(
        string email,
        string password,
        string? displayName = null)
    {
        var payload = new
        {
            email,
            password,
            displayName = displayName ?? email.Split('@')[0]
        };

        var response = await Client.PostAsJsonAsync("/api/v1/auth/register", payload);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<RegisterResponse>();

        // Extract session token from cookie
        var sessionToken = ExtractSessionCookie(response);

        return (sessionToken ?? throw new InvalidOperationException("Session token not found"), result!.User!.Id);
    }

    /// <summary>
    /// Logs in a test user and returns the session token.
    /// </summary>
    protected async Task<(string SessionToken, Guid UserId)> LoginUserAsync(string email, string password)
    {
        var payload = new { email, password };

        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", payload);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();

        // Check if 2FA is required
        if (result?.RequiresTwoFactor is true)
        {
            throw new InvalidOperationException("2FA required - use LoginUserWith2FAAsync instead");
        }

        var sessionToken = ExtractSessionCookie(response);

        return (sessionToken ?? throw new InvalidOperationException("Session token not found"), result!.User!.Id);
    }

    /// <summary>
    /// Sets the session cookie for authenticated requests.
    /// </summary>
    protected void SetSessionCookie(string sessionToken)
    {
        Client.DefaultRequestHeaders.Remove("Cookie");
        Client.DefaultRequestHeaders.Add("Cookie", $"meepleai_session={sessionToken}");
    }

    /// <summary>
    /// Clears authentication cookies from the client.
    /// </summary>
    protected void ClearAuthentication()
    {
        Client.DefaultRequestHeaders.Remove("Cookie");
        Client.DefaultRequestHeaders.Remove("Authorization");
    }

    /// <summary>
    /// Sets API key authentication header.
    /// </summary>
    protected void SetApiKeyAuthentication(string apiKey)
    {
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("ApiKey", apiKey);
    }

    /// <summary>
    /// Extracts session cookie value from HTTP response.
    /// </summary>
    private static string? ExtractSessionCookie(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            foreach (var cookie in cookies)
            {
                if (cookie.StartsWith("meepleai_session=", StringComparison.OrdinalIgnoreCase))
                {
                    var value = cookie.Split(';')[0];
                    return value.Substring("meepleai_session=".Length);
                }
            }
        }

        return null;
    }

    // Response DTOs for deserialization
    private sealed record RegisterResponse(UserDto? User, DateTime? ExpiresAt);
    private sealed record LoginResponse(UserDto? User, DateTime? ExpiresAt, bool? RequiresTwoFactor, string? SessionToken);
    private sealed record UserDto(Guid Id, string Email, string DisplayName, string Role);
}

/// <summary>
/// Static shared infrastructure for E2E tests.
/// This ensures the WebApplicationFactory and containers persist for the entire test run,
/// avoiding ObjectDisposedException issues with xUnit's collection fixture lifecycle.
///
/// The infrastructure is lazily initialized on first access and never explicitly disposed
/// (relies on process termination for cleanup, which is fine for test runs).
/// </summary>
internal static class E2ESharedInfrastructure
{
    private const string E2EDatabaseName = "e2e_shared_test_db";

    private static readonly SemaphoreSlim InitLock = new(1, 1);
    private static SharedTestcontainersFixture? _testcontainers;
    private static E2EWebApplicationFactory? _factory;
    private static string? _databaseConnectionString;
    private static string? _redisConnectionString;
    private static bool _initialized;

    /// <summary>
    /// Shared WebApplicationFactory for all E2E tests.
    /// </summary>
    public static WebApplicationFactory<Program> Factory =>
        _factory ?? throw new InvalidOperationException("E2E infrastructure not initialized. Call EnsureInitializedAsync first.");

    /// <summary>
    /// Connection string for the shared E2E database.
    /// </summary>
    public static string DatabaseConnectionString =>
        _databaseConnectionString ?? throw new InvalidOperationException("E2E infrastructure not initialized. Call EnsureInitializedAsync first.");

    /// <summary>
    /// Ensures the E2E infrastructure is initialized.
    /// Thread-safe and idempotent - can be called multiple times safely.
    /// </summary>
    public static async Task EnsureInitializedAsync()
    {
        if (_initialized)
        {
            return;
        }

        await InitLock.WaitAsync();
        try
        {
            if (_initialized)
            {
                return;
            }

            // Disable custom rate limiting middleware for E2E tests
            // The middleware checks these env vars directly (not from configuration)
            // Setting both DISABLE_RATE_LIMITING and ASPNETCORE_ENVIRONMENT for defense in depth
            Environment.SetEnvironmentVariable("DISABLE_RATE_LIMITING", "true");
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "CI");

            // Disable ASP.NET Core built-in rate limiting via environment variable
            // This must be set BEFORE the WebApplicationFactory is created since AddRateLimitingServices
            // reads this config during service registration in Program.cs
            // Environment variables use __ as hierarchy separator
            Environment.SetEnvironmentVariable("RateLimiting__Enabled", "false");

            // Initialize shared Testcontainers fixture (PostgreSQL and Redis)
            _testcontainers = new SharedTestcontainersFixture();
            await _testcontainers.InitializeAsync();

            // Create a dedicated database for E2E tests
            _databaseConnectionString = await _testcontainers.CreateIsolatedDatabaseAsync(E2EDatabaseName);
            _redisConnectionString = _testcontainers.RedisConnectionString;

            // Create shared WebApplicationFactory once for all tests using a custom class (not fluent builder)
            // This avoids the derived factory lifecycle issues that can cause ObjectDisposedException
            _factory = new E2EWebApplicationFactory(_databaseConnectionString, _redisConnectionString);

            // Force server creation during initialization to catch any startup errors early
            // This also ensures the server is created before any tests run
            try
            {
                var server = _factory.Server;
                Console.WriteLine($"[E2E] TestServer created successfully. BaseAddress: {server.BaseAddress}");

                // Verify the service provider is accessible
                using var scope = server.Services.CreateScope();
                Console.WriteLine("[E2E] Service provider is accessible");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[E2E] ERROR: Failed to create TestServer: {ex}");
                throw;
            }

            // Run migrations once using a temporary DbContext
            using var dbContext = CreateDbContext();
            await dbContext.Database.MigrateAsync();

            _initialized = true;
        }
        finally
        {
            InitLock.Release();
        }
    }

    /// <summary>
    /// Creates a new DbContext connected to the shared E2E database.
    /// Each test should call this to get its own DbContext instance.
    /// </summary>
    public static MeepleAiDbContext CreateDbContext()
    {
        if (_databaseConnectionString == null)
        {
            throw new InvalidOperationException("E2E infrastructure not initialized. Call EnsureInitializedAsync first.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<MeepleAiDbContext>();
        optionsBuilder.UseNpgsql(_databaseConnectionString, o => o.UseVector()); // Issue #3547: Enable pgvector type mapping
        optionsBuilder.ConfigureWarnings(warnings =>
            warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));

        var mockMediator = new Mock<IMediator>();
        var mockEventCollector = new Mock<IDomainEventCollector>();
        mockEventCollector
            .Setup(e => e.GetAndClearEvents())
            .Returns(new List<Api.SharedKernel.Domain.Interfaces.IDomainEvent>().AsReadOnly());

        return new MeepleAiDbContext(optionsBuilder.Options, mockMediator.Object, mockEventCollector.Object);
    }
}

/// <summary>
/// Custom WebApplicationFactory for E2E tests.
/// Using a concrete class instead of the fluent builder pattern avoids
/// derived factory lifecycle issues that can cause ObjectDisposedException.
///
/// IMPORTANT: This factory overrides Dispose to prevent premature disposal.
/// The TestServer should persist for the entire test run.
/// </summary>
internal sealed class E2EWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    private readonly string _redisConnectionString;

    public E2EWebApplicationFactory(string connectionString, string redisConnectionString)
    {
        _connectionString = connectionString;
        _redisConnectionString = redisConnectionString;
    }

    /// <summary>
    /// Override Dispose to prevent the TestServer from being disposed.
    /// The factory is static and should persist for the entire test run.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        // Intentionally do NOT call base.Dispose() to prevent TestServer disposal.
        // The TestServer will be cleaned up when the process terminates.
        // This is the fix for ObjectDisposedException in E2E tests.
        // Disposal state not tracked - intentional for static factory pattern
    }

    /// <summary>
    /// Override DisposeAsync to prevent the TestServer from being disposed.
    /// The factory is static and should persist for the entire test run.
    /// </summary>
    public override ValueTask DisposeAsync()
    {
        // Intentionally do NOT call base.DisposeAsync() to prevent TestServer disposal.
        // The TestServer will be cleaned up when the process terminates.
        // Disposal state not tracked - intentional for static factory pattern
        return ValueTask.CompletedTask;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Use CI environment to bypass rate limiting middleware
        // The middleware checks for both env.EnvironmentName == "CI" and ASPNETCORE_ENVIRONMENT == "CI"
        builder.UseEnvironment("CI");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Clear all configuration sources to ensure test config takes precedence
            config.Sources.Clear();

            var testConfig = new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _connectionString,
                ["ConnectionStrings:Redis"] = _redisConnectionString,
                ["Jwt:Secret"] = "test-secret-key-for-e2e-tests-minimum-32-characters-long",
                ["Jwt:Issuer"] = "MeepleAI-Test",
                ["Jwt:Audience"] = "MeepleAI-Test",
                ["OpenRouter:ApiKey"] = "test-key",
                ["OpenRouter:BaseUrl"] = "https://test.local",
                ["BoardGameGeek:Enabled"] = "false",
                ["Embedding:Enabled"] = "false",
                ["Embedding:Url"] = "http://localhost:8000",
                ["Redis:Enabled"] = "true",
                ["Redis:ConnectionString"] = _redisConnectionString,
                ["Qdrant:Enabled"] = "false",
                ["Qdrant:Host"] = "localhost",
                ["Qdrant:Port"] = "6333",
                ["Authentication:SessionManagement:SessionExpirationDays"] = "30",
                ["Admin:Email"] = "admin@test.local",
                ["Admin:Password"] = "TestAdmin123!",
                ["Admin:DisplayName"] = "Test Admin",
                // CI environment admin seeding configuration
                ["INITIAL_ADMIN_EMAIL"] = "admin@test.local",
                ["INITIAL_ADMIN_PASSWORD"] = "TestAdmin123!",
                ["INITIAL_ADMIN_DISPLAY_NAME"] = "Test Admin",
                ["Observability:Enabled"] = "false",
                ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "",
                // Disable rate limiting for E2E tests to prevent 429 errors
                ["RateLimiting:Enabled"] = "false"
            };

            config.AddInMemoryCollection(testConfig);
        });

        builder.ConfigureServices(services =>
        {
            // Remove all hosted services to prevent startup failures in test environment
            // Hosted services like CacheWarmingService, QualityReportService, etc. might fail
            // during startup and cause the host to dispose
            var hostedServiceDescriptors = services
                .Where(d => d.ServiceType == typeof(IHostedService))
                .ToList();
            foreach (var descriptor in hostedServiceDescriptors)
            {
                services.Remove(descriptor);
            }

            // Rate limiting is disabled via environment variable RateLimiting__Enabled=false
            // which is set in EnsureInitializedAsync() BEFORE the factory is created.
            // The AddRateLimitingServices method in Program.cs reads this and uses NoLimiter policies.
            // Additionally, the custom RateLimitingMiddleware checks DISABLE_RATE_LIMITING env var.

            // Remove any existing DbContext registrations first
            services.RemoveAll<DbContextOptions<MeepleAiDbContext>>();
            services.RemoveAll<MeepleAiDbContext>();
            services.RemoveAll<IDomainEventCollector>();

            // Register domain event collector (skipped in Testing environment by InfrastructureServiceExtensions)
            // Use AddScoped (not TryAddScoped) to ensure registration even if something partial existed
            services.AddScoped<IDomainEventCollector, DomainEventCollector>();

            services.AddDbContext<MeepleAiDbContext>((serviceProvider, options) =>
            {
                var configuration = serviceProvider.GetRequiredService<IConfiguration>();
                var connStr = configuration.GetConnectionString("DefaultConnection")
                    ?? throw new InvalidOperationException("DefaultConnection not configured");

                options.UseNpgsql(connStr, o => o.UseVector()); // Issue #3547: Enable pgvector type mapping
                options.ConfigureWarnings(warnings =>
                    warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
            });

            services.AddScoped<IDbContextMigrator, TestDbContextMigrator>();
        });
    }
}

/// <summary>
/// xUnit fixture for E2E tests.
/// This is a minimal fixture that defers to the static E2ESharedInfrastructure.
/// The fixture itself doesn't manage the lifecycle - it just provides the interface
/// for xUnit collection fixtures while the actual infrastructure is static.
/// </summary>
public sealed class E2ETestFixture : IAsyncLifetime
{
    public async ValueTask InitializeAsync()
    {
        // Initialize the static shared infrastructure
        await E2ESharedInfrastructure.EnsureInitializedAsync();
    }

    public ValueTask DisposeAsync()
    {
        // Do NOT dispose the static infrastructure here.
        // It will be cleaned up when the process terminates.
        // This is intentional to avoid ObjectDisposedException issues.
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Interface for database migration abstraction in tests.
/// </summary>
internal interface IDbContextMigrator
{
    Task MigrateAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Test implementation of database migrator.
/// </summary>
internal sealed class TestDbContextMigrator : IDbContextMigrator
{
    private readonly MeepleAiDbContext _context;

    public TestDbContextMigrator(MeepleAiDbContext context)
    {
        _context = context;
    }

    public Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        return _context.Database.MigrateAsync(cancellationToken);
    }
}

/// <summary>
/// Collection definition for E2E tests.
/// All E2E test classes should use this collection to share the test fixture.
/// DisableParallelization ensures test classes run sequentially to avoid
/// database conflicts and race conditions.
/// </summary>
[CollectionDefinition("E2ETests", DisableParallelization = true)]
public class E2ETestCollection : ICollectionFixture<E2ETestFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionFixture<>] and all the
    // ICollectionFixture<> interfaces.
}
