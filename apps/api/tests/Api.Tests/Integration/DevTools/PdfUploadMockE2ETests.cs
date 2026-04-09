using Api.DevTools;
using Api.Infrastructure;
using Api.Services;
using Api.SharedKernel.Application.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using StackExchange.Redis;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.Integration.DevTools;

/// <summary>
/// Custom WebApplicationFactory for PDF upload E2E mock tests.
/// Boots the application in Testing environment with all 8 MOCK_* toggles on,
/// then manually wires AddMeepleDevTools() + UseMeepleDevTools() so the
/// MockHeaderMiddleware is active and emits X-Meeple-Mock on every response.
///
/// The factory follows the CorsTestFactory pattern (Testing env + InMemory DB +
/// mocked Redis/Embedding/HybridCache) to avoid requiring real external services.
///
/// Note: AddMeepleDevTools / UseMeepleDevTools are guarded by #if DEBUG + IsDevelopment()
/// in Program.cs, so they are NOT called automatically in Testing environment.
/// We call them explicitly here so the middleware is present regardless of env name.
/// </summary>
public class PdfUploadMockE2ETestFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Testing environment: skips secret validation and DB migrations (Program.cs:102, 891)
        builder.UseEnvironment("Testing");

        // Set all 8 MOCK_* toggles before the host builds so MockToggleStateProvider
        // reads them from Environment.GetEnvironmentVariables() inside AddMeepleDevTools().
        System.Environment.SetEnvironmentVariable("MOCK_LLM", "true");
        System.Environment.SetEnvironmentVariable("MOCK_EMBEDDING", "true");
        System.Environment.SetEnvironmentVariable("MOCK_S3", "true");
        System.Environment.SetEnvironmentVariable("MOCK_SMOLDOCLING", "true");
        System.Environment.SetEnvironmentVariable("MOCK_UNSTRUCTURED", "true");
        System.Environment.SetEnvironmentVariable("MOCK_BGG", "true");
        System.Environment.SetEnvironmentVariable("MOCK_N8N", "true");
        System.Environment.SetEnvironmentVariable("MOCK_RERANKER", "true");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            // Provide dummy values required by services that initialise during startup
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OPENROUTER_API_KEY"] = "test-openrouter-key",
                ["OPENROUTER_API_KEY_FILE"] = null,
                ["ConnectionStrings:Postgres"] = "Host=localhost;Port=5432;Database=dummy;Username=dummy;Password=dummy"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Replace Postgres DbContext with in-memory provider (no real DB required)
            services.RemoveAll(typeof(DbContextOptions<MeepleAiDbContext>));
            services.RemoveAll(typeof(MeepleAiDbContext));
            services.AddDbContext<MeepleAiDbContext>(options =>
            {
                options.UseInMemoryDatabase("PdfUploadMockE2EDb");
            });

            // Replace Redis with a mock (used by HybridCache, SessionCache, BackgroundTaskOrchestrator)
            services.RemoveAll(typeof(IConnectionMultiplexer));
            var mockRedis = new Mock<IConnectionMultiplexer>();
            var mockDatabase = new Mock<IDatabase>();
            mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(mockDatabase.Object);
            services.AddSingleton(mockRedis.Object);

            // Replace embedding service (real impl tries to reach an external HTTP endpoint)
            services.RemoveAll(typeof(IEmbeddingService));
            services.AddScoped<IEmbeddingService>(_ => Mock.Of<IEmbeddingService>());

            // Replace HybridCache service
            services.RemoveAll(typeof(IHybridCacheService));
            services.AddScoped<IHybridCacheService>(_ => Mock.Of<IHybridCacheService>());

            // DomainEventCollector is only registered in non-Testing path of
            // InfrastructureServiceExtensions.AddDatabaseServices(); add it manually.
            services.AddScoped<IDomainEventCollector, DomainEventCollector>();

            // Wire the DevTools service registrations explicitly.
            // Program.cs wraps these calls in `#if DEBUG && IsDevelopment()`, which is
            // not satisfied when UseEnvironment("Testing") is set. We call them here
            // directly so MockToggleStateProvider + MockHeaderMiddleware are available.
            DevToolsServiceCollectionExtensions.AddMeepleDevTools(services);

            // IStartupFilter runs before the application middleware pipeline and gives us
            // a hook to prepend UseMeepleDevTools() without replacing the whole pipeline
            // (which builder.Configure() would do). Using IStartupFilter is the standard
            // WebApplicationFactory pattern for injecting middleware in tests.
            services.AddSingleton<IStartupFilter, MockHeaderStartupFilter>();
        });
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Clean up MOCK_* env vars so they don't leak to other tests
            System.Environment.SetEnvironmentVariable("MOCK_LLM", null);
            System.Environment.SetEnvironmentVariable("MOCK_EMBEDDING", null);
            System.Environment.SetEnvironmentVariable("MOCK_S3", null);
            System.Environment.SetEnvironmentVariable("MOCK_SMOLDOCLING", null);
            System.Environment.SetEnvironmentVariable("MOCK_UNSTRUCTURED", null);
            System.Environment.SetEnvironmentVariable("MOCK_BGG", null);
            System.Environment.SetEnvironmentVariable("MOCK_N8N", null);
            System.Environment.SetEnvironmentVariable("MOCK_RERANKER", null);
        }
        base.Dispose(disposing);
    }
}

/// <summary>
/// IStartupFilter that prepends MockHeaderMiddleware to the application pipeline.
/// Using IStartupFilter is the standard WebApplicationFactory pattern for injecting
/// middleware in tests without replacing the entire pipeline (which builder.Configure()
/// would do). The filter wraps the existing pipeline: it calls next(app) to run all
/// other middleware registrations first, then inserts UseMeepleDevTools() at position 0.
/// </summary>
internal sealed class MockHeaderStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            DevToolsServiceCollectionExtensions.UseMeepleDevTools(app);
            next(app);
        };
    }
}

/// <summary>
/// E2E integration test verifying that the MeepleDev mock infrastructure
/// fully wires through the application pipeline.
///
/// With all 8 MOCK_* toggles on and the DevTools middleware in the pipeline,
/// any response — including the lightweight /health/live liveness probe —
/// must carry the X-Meeple-Mock response header whose value starts with
/// "backend-di:" followed by the comma-separated list of active mocks.
///
/// Endpoint chosen: /health/live
/// Rationale: uses Predicate = _ => false (no actual health checks executed),
/// so it returns HTTP 200 regardless of database/Redis availability, making it
/// the safest unauthenticated probe for an integration test that avoids real
/// infrastructure (Program.cs:591-594).
/// </summary>
[Trait("Category", TestCategories.Integration)]
public sealed class PdfUploadMockE2ETests : IClassFixture<PdfUploadMockE2ETestFactory>
{
    private readonly PdfUploadMockE2ETestFactory _factory;
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public PdfUploadMockE2ETests(PdfUploadMockE2ETestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task HealthLive_WithAllMocksEnabled_Returns200WithMockHeader()
    {
        // Arrange
        using var client = _factory.CreateClient();

        // Act — /health/live: Predicate = _ => false, returns Healthy with no checks
        var response = await client.GetAsync("/health/live", TestCancellationToken);

        // Assert: liveness probe must succeed
        response.IsSuccessStatusCode.Should().BeTrue(
            $"expected 2xx from /health/live but got {(int)response.StatusCode} {response.StatusCode}");

        // Assert: X-Meeple-Mock header must be present (middleware wired by factory)
        response.Headers.Contains("X-Meeple-Mock").Should().BeTrue(
            "X-Meeple-Mock header must be present on all responses when MockHeaderMiddleware is in the pipeline");

        // Assert: header value must start with "backend-di:" and list all 8 mocks
        var headerValue = string.Join(",", response.Headers.GetValues("X-Meeple-Mock"));
        headerValue.Should().StartWith("backend-di:",
            $"X-Meeple-Mock must start with 'backend-di:' but was '{headerValue}'");

        // All 8 known mock service keys must appear in the header
        var activeMocks = headerValue["backend-di:".Length..].Split(',', StringSplitOptions.RemoveEmptyEntries);
        activeMocks.Should().Contain("bgg", "MOCK_BGG=true");
        activeMocks.Should().Contain("embedding", "MOCK_EMBEDDING=true");
        activeMocks.Should().Contain("llm", "MOCK_LLM=true");
        activeMocks.Should().Contain("n8n", "MOCK_N8N=true");
        activeMocks.Should().Contain("reranker", "MOCK_RERANKER=true");
        activeMocks.Should().Contain("s3", "MOCK_S3=true");
        activeMocks.Should().Contain("smoldocling", "MOCK_SMOLDOCLING=true");
        activeMocks.Should().Contain("unstructured", "MOCK_UNSTRUCTURED=true");
    }
}
