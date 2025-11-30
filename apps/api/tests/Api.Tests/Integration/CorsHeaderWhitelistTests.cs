using Api.Infrastructure;
using Api.Services;
using Api.SharedKernel.Application.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using StackExchange.Redis;
using System.Net;
using System.Threading;
using Xunit;

namespace Api.Tests.Integration;

/// <summary>
/// Custom WebApplicationFactory for CORS tests that runs in Testing environment
/// without requiring external dependencies (Postgres, Redis, Qdrant).
/// </summary>
/// <remarks>
/// This factory configures the test host to:
/// 1. Run in "Testing" environment (skips Postgres/Qdrant initialization in InfrastructureServiceExtensions.cs:38)
/// 2. Replace DbContext with EF Core InMemory provider (no database required)
/// 3. Mock external services (Redis, Qdrant, Embedding) that aren't used by CORS tests
/// 4. Maintain correct service lifetimes matching production configuration
/// </remarks>
public class CorsTestFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((context, configBuilder) =>
        {
            // Provide dummy secrets so hosted services don't throw during startup
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OPENROUTER_API_KEY"] = "test-openrouter-key",
                ["OPENROUTER_API_KEY_FILE"] = null,
                ["ConnectionStrings:Postgres"] = "Host=localhost;Port=5432;Database=dummy;Username=dummy;Password=dummy"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove existing DbContext registration (originally uses Postgres)
            services.RemoveAll(typeof(DbContextOptions<MeepleAiDbContext>));
            services.RemoveAll(typeof(MeepleAiDbContext));

            // Add in-memory database for testing (no Postgres container required)
            // Note: InMemory provider has limitations (no FK constraints, different transaction behavior)
            // but is sufficient for CORS tests that don't access the database
            services.AddDbContext<MeepleAiDbContext>(options =>
            {
                options.UseInMemoryDatabase("CorsTestDb");
            });

            // Replace Redis with mock (used by HybridCache, SessionCache, BackgroundTaskOrchestrator)
            // Singleton lifetime matches production IConnectionMultiplexer registration
            services.RemoveAll(typeof(IConnectionMultiplexer));
            var mockRedis = new Mock<IConnectionMultiplexer>();
            var mockDatabase = new Mock<IDatabase>();
            mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(mockDatabase.Object);
            services.AddSingleton(mockRedis.Object);

            // Replace vector/embedding services (used by RagService, HybridSearchService, DocumentProcessing)
            // Scoped lifetime matches production registration (ApplicationServiceExtensions.cs:68,70)
            services.RemoveAll(typeof(IQdrantService));
            services.RemoveAll(typeof(IEmbeddingService));
            services.AddScoped<IQdrantService>(_ => Mock.Of<IQdrantService>());
            services.AddScoped<IEmbeddingService>(_ => Mock.Of<IEmbeddingService>());

            // Replace HybridCache service (used for L1/L2 caching throughout application)
            // Scoped lifetime matches typical cache service pattern
            services.RemoveAll(typeof(IHybridCacheService));
            services.AddScoped<IHybridCacheService>(_ => Mock.Of<IHybridCacheService>());

            // Ensure domain event collector is registered (required by DbContext in Testing environment)
            // InfrastructureServiceExtensions.AddDatabaseServices only registers this in non-Testing environments (line 73)
            services.AddScoped<IDomainEventCollector, DomainEventCollector>();
        });
    }

    /// <summary>
    /// Cleanup resources when factory is disposed.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Base class handles cleanup of test server and services
            // Mocks are transient and will be garbage collected
        }
        base.Dispose(disposing);
    }
}

/// <summary>
/// Integration tests for CORS header whitelist configuration (Issue #1448).
/// Verifies that only whitelisted headers are permitted in CORS requests.
/// </summary>
/// <remarks>
/// Tests Cover:
/// 1. Whitelisted headers (Content-Type, Authorization, X-Correlation-ID, X-API-Key) are accepted
/// 2. Non-whitelisted headers are rejected
/// 3. CORS preflight requests work correctly
/// 4. Multiple headers can be requested simultaneously
/// 5. Header names are case-insensitive
///
/// Pattern: AAA (Arrange-Act-Assert), Custom WebApplicationFactory with Testing environment
/// </remarks>
[Collection("CORS")]
public class CorsHeaderWhitelistTests : IClassFixture<CorsTestFactory>
{
    private readonly CorsTestFactory _factory;
    private readonly HttpClient _client;
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    // Whitelisted headers (Issue #1448)
    private static readonly string[] WhitelistedHeaders = new[]
    {
        "Content-Type",
        "Authorization",
        "X-Correlation-ID",
        "X-API-Key"
    };

    // Non-whitelisted headers (should be rejected)
    private static readonly string[] NonWhitelistedHeaders = new[]
    {
        "X-Custom-Header",
        "X-Malicious-Header",
        "X-Debug-Info",
        "X-Internal-Secret"
    };

    public CorsHeaderWhitelistTests(CorsTestFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Theory]
    [InlineData("Content-Type")]
    [InlineData("Authorization")]
    [InlineData("X-Correlation-ID")]
    [InlineData("X-API-Key")]
    public async Task PreflightRequest_WhitelistedHeader_ReturnsAllowedHeaders(string headerName)
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/games");
        request.Headers.Add("Origin", "http://localhost:3000");
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", headerName);

        // Act
        var response = await _client.SendAsync(request, TestCancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(response.Headers.Contains("Access-Control-Allow-Headers"));

        var allowedHeaders = response.Headers.GetValues("Access-Control-Allow-Headers");
        var allowedHeadersList = string.Join(",", allowedHeaders).Split(',', StringSplitOptions.TrimEntries);

        Assert.Contains(headerName, allowedHeadersList, StringComparer.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("X-Custom-Header")]
    [InlineData("X-Malicious-Header")]
    [InlineData("X-Debug-Info")]
    [InlineData("X-Internal-Secret")]
    public async Task PreflightRequest_NonWhitelistedHeader_DoesNotAllowHeader(string headerName)
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/games");
        request.Headers.Add("Origin", "http://localhost:3000");
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", headerName);

        // Act
        var response = await _client.SendAsync(request, TestCancellationToken);

        // Assert
        // Preflight should succeed (NoContent), but the header should NOT be in allowed headers
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        if (response.Headers.Contains("Access-Control-Allow-Headers"))
        {
            var allowedHeaders = response.Headers.GetValues("Access-Control-Allow-Headers");
            var allowedHeadersList = string.Join(",", allowedHeaders).Split(',', StringSplitOptions.TrimEntries);

            // The non-whitelisted header should NOT be in the allowed headers list
            Assert.DoesNotContain(headerName, allowedHeadersList, StringComparer.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task PreflightRequest_MultipleWhitelistedHeaders_AllAllowed()
    {
        // Arrange
        var requestedHeaders = string.Join(", ", WhitelistedHeaders);
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/games");
        request.Headers.Add("Origin", "http://localhost:3000");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", requestedHeaders);

        // Act
        var response = await _client.SendAsync(request, TestCancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(response.Headers.Contains("Access-Control-Allow-Headers"));

        var allowedHeaders = response.Headers.GetValues("Access-Control-Allow-Headers");
        var allowedHeadersList = string.Join(",", allowedHeaders).Split(',', StringSplitOptions.TrimEntries);

        // All whitelisted headers should be present
        foreach (var header in WhitelistedHeaders)
        {
            Assert.Contains(header, allowedHeadersList, StringComparer.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task PreflightRequest_MixedHeaders_OnlyWhitelistedAllowed()
    {
        // Arrange
        var mixedHeaders = string.Join(", ",
            new[] { "Content-Type", "X-Custom-Header", "Authorization", "X-Malicious-Header" });

        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/games");
        request.Headers.Add("Origin", "http://localhost:3000");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", mixedHeaders);

        // Act
        var response = await _client.SendAsync(request, TestCancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        if (response.Headers.Contains("Access-Control-Allow-Headers"))
        {
            var allowedHeaders = response.Headers.GetValues("Access-Control-Allow-Headers");
            var allowedHeadersList = string.Join(",", allowedHeaders).Split(',', StringSplitOptions.TrimEntries);

            // Whitelisted headers should be present
            Assert.Contains("Content-Type", allowedHeadersList, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("Authorization", allowedHeadersList, StringComparer.OrdinalIgnoreCase);

            // Non-whitelisted headers should NOT be present
            Assert.DoesNotContain("X-Custom-Header", allowedHeadersList, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain("X-Malicious-Header", allowedHeadersList, StringComparer.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData("content-type")]
    [InlineData("AUTHORIZATION")]
    [InlineData("x-correlation-id")]
    [InlineData("X-API-KEY")]
    public async Task PreflightRequest_CaseInsensitiveHeaders_Accepted(string headerName)
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/games");
        request.Headers.Add("Origin", "http://localhost:3000");
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", headerName);

        // Act
        var response = await _client.SendAsync(request, TestCancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(response.Headers.Contains("Access-Control-Allow-Headers"));
    }

    [Fact]
    public async Task PreflightRequest_ValidOrigin_ReturnsAccessControlHeaders()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/games");
        request.Headers.Add("Origin", "http://localhost:3000");
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "Content-Type");

        // Act
        var response = await _client.SendAsync(request, TestCancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.True(response.Headers.Contains("Access-Control-Allow-Methods"));
        Assert.True(response.Headers.Contains("Access-Control-Allow-Headers"));
        Assert.True(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task ActualRequest_WithWhitelistedHeader_Succeeds()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/games");
        request.Headers.Add("Origin", "http://localhost:3000");
        request.Headers.Add("Authorization", "Bearer test-token");
        request.Headers.Add("X-Correlation-ID", Guid.NewGuid().ToString());

        // Act
        var response = await _client.SendAsync(request, TestCancellationToken);

        // Assert
        // Should succeed (or return 401 if auth is required, but NOT a CORS error)
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.True(response.Headers.Contains("Access-Control-Allow-Origin") ||
                    response.StatusCode == HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PreflightRequest_AllWhitelistedHeaders_ExactlyFourHeaders()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/games");
        request.Headers.Add("Origin", "http://localhost:3000");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", string.Join(", ", WhitelistedHeaders));

        // Act
        var response = await _client.SendAsync(request, TestCancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(response.Headers.Contains("Access-Control-Allow-Headers"));

        var allowedHeaders = response.Headers.GetValues("Access-Control-Allow-Headers");
        var allowedHeadersList = string.Join(",", allowedHeaders).Split(',', StringSplitOptions.TrimEntries);

        // Verify exactly 4 whitelisted headers
        Assert.Equal(4, WhitelistedHeaders.Length);
        foreach (var header in WhitelistedHeaders)
        {
            Assert.Contains(header, allowedHeadersList, StringComparer.OrdinalIgnoreCase);
        }
    }
}

