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
    // Issue #2707: Removed _dbContext field - use fresh scope for each operation to prevent ObjectDisposedException

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

        // Issue #2707: Initialize database using scoped context (disposed after setup)
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            await dbContext.Database.MigrateAsync();

            // Seed test data within the same scope
            await SeedTestDataAsync(dbContext);
        }

        _client = _factory.CreateClient();
    }

    private async Task SeedTestDataAsync(MeepleAiDbContext dbContext)
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
        dbContext.Set<UserEntity>().Add(user);

        // Seed categories
        var category = new GameCategoryEntity { Id = Guid.NewGuid(), Name = "Strategy", Slug = "strategy" };
        dbContext.Set<GameCategoryEntity>().Add(category);

        // Seed mechanics
        var mechanic = new GameMechanicEntity { Id = Guid.NewGuid(), Name = "Deck Building", Slug = "deck-building" };
        dbContext.Set<GameMechanicEntity>().Add(mechanic);

        await dbContext.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        _factory?.Dispose();
        // Issue #2707: No _dbContext field to dispose - all contexts are scoped and disposed automatically
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

    // ========================================
    // APPROVAL WORKFLOW TESTS (Issue #2514)
    // ========================================

    [Fact]
    public async Task SubmitForApproval_WithDraftGame_ReturnsNoContent()
    {
        // Arrange
        var game = await CreateTestGameAsync(GameStatus.Draft);

        // Act
        var response = await _client.PostAsync($"/api/v1/admin/shared-games/{game.Id}/submit-for-approval", null);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Issue #2707: Use fresh scope for assertions to prevent ObjectDisposedException
        using var assertScope = _factory.Services.CreateScope();
        var dbContext = assertScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var updatedGame = await dbContext.SharedGames.FindAsync(game.Id);
        Assert.NotNull(updatedGame);
        Assert.Equal((int)GameStatus.PendingApproval, updatedGame.Status);
    }

    [Fact]
    public async Task ApprovePublication_WithPendingApprovalGame_ReturnsNoContent()
    {
        // Arrange
        var game = await CreateTestGameAsync(GameStatus.PendingApproval);

        // Act
        var response = await _client.PostAsync($"/api/v1/admin/shared-games/{game.Id}/approve-publication", null);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Issue #2707: Use fresh scope for assertions to prevent ObjectDisposedException
        using var assertScope = _factory.Services.CreateScope();
        var dbContext = assertScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var updatedGame = await dbContext.SharedGames.FindAsync(game.Id);
        Assert.NotNull(updatedGame);
        Assert.Equal((int)GameStatus.Published, updatedGame.Status);
    }

    [Fact]
    public async Task RejectPublication_WithPendingApprovalGame_ReturnsNoContent()
    {
        // Arrange
        var game = await CreateTestGameAsync(GameStatus.PendingApproval);
        var request = new { Reason = "Needs better description" };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/v1/admin/shared-games/{game.Id}/reject-publication", request);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Issue #2707: Use fresh scope for assertions to prevent ObjectDisposedException
        using var assertScope = _factory.Services.CreateScope();
        var dbContext = assertScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var updatedGame = await dbContext.SharedGames.FindAsync(game.Id);
        Assert.NotNull(updatedGame);
        Assert.Equal((int)GameStatus.Draft, updatedGame.Status);
    }

    [Fact]
    public async Task GetPendingApprovals_ReturnsPendingGames()
    {
        // Arrange
        await CreateTestGameAsync(GameStatus.PendingApproval);
        await CreateTestGameAsync(GameStatus.PendingApproval);
        await CreateTestGameAsync(GameStatus.Draft); // Should not appear in results

        // Act
        var response = await _client.GetAsync("/api/v1/admin/shared-games/pending-approvals?pageNumber=1&pageSize=20");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<SharedGameDto>>();
        Assert.NotNull(result);
        Assert.Equal(2, result.Total);
        Assert.All(result.Items, g => Assert.Equal(GameStatus.PendingApproval, g.Status));
    }

    /// <summary>
    /// Issue #2707: Use fresh scope to prevent ObjectDisposedException
    /// </summary>
    private async Task<SharedGameEntity> CreateTestGameAsync(GameStatus status)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var game = new SharedGameEntity
        {
            Id = Guid.NewGuid(),
            Title = $"Test Game {Guid.NewGuid():N}",
            YearPublished = 2024,
            Description = "Test description",
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            ImageUrl = "https://example.com/image.jpg",
            ThumbnailUrl = "https://example.com/thumb.jpg",
            Status = (int)status,
            CreatedBy = TestUserId,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.SharedGames.Add(game);
        await dbContext.SaveChangesAsync();
        return game;
    }
}
