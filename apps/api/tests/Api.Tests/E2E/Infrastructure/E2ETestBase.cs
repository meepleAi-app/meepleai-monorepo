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
using Moq;
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
/// Features:
/// - Full API pipeline testing (HTTP → Endpoints → Handlers → Database)
/// - Real PostgreSQL and Redis via Testcontainers
/// - Isolated database per test class
/// - Cookie-based and API key authentication helpers
/// - Automatic cleanup between tests
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
///         var response = await Client.PostAsJsonAsync("/api/v1/auth/register", new { ... });
///         response.EnsureSuccessStatusCode();
///     }
/// }
/// </summary>
public abstract class E2ETestBase : IAsyncLifetime
{
    private readonly E2ETestFixture _fixture;
    private MeepleAiDbContext? _dbContext;
    private string? _databaseName;
    private WebApplicationFactory<Program>? _factory;

    protected HttpClient Client { get; private set; } = null!;
    protected WebApplicationFactory<Program> Factory => _factory ?? throw new InvalidOperationException("Factory not initialized");
    protected MeepleAiDbContext DbContext => _dbContext ?? throw new InvalidOperationException("DbContext not initialized");

    /// <summary>
    /// Unique test class identifier for database isolation.
    /// Override to provide a custom database name.
    /// </summary>
    protected virtual string TestClassName => GetType().Name.ToLowerInvariant();

    protected E2ETestBase(E2ETestFixture fixture)
    {
        _fixture = fixture;
    }

    public virtual async ValueTask InitializeAsync()
    {
        // Create isolated database for this test class
        var fullDbName = $"e2e_{TestClassName}_{Guid.NewGuid():N}";
        _databaseName = fullDbName.Length > 63 ? fullDbName.Substring(0, 63) : fullDbName;
        var connectionString = await _fixture.Fixture.CreateIsolatedDatabaseAsync(_databaseName);

        // Create factory specific to this test class (not shared)
        _factory = CreateTestFactory(connectionString, _fixture.Fixture.RedisConnectionString);

        // Create HttpClient from factory
        Client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        // Create DbContext for test assertions
        var optionsBuilder = new DbContextOptionsBuilder<MeepleAiDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        optionsBuilder.ConfigureWarnings(warnings =>
            warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));

        var mockMediator = new Mock<IMediator>();
        var mockEventCollector = new Mock<IDomainEventCollector>();
        mockEventCollector
            .Setup(e => e.GetAndClearEvents())
            .Returns(new List<Api.SharedKernel.Domain.Interfaces.IDomainEvent>().AsReadOnly());

        _dbContext = new MeepleAiDbContext(optionsBuilder.Options, mockMediator.Object, mockEventCollector.Object);

        // Run migrations
        await _dbContext.Database.MigrateAsync();

        // Seed default test data if needed
        await SeedTestDataAsync();
    }

    public virtual async ValueTask DisposeAsync()
    {
        Client?.Dispose();
        
        if (_factory != null)
        {
            await _factory.DisposeAsync();
        }

        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }

        if (_databaseName != null)
        {
            await _fixture.Fixture.DropIsolatedDatabaseAsync(_databaseName);
        }
    }

    /// <summary>
    /// Creates a WebApplicationFactory configured for this specific test class.
    /// Each test class gets its own factory to avoid shared state issues.
    /// </summary>
    private static WebApplicationFactory<Program> CreateTestFactory(string postgresConnectionString, string redisConnectionString)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");

                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.Sources.Clear();

                    var testConfig = new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] = postgresConnectionString,
                        ["ConnectionStrings:Redis"] = redisConnectionString,
                        ["Jwt:Secret"] = "test-secret-key-for-e2e-tests-minimum-32-characters-long",
                        ["Jwt:Issuer"] = "MeepleAI-Test",
                        ["Jwt:Audience"] = "MeepleAI-Test",
                        ["OpenRouter:ApiKey"] = "test-key",
                        ["OpenRouter:BaseUrl"] = "https://test.local",
                        ["BoardGameGeek:Enabled"] = "false",
                        ["Embedding:Enabled"] = "false",
                        ["Embedding:Url"] = "http://localhost:8000",
                        ["Redis:Enabled"] = "true",
                        ["Redis:ConnectionString"] = redisConnectionString,
                        ["Qdrant:Enabled"] = "false",
                        ["Qdrant:Host"] = "localhost",
                        ["Qdrant:Port"] = "6333",
                        ["Authentication:SessionManagement:SessionExpirationDays"] = "30",
                        ["Admin:Email"] = "admin@test.local",
                        ["Admin:Password"] = "TestAdmin123!",
                        ["Admin:DisplayName"] = "Test Admin",
                        ["Observability:Enabled"] = "false",
                        ["OTEL_EXPORTER_OTLP_ENDPOINT"] = ""
                    };

                    config.AddInMemoryCollection(testConfig);
                });

                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<DbContextOptions<MeepleAiDbContext>>();
                    services.RemoveAll<MeepleAiDbContext>();

                    // Register domain event collector (skipped in Testing environment by InfrastructureServiceExtensions)
                    services.TryAddScoped<IDomainEventCollector, DomainEventCollector>();

                    services.AddDbContext<MeepleAiDbContext>((serviceProvider, options) =>
                    {
                        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
                        var connStr = configuration.GetConnectionString("DefaultConnection")
                            ?? throw new InvalidOperationException("DefaultConnection not configured");

                        options.UseNpgsql(connStr);
                        options.ConfigureWarnings(warnings =>
                            warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
                    });

                    services.AddScoped<IDbContextMigrator, TestDbContextMigrator>();
                });
            });
    }

    /// <summary>
    /// Override to seed test data before tests run.
    /// Called after database migration in InitializeAsync.
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
/// xUnit fixture for E2E tests that manages Testcontainers.
/// Shared across all E2E test classes via collection fixture.
/// Each test class creates its own WebApplicationFactory for isolation.
/// </summary>
public sealed class E2ETestFixture : IAsyncLifetime
{
    public SharedTestcontainersFixture Fixture { get; } = new SharedTestcontainersFixture();

    public async ValueTask InitializeAsync()
    {
        // Initialize shared Testcontainers fixture (PostgreSQL and Redis)
        await Fixture.InitializeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await Fixture.DisposeAsync();
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
/// </summary>
[CollectionDefinition("E2ETests")]
public class E2ETestCollection : ICollectionFixture<E2ETestFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionFixture<>] and all the
    // ICollectionFixture<> interfaces.
}