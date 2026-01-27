using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
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
/// Integration tests for Review Lock HTTP endpoints
/// Tests: StartReview, ReleaseReview, GetMyActiveReviews
/// Issue #2737
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class ReviewLockEndpointsIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    internal static readonly Guid TestAdminId = Guid.NewGuid();
    internal static readonly Guid TestContributorId = Guid.NewGuid();

    public ReviewLockEndpointsIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"review_lock_endpoints_{Guid.NewGuid():N}";
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
                    services.AddScoped<IDomainEventCollector, DomainEventCollector>();

                    // Mock IHybridCacheService
                    services.RemoveAll(typeof(Api.Services.IHybridCacheService));
                    services.AddScoped<Api.Services.IHybridCacheService>(_ => Mock.Of<Api.Services.IHybridCacheService>());
                });
            });

        // Initialize database using scoped context
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            await dbContext.Database.MigrateAsync();

            // Seed test data
            await SeedTestDataAsync(dbContext);
        }

        _client = _factory.CreateClient();

        // Set admin authorization header (bypass auth for testing)
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-token");
    }

    private async Task SeedTestDataAsync(MeepleAiDbContext dbContext)
    {
        // Seed admin user
        var admin = new UserEntity
        {
            Id = TestAdminId,
            Email = "admin@test.com",
            DisplayName = "Test Admin",
            Role = "admin",
            CreatedAt = DateTime.UtcNow
        };
        dbContext.Set<UserEntity>().Add(admin);

        // Seed contributor user
        var contributor = new UserEntity
        {
            Id = TestContributorId,
            Email = "contributor@test.com",
            DisplayName = "Test Contributor",
            Role = "user",
            CreatedAt = DateTime.UtcNow
        };
        dbContext.Set<UserEntity>().Add(contributor);

        await dbContext.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        _factory?.Dispose();
        await _fixture.DropIsolatedDatabaseAsync(_testDbName);
    }

    #region StartReview Endpoint Tests

    [Fact]
    public async Task StartReview_FromPendingState_ReturnsOkWithLockDetails()
    {
        // Arrange
        var shareRequestId = await CreateShareRequestAsync(ShareRequestStatus.Pending);

        // Act
        var response = await _client.PostAsync(
            $"/api/v1/admin/share-requests/{shareRequestId}/start-review",
            null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<StartReviewResponse>();
        Assert.NotNull(result);
        Assert.Equal(shareRequestId, result.ShareRequestId);
        Assert.True(result.LockExpiresAt > DateTime.UtcNow);
        Assert.True(result.LockExpiresAt <= DateTime.UtcNow.AddMinutes(31));
    }

    [Fact]
    public async Task StartReview_FromChangesRequestedState_ReturnsOk()
    {
        // Arrange
        var shareRequestId = await CreateShareRequestAsync(ShareRequestStatus.ChangesRequested);

        // Act
        var response = await _client.PostAsync(
            $"/api/v1/admin/share-requests/{shareRequestId}/start-review",
            null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task StartReview_WhenAlreadyLocked_ReturnsConflict()
    {
        // Arrange
        var shareRequestId = await CreateShareRequestAsync(ShareRequestStatus.InReview);

        // Act
        var response = await _client.PostAsync(
            $"/api/v1/admin/share-requests/{shareRequestId}/start-review",
            null);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task StartReview_WithNonExistentRequest_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.PostAsync(
            $"/api/v1/admin/share-requests/{nonExistentId}/start-review",
            null);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task StartReview_FromApprovedState_ReturnsBadRequest()
    {
        // Arrange
        var shareRequestId = await CreateShareRequestAsync(ShareRequestStatus.Approved);

        // Act
        var response = await _client.PostAsync(
            $"/api/v1/admin/share-requests/{shareRequestId}/start-review",
            null);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region ReleaseReview Endpoint Tests

    [Fact]
    public async Task ReleaseReview_WithValidLock_ReturnsNoContent()
    {
        // Arrange
        var shareRequestId = await CreateShareRequestAsync(ShareRequestStatus.InReview);

        // Act
        var response = await _client.PostAsync(
            $"/api/v1/admin/share-requests/{shareRequestId}/release",
            null);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify lock was released
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var entity = await dbContext.Set<ShareRequestEntity>()
            .FirstOrDefaultAsync(sr => sr.Id == shareRequestId);

        Assert.NotNull(entity);
        Assert.NotEqual((int)ShareRequestStatus.InReview, entity.Status);
        Assert.Null(entity.ReviewingAdminId);
    }

    [Fact]
    public async Task ReleaseReview_WhenNotInReview_ReturnsBadRequest()
    {
        // Arrange
        var shareRequestId = await CreateShareRequestAsync(ShareRequestStatus.Pending);

        // Act
        var response = await _client.PostAsync(
            $"/api/v1/admin/share-requests/{shareRequestId}/release",
            null);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ReleaseReview_WithNonExistentRequest_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.PostAsync(
            $"/api/v1/admin/share-requests/{nonExistentId}/release",
            null);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    #region GetMyActiveReviews Endpoint Tests

    [Fact]
    public async Task GetMyActiveReviews_WithNoActiveReviews_ReturnsEmptyList()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/admin/share-requests/my-reviews");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<IReadOnlyCollection<ActiveReviewDto>>();
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetMyActiveReviews_WithSingleReview_ReturnsSingleItem()
    {
        // Arrange
        var shareRequestId = await CreateShareRequestAsync(ShareRequestStatus.InReview);

        // Act
        var response = await _client.GetAsync("/api/v1/admin/share-requests/my-reviews");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<IReadOnlyCollection<ActiveReviewDto>>();
        Assert.NotNull(result);
        Assert.Single(result);

        var review = result.First();
        Assert.Equal(shareRequestId, review.ShareRequestId);
        Assert.Equal(ShareRequestStatus.InReview, review.Status);
    }

    [Fact]
    public async Task GetMyActiveReviews_WithMultipleReviews_ReturnsAllReviews()
    {
        // Arrange
        await CreateShareRequestAsync(ShareRequestStatus.InReview);
        await CreateShareRequestAsync(ShareRequestStatus.InReview);
        await CreateShareRequestAsync(ShareRequestStatus.InReview);

        // Act
        var response = await _client.GetAsync("/api/v1/admin/share-requests/my-reviews");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<IReadOnlyCollection<ActiveReviewDto>>();
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task GetMyActiveReviews_OnlyReturnsInReviewStatus()
    {
        // Arrange
        await CreateShareRequestAsync(ShareRequestStatus.InReview);
        await CreateShareRequestAsync(ShareRequestStatus.Pending);
        await CreateShareRequestAsync(ShareRequestStatus.Approved);

        // Act
        var response = await _client.GetAsync("/api/v1/admin/share-requests/my-reviews");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<IReadOnlyCollection<ActiveReviewDto>>();
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.All(result, r => Assert.Equal(ShareRequestStatus.InReview, r.Status));
    }

    [Fact]
    public async Task GetMyActiveReviews_IncludesCorrectGameAndUserDetails()
    {
        // Arrange
        var shareRequestId = await CreateShareRequestAsync(ShareRequestStatus.InReview);

        // Act
        var response = await _client.GetAsync("/api/v1/admin/share-requests/my-reviews");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<IReadOnlyCollection<ActiveReviewDto>>();
        Assert.NotNull(result);
        Assert.Single(result);

        var review = result.First();
        Assert.NotEqual(Guid.Empty, review.SourceGameId);
        Assert.NotNull(review.GameTitle);
        Assert.Equal(TestContributorId, review.ContributorId);
        Assert.NotNull(review.ContributorName);
    }

    [Fact]
    public async Task GetMyActiveReviews_IncludesCorrectTimestamps()
    {
        // Arrange
        var beforeCreation = DateTime.UtcNow;
        await CreateShareRequestAsync(ShareRequestStatus.InReview);
        var afterCreation = DateTime.UtcNow;

        // Act
        var response = await _client.GetAsync("/api/v1/admin/share-requests/my-reviews");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<IReadOnlyCollection<ActiveReviewDto>>();
        Assert.NotNull(result);
        Assert.Single(result);

        var review = result.First();
        Assert.NotNull(review.ReviewStartedAt);
        Assert.True(review.ReviewStartedAt >= beforeCreation);
        Assert.True(review.ReviewStartedAt <= afterCreation);

        Assert.NotNull(review.ReviewLockExpiresAt);
        Assert.True(review.ReviewLockExpiresAt > DateTime.UtcNow);
    }

    #endregion

    #region Helper Methods

    private async Task<Guid> CreateShareRequestAsync(ShareRequestStatus status)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        // Create game with all required fields to satisfy database constraints
        var gameId = Guid.NewGuid();
        var game = new SharedGameEntity
        {
            Id = gameId,
            Title = $"Test Game {Guid.NewGuid():N}",
            Description = "Test game description",
            MinPlayers = 2,        // chk_shared_games_players: min_players > 0
            MaxPlayers = 4,        // chk_shared_games_players: max_players >= min_players
            PlayingTimeMinutes = 60, // chk_shared_games_playing_time: playing_time_minutes > 0
            YearPublished = 2024,  // chk_shared_games_year_published: year_published > 1900 AND <= 2100
            MinAge = 10,           // chk_shared_games_min_age: min_age >= 0
            CreatedAt = DateTime.UtcNow,
            CreatedBy = TestContributorId
        };
        dbContext.Set<SharedGameEntity>().Add(game);

        // Create share request
        var shareRequestId = Guid.NewGuid();
        var shareRequest = new ShareRequestEntity
        {
            Id = shareRequestId,
            UserId = TestContributorId,
            SourceGameId = gameId,
            Status = (int)status,
            StatusBeforeReview = status == ShareRequestStatus.InReview ? (int)ShareRequestStatus.Pending : null,
            ContributionType = (int)ContributionType.NewGame,
            ReviewingAdminId = status == ShareRequestStatus.InReview ? TestAdminId : null,
            ReviewStartedAt = status == ShareRequestStatus.InReview ? DateTime.UtcNow.AddMinutes(-10) : null,
            ReviewLockExpiresAt = status == ShareRequestStatus.InReview ? DateTime.UtcNow.AddMinutes(20) : null,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            CreatedBy = TestContributorId
        };

        dbContext.Set<ShareRequestEntity>().Add(shareRequest);
        await dbContext.SaveChangesAsync();

        return shareRequestId;
    }

    #endregion
}
