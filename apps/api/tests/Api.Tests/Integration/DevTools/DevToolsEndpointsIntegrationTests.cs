using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Api.DevTools;
using Api.Infrastructure;
using Api.Services;
using Api.SharedKernel.Application.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Api.Tests.Integration.DevTools;

/// <summary>
/// Integration tests for the DevTools toggle HTTP endpoints:
///   GET  /dev/toggles
///   PATCH /dev/toggles
///   POST /dev/toggles/reset
///
/// Uses "Testing" environment (skips secret validation) and manually wires
/// AddMeepleDevTools + the endpoint routes via IStartupFilter/UseEndpoints.
///
/// Tests share a single WebApplicationFactory (IClassFixture) for startup performance.
/// They are grouped in a collection to enforce sequential execution, since the
/// MockToggleStateProvider singleton is shared across tests.
/// </summary>
[Collection("DevToolsEndpoints")]
public class DevToolsEndpointsIntegrationTests
    : IClassFixture<DevToolsEndpointsTestFactory>
{
    private readonly DevToolsEndpointsTestFactory _factory;

    public DevToolsEndpointsIntegrationTests(DevToolsEndpointsTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetToggles_ReturnsCurrentState()
    {
        using var client = _factory.CreateClient();

        // Reset to env defaults first to ensure deterministic state regardless of test order
        var resetResponse = await client.PostAsync("/dev/toggles/reset", null).ConfigureAwait(true);
        Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);

        var response = await client.GetAsync("/dev/toggles").ConfigureAwait(true);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content
            .ReadFromJsonAsync<TestGetTogglesResponse>()
            .ConfigureAwait(true);

        Assert.NotNull(body);
        Assert.True(body!.Toggles.ContainsKey("llm"));
        Assert.True(body.Toggles["llm"]);      // MOCK_LLM=true set in static ctor
        Assert.False(body.Toggles["embedding"]); // MOCK_EMBEDDING=false set in static ctor
        Assert.Contains("llm", body.KnownServices);
    }

    [Fact]
    public async Task PatchToggles_UpdatesAndReturnsNewState()
    {
        using var client = _factory.CreateClient();
        var response = await client.PatchAsJsonAsync("/dev/toggles", new
        {
            toggles = new Dictionary<string, bool> { ["llm"] = false }
        }).ConfigureAwait(true);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content
            .ReadFromJsonAsync<TestPatchTogglesResponse>()
            .ConfigureAwait(true);

        Assert.NotNull(body);
        Assert.Contains("llm", body!.Updated);
        Assert.False(body.Toggles["llm"]);
    }

    [Fact]
    public async Task PatchToggles_UnknownService_Returns400()
    {
        using var client = _factory.CreateClient();
        var response = await client.PatchAsJsonAsync("/dev/toggles", new
        {
            toggles = new Dictionary<string, bool> { ["nonexistent"] = true }
        }).ConfigureAwait(true);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ResetToggles_RestoresEnvDefaults()
    {
        using var client = _factory.CreateClient();

        // First flip llm to false
        await client.PatchAsJsonAsync("/dev/toggles", new
        {
            toggles = new Dictionary<string, bool> { ["llm"] = false }
        }).ConfigureAwait(true);

        // Then reset
        var response = await client.PostAsync("/dev/toggles/reset", null).ConfigureAwait(true);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content
            .ReadFromJsonAsync<TestGetTogglesResponse>()
            .ConfigureAwait(true);

        Assert.NotNull(body);
        // env default is MOCK_LLM=true (set in factory static ctor)
        Assert.True(body!.Toggles["llm"]);
    }

    private sealed class TestGetTogglesResponse
    {
        public Dictionary<string, bool> Toggles { get; set; } = new();
        public List<string> KnownServices { get; set; } = new();
    }

    private sealed class TestPatchTogglesResponse
    {
        public List<string> Updated { get; set; } = new();
        public Dictionary<string, bool> Toggles { get; set; } = new();
    }
}

/// <summary>
/// WebApplicationFactory that boots the app in "Testing" environment
/// (skipping secret validation) while manually wiring DevTools service
/// registrations and endpoints. A fake IWebHostEnvironment reporting
/// EnvironmentName="Development" is injected so that MapMeepleDevTools
/// registers the /dev/toggles routes.
/// </summary>
public sealed class DevToolsEndpointsTestFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// Set MOCK_LLM=true and MOCK_EMBEDDING=false before the host builds
    /// so MockToggleStateProvider captures these as bootstrap defaults.
    /// </summary>
    static DevToolsEndpointsTestFactory()
    {
        Environment.SetEnvironmentVariable("MOCK_LLM", "true");
        Environment.SetEnvironmentVariable("MOCK_EMBEDDING", "false");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing"); // skips secret validation

        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OPENROUTER_API_KEY"] = "test-key",
                ["OPENROUTER_API_KEY_FILE"] = null,
                ["ConnectionStrings:Postgres"] =
                    "Host=localhost;Port=5432;Database=dummy;Username=dummy;Password=dummy",
            });
        });

        builder.ConfigureServices(services =>
        {
            // InMemory DB — no real Postgres required
            services.RemoveAll(typeof(DbContextOptions<MeepleAiDbContext>));
            services.RemoveAll(typeof(MeepleAiDbContext));
            services.AddDbContext<MeepleAiDbContext>(opts =>
                opts.UseInMemoryDatabase("DevToolsEndpointsTestDb"));

            // Mock Redis
            services.RemoveAll(typeof(IConnectionMultiplexer));
            var mockRedis = new Mock<IConnectionMultiplexer>();
            var mockDb = new Mock<IDatabase>();
            mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(mockDb.Object);
            services.AddSingleton(mockRedis.Object);

            // Mock EmbeddingService (external HTTP)
            services.RemoveAll(typeof(IEmbeddingService));
            services.AddScoped<IEmbeddingService>(_ => Mock.Of<IEmbeddingService>());

            // Mock HybridCache
            services.RemoveAll(typeof(IHybridCacheService));
            services.AddScoped<IHybridCacheService>(_ => Mock.Of<IHybridCacheService>());

            // DomainEventCollector is only registered in non-Testing path of
            // InfrastructureServiceExtensions.AddDatabaseServices(); add it manually.
            services.AddScoped<IDomainEventCollector, DomainEventCollector>();

            // Wire DevTools services manually (guarded by #if DEBUG + IsDevelopment in Program.cs)
            DevToolsServiceCollectionExtensions.AddMeepleDevTools(services);

            // Wire DevTools middleware + endpoints via IStartupFilter.
            // Routes are registered via UseEndpoints so they work in Testing environment
            // without triggering the IsDevelopment() guard in MapMeepleDevTools.
            services.AddSingleton<IStartupFilter, DevToolsEndpointsStartupFilter>();
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Environment.SetEnvironmentVariable("MOCK_LLM", null);
            Environment.SetEnvironmentVariable("MOCK_EMBEDDING", null);
        }
        base.Dispose(disposing);
    }
}

/// <summary>
/// IStartupFilter that wires both the MockHeaderMiddleware and the /dev/toggles
/// routes directly using UseEndpoints, bypassing the IsDevelopment() guard in
/// MapMeepleDevTools (which would block route registration in "Testing" env).
/// </summary>
internal sealed class DevToolsEndpointsStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            DevToolsServiceCollectionExtensions.UseMeepleDevTools(app);
            next(app);

            // Register /dev/toggles routes on top of the existing endpoint pipeline.
            // UseEndpoints appends to the existing routing, not replacing it.
            app.UseEndpoints(endpoints =>
            {
                var group = endpoints.MapGroup("/dev/toggles");
                group.MapGet("/", Api.DevTools.Http.DevToolsEndpoints.GetToggles);
                group.MapPatch("/", Api.DevTools.Http.DevToolsEndpoints.PatchToggles);
                group.MapPost("/reset", Api.DevTools.Http.DevToolsEndpoints.ResetToggles);
            });
        };
    }
}
