using System.Net;
using Api.Infrastructure;
using Api.Models;
using Api.Services;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Api.Tests.Routing;

/// <summary>
/// Endpoint contract test: verifies frontend API URLs resolve to backend routes.
///
/// Strategy:
/// - KnownRoutes: routes the frontend calls that MUST exist on the backend.
///   They should return 401 (unauthenticated) or any non-404/405, proving the route is registered.
/// - PendingBackendRoutes: routes the frontend calls but the backend has NOT yet implemented.
///   They must return 404/405; when they stop doing so, move them to KnownRoutes.
///
/// Uses a minimal in-process test server (no real DB, no containers) so this test
/// runs fast as part of the unit / CI pipeline without external dependencies.
///
/// See: endpoint-audit-2026-04-15, agentsClient.ts BACKEND MISSING annotations.
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "Routing")]
public sealed class EndpointContractTests : IClassFixture<RouteContractTestFactory>
{
    private readonly HttpClient _client;

    public EndpointContractTests(RouteContractTestFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    // -----------------------------------------------------------------------
    // Known routes — frontend calls these, backend must have them registered.
    // Expected status: anything except 404 / 405 (typically 401 Unauthorized).
    // -----------------------------------------------------------------------
    public static IEnumerable<object[]> KnownRoutes()
    {
        // Auth
        yield return ["POST", "/api/v1/auth/login"];
        yield return ["POST", "/api/v1/auth/register"];
        yield return ["GET", "/api/v1/auth/me"];
        yield return ["GET", "/api/v1/auth/session/status"];

        // Library
        yield return ["GET", "/api/v1/library"];
        yield return ["GET", "/api/v1/library/games/00000000-0000-0000-0000-000000000001/agent-config"];
        yield return ["PUT", "/api/v1/library/games/00000000-0000-0000-0000-000000000001/agent-config"];
        yield return ["POST", "/api/v1/library/games/00000000-0000-0000-0000-000000000001/agent-config"];

        // PDF / ingest
        yield return ["GET", "/api/v1/pdfs/00000000-0000-0000-0000-000000000001/progress"];
        yield return ["POST", "/api/v1/ingest/pdf"];

        // Knowledge Base
        yield return ["GET", "/api/v1/knowledge-base/00000000-0000-0000-0000-000000000001/status"];
        yield return ["POST", "/api/v1/knowledge-base/search"];

        // Sessions
        yield return ["GET", "/api/v1/sessions/active"];
        yield return ["GET", "/api/v1/sessions/history"];

        // Chat
        yield return ["GET", "/api/v1/chat-threads"];

        // Models
        yield return ["GET", "/api/v1/models"];

        // Games
        yield return ["GET", "/api/v1/games"];

        // Notifications
        yield return ["GET", "/api/v1/notifications"];

        // Wishlist
        yield return ["GET", "/api/v1/wishlist"];

        // Playlists
        yield return ["GET", "/api/v1/playlists"];

        // Play Records
        yield return ["GET", "/api/v1/play-records/history"];

        // Game Nights
        yield return ["GET", "/api/v1/game-nights"];

        // Contact (public, no auth)
        yield return ["POST", "/api/v1/contact"];
    }

    // -----------------------------------------------------------------------
    // Pending routes — frontend calls these but NO backend implementation exists.
    // Expected status: 404 or 405 (route not registered).
    // When a route starts returning something else, move it to KnownRoutes!
    // -----------------------------------------------------------------------
    public static IEnumerable<object[]> PendingBackendRoutes()
    {
        // Agent CRUD — agentsClient.ts marks these as BACKEND MISSING (audit 2026-04-15).
        yield return ["GET", "/api/v1/agents", "agent listing"];
        yield return ["GET", "/api/v1/agents/00000000-0000-0000-0000-000000000001", "agent by id"];
        yield return ["GET", "/api/v1/agents/00000000-0000-0000-0000-000000000001/status", "agent status"];
        yield return ["GET", "/api/v1/agent-typologies", "typology listing"];
        yield return ["GET", "/api/v1/agents/recent?limit=10", "recent agents"];
        yield return ["POST", "/api/v1/agents/user", "create user agent"];
        yield return ["GET", "/api/v1/user/agent-slots", "agent slots"];
        yield return ["POST", "/api/v1/agents/create-with-setup", "create with setup"];
        yield return ["POST", "/api/v1/agents/quick-create", "quick create tutor"];
        yield return ["PUT", "/api/v1/agents/00000000-0000-0000-0000-000000000001/configure", "configure agent"];
        yield return ["GET", "/api/v1/agents/00000000-0000-0000-0000-000000000001/configuration", "get agent config"];
        yield return ["PATCH", "/api/v1/agents/00000000-0000-0000-0000-000000000001/configuration", "update agent config"];

    }

    [Theory]
    [MemberData(nameof(KnownRoutes))]
    public async Task KnownRoute_ShouldNotReturn404(string method, string url)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (method is "POST" or "PUT" or "PATCH")
            request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");

        var response = await _client.SendAsync(request);

        // Any status except 404/405 = route is registered (typically 401 unauthenticated)
        Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(PendingBackendRoutes))]
    public async Task PendingRoute_ShouldReturn404UntilImplemented(
        string method, string url, string description)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (method is "POST" or "PUT" or "PATCH")
            request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");

        var response = await _client.SendAsync(request);

        // If this assertion starts FAILING, the route was implemented!
        // → Move the row from PendingBackendRoutes to KnownRoutes.
        Assert.True(
            response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed,
            $"Route {method} {url} ({description}) now returns {(int)response.StatusCode} {response.StatusCode}. " +
            "Backend implementation detected — move this entry from PendingBackendRoutes to KnownRoutes.");
    }
}

/// <summary>
/// Lightweight WebApplicationFactory for endpoint contract tests.
///
/// Uses SQLite InMemory instead of Postgres (no Testcontainers needed),
/// mocks all external services (Redis, embeddings, cache), and strips
/// hosted background services so startup is fast and dependency-free.
///
/// This factory is intentionally minimal: we only need the ASP.NET routing
/// pipeline to start so we can probe HTTP status codes.
/// </summary>
public sealed class RouteContractTestFactory : WebApplicationFactory<Program>
{
    static RouteContractTestFactory()
    {
        Environment.SetEnvironmentVariable("DISABLE_RATE_LIMITING", "true");
        Environment.SetEnvironmentVariable("RateLimiting__Enabled", "false");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.Sources.Clear();
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Use SQLite InMemory — no external DB required
                ["ConnectionStrings:DefaultConnection"] = "DataSource=:memory:",
                ["ConnectionStrings:Postgres"] = "DataSource=:memory:",
                // JWT (required so auth middleware can start)
                ["Jwt:Secret"] = "contract-test-secret-key-minimum-32-characters-long",
                ["Jwt:Issuer"] = "MeepleAI-ContractTest",
                ["Jwt:Audience"] = "MeepleAI-ContractTest",
                // OpenRouter placeholder
                ["OpenRouter:ApiKey"] = "contract-test-key",
                ["OpenRouter:BaseUrl"] = "https://test.local",
                // Disable all external services
                ["BoardGameGeek:Enabled"] = "false",
                ["Embedding:Enabled"] = "false",
                ["Embedding:Url"] = "http://localhost:8000",
                ["Qdrant:Enabled"] = "false",
                ["Qdrant:Host"] = "localhost",
                ["Qdrant:Port"] = "6333",
                ["Redis:Enabled"] = "false",
                ["Redis:ConnectionString"] = "localhost:6379",
                // Session config
                ["Authentication:SessionManagement:SessionExpirationDays"] = "30",
                // Admin seed (skipped because hosted services are removed)
                ["Admin:Email"] = "admin@test.local",
                ["Admin:Password"] = "TestAdmin123!",
                ["Admin:DisplayName"] = "Test Admin",
                ["INITIAL_ADMIN_EMAIL"] = "admin@test.local",
                ["INITIAL_ADMIN_PASSWORD"] = "TestAdmin123!",
                ["INITIAL_ADMIN_DISPLAY_NAME"] = "Test Admin",
                // Observability off
                ["Observability:Enabled"] = "false",
                ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "",
                // Rate limiting off
                ["RateLimiting:Enabled"] = "false",
                // CORS
                ["Cors:Origins:0"] = "http://localhost:3000",
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove all background services — prevents startup failures for services
            // that need real DB / Redis / external endpoints.
            var hostedServiceDescriptors = services
                .Where(d => d.ServiceType == typeof(IHostedService))
                .ToList();
            foreach (var descriptor in hostedServiceDescriptors)
                services.Remove(descriptor);

            // Replace EF DbContext with SQLite InMemory (pgvector-free)
            services.RemoveAll<DbContextOptions<MeepleAiDbContext>>();
            services.RemoveAll<MeepleAiDbContext>();
            services.RemoveAll<IDomainEventCollector>();

            services.AddScoped<IDomainEventCollector, DomainEventCollector>();

            services.AddDbContext<MeepleAiDbContext>(options =>
            {
                options.UseSqlite("DataSource=:memory:");
                options.EnableSensitiveDataLogging();
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Mock Redis
            services.RemoveAll(typeof(IConnectionMultiplexer));
            var mockRedis = new Mock<IConnectionMultiplexer>();
            var mockDatabase = new Mock<IDatabase>();
            mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                     .Returns(mockDatabase.Object);
            services.AddSingleton(mockRedis.Object);

            // Mock embedding service
            services.RemoveAll(typeof(IEmbeddingService));
            services.AddScoped<IEmbeddingService>(_ => Mock.Of<IEmbeddingService>());

            // Pass-through hybrid cache (no Redis needed)
            services.RemoveAll(typeof(IHybridCacheService));
            services.AddScoped<IHybridCacheService, ContractTestHybridCacheService>();

            // Mock configuration service — returns sensible defaults
            services.RemoveAll(typeof(IConfigurationService));
            var mockConfigService = new Mock<IConfigurationService>();
            mockConfigService
                .Setup(c => c.GetValueAsync<bool?>(It.IsAny<string>(), It.IsAny<bool?>(), It.IsAny<string?>()))
                .ReturnsAsync((string _, bool? def, string? __) => def);
            mockConfigService
                .Setup(c => c.GetValueAsync<int?>(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string?>()))
                .ReturnsAsync((string _, int? def, string? __) => def);
            mockConfigService
                .Setup(c => c.GetValueAsync<string?>(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
                .ReturnsAsync((string _, string? def, string? __) => def);
            mockConfigService
                .Setup(c => c.GetConfigurationByKeyAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((SystemConfigurationDto?)null);
            services.AddScoped<IConfigurationService>(_ => mockConfigService.Object);
        });
    }
}

/// <summary>
/// No-op IHybridCacheService: always executes the factory without caching.
/// </summary>
internal sealed class ContractTestHybridCacheService : IHybridCacheService
{
    public async Task<T> GetOrCreateAsync<T>(
        string cacheKey,
        Func<CancellationToken, Task<T>> factory,
        string[]? tags = null,
        TimeSpan? expiration = null,
        CancellationToken ct = default) where T : class
        => await factory(ct).ConfigureAwait(false);

    public Task RemoveAsync(string cacheKey, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task<int> RemoveByTagAsync(string tag, CancellationToken ct = default)
        => Task.FromResult(0);

    public Task<int> RemoveByTagsAsync(string[] tags, CancellationToken ct = default)
        => Task.FromResult(0);

    public Task<HybridCacheStats> GetStatsAsync(CancellationToken ct = default)
        => Task.FromResult(new HybridCacheStats());
}
