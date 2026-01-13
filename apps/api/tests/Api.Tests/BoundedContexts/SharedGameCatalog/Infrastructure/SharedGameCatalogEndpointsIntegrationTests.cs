using System.Net;
using System.Net.Http.Json;
using Api.BoundedContexts.SharedGameCatalog.Application;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.DependencyInjection;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Models;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Interfaces;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure;

/// <summary>
/// Integration tests for SharedGameCatalog HTTP endpoints
/// Tests: Public access, Authorization, Cache scenarios
/// Issue #2371 Phase 2
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class SharedGameCatalogEndpointsIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private MeepleAiDbContext _dbContext = null!;

    private static readonly Guid TestUserId = Guid.NewGuid();

    public SharedGameCatalogEndpointsIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"sharedgame_endpoints_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        // Create isolated test database
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);

        // Create WebApplicationFactory
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");

                builder.ConfigureAppConfiguration((context, configBuilder) =>
                {
                    configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["OPENROUTER_API_KEY"] = "test-key",
                        ["ConnectionStrings:Postgres"] = connectionString
                    });
                });

                builder.ConfigureTestServices(services =>
                {
                    // Replace DbContext with test database
                    services.RemoveAll(typeof(DbContextOptions<MeepleAiDbContext>));
                    services.AddDbContext<MeepleAiDbContext>(options =>
                        options.UseNpgsql(connectionString));

                    // Mock Redis for HybridCache
                    services.RemoveAll(typeof(IConnectionMultiplexer));
                    var mockRedis = new Mock<IConnectionMultiplexer>();
                    services.AddSingleton(mockRedis.Object);

                    // Mock vector/embedding services
                    services.RemoveAll(typeof(Api.Services.IQdrantService));
                    services.RemoveAll(typeof(Api.Services.IEmbeddingService));
                    services.AddScoped<Api.Services.IQdrantService>(_ => Mock.Of<Api.Services.IQdrantService>());
                    services.AddScoped<Api.Services.IEmbeddingService>(_ => Mock.Of<Api.Services.IEmbeddingService>());

                    // Ensure domain event collector is registered
                    services.AddScoped<IDomainEventCollector, Api.SharedKernel.Application.Services.DomainEventCollector>();

                    // Register authorization policies
                    services.AddSharedGameCatalogPolicies();
                });
            });

        // Initialize database
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            await dbContext.Database.MigrateAsync();
            _dbContext = dbContext;

            // Seed test data
            await SeedTestDataAsync();
        }

        _client = _factory.CreateClient();
    }

    private async Task SeedTestDataAsync()
    {
        // Seed test user for FK
        var user = new UserEntity
        {
            Id = TestUserId,
            Email = "test@test.com",
            DisplayName = "Test User",
            Role = "Admin",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Set<UserEntity>().Add(user);

        // Seed categories
        var category = new GameCategoryEntity { Id = Guid.NewGuid(), Name = "Strategy", Slug = "strategy" };
        _dbContext.Set<GameCategoryEntity>().Add(category);

        // Seed mechanics
        var mechanic = new GameMechanicEntity { Id = Guid.NewGuid(), Name = "Deck Building", Slug = "deck-building" };
        _dbContext.Set<GameMechanicEntity>().Add(mechanic);

        await _dbContext.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        _factory?.Dispose();
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }
        await _fixture.DropIsolatedDatabaseAsync(_testDbName);
    }

    [Fact]
    public async Task GetCategories_ReturnsCategories()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/shared-games/categories");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var categories = await response.Content.ReadFromJsonAsync<List<GameCategoryDto>>();
        Assert.NotNull(categories);
        Assert.NotEmpty(categories);
    }

    [Fact]
    public async Task GetMechanics_ReturnsMechanics()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/shared-games/mechanics");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var mechanics = await response.Content.ReadFromJsonAsync<List<GameMechanicDto>>();
        Assert.NotNull(mechanics);
        Assert.NotEmpty(mechanics);
    }

    [Fact]
    public async Task SearchGames_WithNoFilters_ReturnsPublishedGames()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/shared-games?pageNumber=1&pageSize=20");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<SharedGameDto>>();
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetGameById_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/v1/shared-games/{nonExistentId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
