using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
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
using FluentAssertions;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure;

/// <summary>
/// Integration tests for Review Lock HTTP endpoints
/// Tests: StartReview, ReleaseReview, GetMyActiveReviews
/// Issue #2737
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class ReviewLockEndpointsIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private string _adminSessionToken = null!;

    internal static readonly Guid TestAdminId = Guid.NewGuid();
    internal static readonly Guid TestContributorId = Guid.NewGuid();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public ReviewLockEndpointsIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"review_lock_endpoints_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        // Create isolated test database
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);

        // Set environment variables for rate limiting bypass (must be set before factory creation)
        // The rate limiting middleware checks for these environment variables
        Environment.SetEnvironmentVariable("DISABLE_RATE_LIMITING", "true");
        Environment.SetEnvironmentVariable("RateLimiting__Enabled", "false");
        // Set ASPNETCORE_ENVIRONMENT to Testing to skip secret validation in Program.cs
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");

        // Create WebApplicationFactory
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                // Use Testing environment (matches ASPNETCORE_ENVIRONMENT) to skip secret validation
                // and avoid startup failures from missing secret files
                builder.UseEnvironment("Testing");

                builder.ConfigureAppConfiguration((context, configBuilder) =>
                {
                    // Clear existing configuration to ensure test config takes precedence
                    configBuilder.Sources.Clear();

                    configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        // Database connection (must use DefaultConnection - that's what the app expects)
                        ["ConnectionStrings:DefaultConnection"] = connectionString,
                        // JWT configuration (required for session authentication)
                        ["Jwt:Secret"] = "test-secret-key-for-integration-tests-minimum-32-characters-long",
                        ["Jwt:Issuer"] = "MeepleAI-Test",
                        ["Jwt:Audience"] = "MeepleAI-Test",
                        // OpenRouter
                        ["OpenRouter:ApiKey"] = "test-key",
                        ["OpenRouter:BaseUrl"] = "https://test.local",
                        // Disable external services
                        ["BoardGameGeek:Enabled"] = "false",
                        ["Embedding:Enabled"] = "false",
                        ["Embedding:Url"] = "http://localhost:8000",
                        ["Qdrant:Enabled"] = "false",
                        ["Qdrant:Host"] = "localhost",
                        ["Qdrant:Port"] = "6333",
                        // Redis configuration
                        ["Redis:Enabled"] = "true",
                        ["Redis:ConnectionString"] = _fixture.RedisConnectionString,
                        // Session configuration
                        ["Authentication:SessionManagement:SessionExpirationDays"] = "30",
                        // Admin configuration
                        ["Admin:Email"] = "admin@test.local",
                        ["Admin:Password"] = "TestAdmin123!",
                        ["Admin:DisplayName"] = "Test Admin",
                        // CI environment admin seeding configuration (required by AutoConfigurationService)
                        ["INITIAL_ADMIN_EMAIL"] = "admin@test.local",
                        ["INITIAL_ADMIN_PASSWORD"] = "TestAdmin123!",
                        ["INITIAL_ADMIN_DISPLAY_NAME"] = "Test Admin",
                        // Disable observability
                        ["Observability:Enabled"] = "false",
                        ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "",
                        // Disable rate limiting
                        ["RateLimiting:Enabled"] = "false"
                    });
                });

                builder.ConfigureServices(services =>
                {
                    // Remove all hosted services to prevent startup failures
                    // Hosted services might fail during startup and cause the host to dispose
                    var hostedServiceDescriptors = services
                        .Where(d => d.ServiceType == typeof(IHostedService))
                        .ToList();
                    foreach (var descriptor in hostedServiceDescriptors)
                    {
                        services.Remove(descriptor);
                    }

                    // Remove and replace DbContext with test database
                    services.RemoveAll<DbContextOptions<MeepleAiDbContext>>();
                    services.RemoveAll<MeepleAiDbContext>();
                    services.RemoveAll<IDomainEventCollector>();

                    // Register domain event collector
                    services.AddScoped<IDomainEventCollector, DomainEventCollector>();

                    services.AddDbContext<MeepleAiDbContext>((serviceProvider, options) =>
                    {
                        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
                        var connStr = configuration.GetConnectionString("DefaultConnection")
                            ?? throw new InvalidOperationException("DefaultConnection not configured");

                        options.UseNpgsql(connStr, o => o.UseVector()); // Issue #3547: Enable pgvector
                        options.EnableSensitiveDataLogging();
                        options.ConfigureWarnings(warnings =>
                            warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
                    });

                    // Mock Redis for HybridCache with proper database mock
                    services.RemoveAll(typeof(IConnectionMultiplexer));
                    var mockRedis = new Mock<IConnectionMultiplexer>();
                    var mockDatabase = new Mock<IDatabase>();
                    mockRedis.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                        .Returns(mockDatabase.Object);
                    services.AddSingleton(mockRedis.Object);

                    // Mock vector/embedding services
                    services.RemoveAll(typeof(Api.Services.IEmbeddingService));
                    services.AddScoped<Api.Services.IEmbeddingService>(_ => Mock.Of<Api.Services.IEmbeddingService>());

                    // Mock IHybridCacheService (required for ReviewLockConfigService and session validation)
                    // Must actually execute the factory function to return proper values
                    services.RemoveAll(typeof(Api.Services.IHybridCacheService));
                    services.AddScoped<Api.Services.IHybridCacheService, TestHybridCacheService>();
                });
            });

        // Initialize database using scoped context
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

            // Verify connection string is correct
            var connString = dbContext.Database.GetConnectionString();
            if (string.IsNullOrEmpty(connString) || !connString.Contains(_testDbName))
            {
                throw new InvalidOperationException($"DbContext is using wrong connection string. Expected to contain '{_testDbName}', got: {connString}");
            }

            await dbContext.Database.MigrateAsync();

            // Seed test data and create admin session
            await SeedTestDataAsync(dbContext);

            // Create admin session using TestSessionHelper
            var (_, token) = await TestSessionHelper.CreateAdminSessionAsync(dbContext, TestAdminId);
            _adminSessionToken = token;
        }

        _client = _factory.CreateClient();
    }

    /// <summary>
    /// Pre-computed password hash for test users (matches TestSessionHelper).
    /// </summary>
    private const string TestPasswordHash = "v1.210000.AAAAAAAAAAAAAAAAAAAAAA==.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";

    private async Task SeedTestDataAsync(MeepleAiDbContext dbContext)
    {
        // Seed contributor user (admin user is created by TestSessionHelper)
        // Must include PasswordHash and Tier for UserRepository.MapToDomain
        var contributor = new UserEntity
        {
            Id = TestContributorId,
            Email = "contributor@test.com",
            DisplayName = "Test Contributor",
            Role = "user",
            PasswordHash = TestPasswordHash,
            Tier = "free",
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
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/admin/share-requests/{shareRequestId}/start-review",
            _adminSessionToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<StartReviewResponse>(JsonOptions);
        result.Should().NotBeNull();
        result.ShareRequestId.Should().Be(shareRequestId);
        (result.LockExpiresAt > DateTime.UtcNow).Should().BeTrue();
        (result.LockExpiresAt <= DateTime.UtcNow.AddMinutes(31)).Should().BeTrue();
    }

    [Fact]
    public async Task StartReview_FromChangesRequestedState_ReturnsOk()
    {
        // Arrange
        var shareRequestId = await CreateShareRequestAsync(ShareRequestStatus.ChangesRequested);
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/admin/share-requests/{shareRequestId}/start-review",
            _adminSessionToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task StartReview_WhenAlreadyLocked_ReturnsConflict()
    {
        // Arrange
        var shareRequestId = await CreateShareRequestAsync(ShareRequestStatus.InReview);
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/admin/share-requests/{shareRequestId}/start-review",
            _adminSessionToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task StartReview_WithNonExistentRequest_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/admin/share-requests/{nonExistentId}/start-review",
            _adminSessionToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task StartReview_FromApprovedState_ReturnsConflict()
    {
        // Arrange - Approved requests cannot be started for review (invalid state transition)
        var shareRequestId = await CreateShareRequestAsync(ShareRequestStatus.Approved);
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/admin/share-requests/{shareRequestId}/start-review",
            _adminSessionToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert - InvalidShareRequestStateException is mapped to Conflict (409)
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    #endregion

    #region ReleaseReview Endpoint Tests

    [Fact]
    public async Task ReleaseReview_WithValidLock_ReturnsNoContent()
    {
        // Arrange
        var shareRequestId = await CreateShareRequestAsync(ShareRequestStatus.InReview);
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/admin/share-requests/{shareRequestId}/release",
            _adminSessionToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify lock was released
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var entity = await dbContext.Set<ShareRequestEntity>()
            .FirstOrDefaultAsync(sr => sr.Id == shareRequestId);

        entity.Should().NotBeNull();
        entity.Status.Should().NotBe((int)ShareRequestStatus.InReview);
        entity.ReviewingAdminId.Should().BeNull();
    }

    [Fact]
    public async Task ReleaseReview_WhenNotInReview_ReturnsBadRequest()
    {
        // Arrange
        var shareRequestId = await CreateShareRequestAsync(ShareRequestStatus.Pending);
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/admin/share-requests/{shareRequestId}/release",
            _adminSessionToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ReleaseReview_WithNonExistentRequest_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/admin/share-requests/{nonExistentId}/release",
            _adminSessionToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region GetMyActiveReviews Endpoint Tests

    [Fact]
    public async Task GetMyActiveReviews_WithNoActiveReviews_ReturnsEmptyList()
    {
        // Arrange
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/admin/share-requests/my-reviews",
            _adminSessionToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<IReadOnlyCollection<ActiveReviewDto>>(JsonOptions);
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMyActiveReviews_WithSingleReview_ReturnsSingleItem()
    {
        // Arrange
        var shareRequestId = await CreateShareRequestAsync(ShareRequestStatus.InReview);
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/admin/share-requests/my-reviews",
            _adminSessionToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<IReadOnlyCollection<ActiveReviewDto>>(JsonOptions);
        result.Should().NotBeNull();
        result.Should().ContainSingle();

        var review = result.First();
        review.ShareRequestId.Should().Be(shareRequestId);
        review.Status.Should().Be(ShareRequestStatus.InReview);
    }

    [Fact]
    public async Task GetMyActiveReviews_WithMultipleReviews_ReturnsAllReviews()
    {
        // Arrange
        await CreateShareRequestAsync(ShareRequestStatus.InReview);
        await CreateShareRequestAsync(ShareRequestStatus.InReview);
        await CreateShareRequestAsync(ShareRequestStatus.InReview);
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/admin/share-requests/my-reviews",
            _adminSessionToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<IReadOnlyCollection<ActiveReviewDto>>(JsonOptions);
        result.Should().NotBeNull();
        result.Count.Should().Be(3);
    }

    [Fact]
    public async Task GetMyActiveReviews_OnlyReturnsInReviewStatus()
    {
        // Arrange
        await CreateShareRequestAsync(ShareRequestStatus.InReview);
        await CreateShareRequestAsync(ShareRequestStatus.Pending);
        await CreateShareRequestAsync(ShareRequestStatus.Approved);
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/admin/share-requests/my-reviews",
            _adminSessionToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<IReadOnlyCollection<ActiveReviewDto>>(JsonOptions);
        result.Should().NotBeNull();
        result.Should().ContainSingle();
        result.Should().AllSatisfy(r => r.Status.Should().Be(ShareRequestStatus.InReview));
    }

    [Fact]
    public async Task GetMyActiveReviews_IncludesCorrectGameAndUserDetails()
    {
        // Arrange
        var shareRequestId = await CreateShareRequestAsync(ShareRequestStatus.InReview);
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/admin/share-requests/my-reviews",
            _adminSessionToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<IReadOnlyCollection<ActiveReviewDto>>(JsonOptions);
        result.Should().NotBeNull();
        result.Should().ContainSingle();

        var review = result.First();
        review.SourceGameId.Should().NotBe(Guid.Empty);
        review.GameTitle.Should().NotBeNull();
        review.ContributorId.Should().Be(TestContributorId);
        review.ContributorName.Should().NotBeNull();
    }

    [Fact]
    public async Task GetMyActiveReviews_IncludesCorrectTimestamps()
    {
        // Arrange
        // Note: CreateShareRequestAsync creates InReview requests with:
        // - ReviewStartedAt = DateTime.UtcNow.AddMinutes(-10) (10 minutes in the past)
        // - ReviewLockExpiresAt = DateTime.UtcNow.AddMinutes(20) (20 minutes in the future)
        var beforeCreation = DateTime.UtcNow;
        await CreateShareRequestAsync(ShareRequestStatus.InReview);
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/admin/share-requests/my-reviews",
            _adminSessionToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<IReadOnlyCollection<ActiveReviewDto>>(JsonOptions);
        result.Should().NotBeNull();
        result.Should().ContainSingle();

        var review = result.First();

        // ReviewStartedAt should be in the past (created as UtcNow - 10 minutes)
        review.ReviewStartedAt.Should().NotBe(default);
        (review.ReviewStartedAt < beforeCreation).Should().BeTrue($"ReviewStartedAt ({review.ReviewStartedAt:O}) should be before creation time ({beforeCreation:O})");
        (review.ReviewStartedAt > beforeCreation.AddMinutes(-15)).Should().BeTrue("ReviewStartedAt should be within 15 minutes of test execution");

        // ReviewLockExpiresAt should be in the future (created as UtcNow + 20 minutes)
        review.ReviewLockExpiresAt.Should().NotBe(default);
        (review.ReviewLockExpiresAt > DateTime.UtcNow).Should().BeTrue("ReviewLockExpiresAt should be in the future");
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

/// <summary>
/// Test stub for IHybridCacheService that executes factory functions directly.
/// Required for integration tests where cached services need to execute their factories.
/// </summary>
internal sealed class TestHybridCacheService : Api.Services.IHybridCacheService
{
    public async Task<T> GetOrCreateAsync<T>(
        string cacheKey,
        Func<CancellationToken, Task<T>> factory,
        string[]? tags = null,
        TimeSpan? expiration = null,
        CancellationToken ct = default) where T : class
    {
        // Execute factory directly - no caching in tests
        return await factory(ct).ConfigureAwait(false);
    }

    public Task RemoveAsync(string cacheKey, CancellationToken ct = default) => Task.CompletedTask;

    public Task<int> RemoveByTagAsync(string tag, CancellationToken ct = default) => Task.FromResult(0);

    public Task<int> RemoveByTagsAsync(string[] tags, CancellationToken ct = default) => Task.FromResult(0);

    public Task<Api.Services.HybridCacheStats> GetStatsAsync(CancellationToken ct = default)
        => Task.FromResult(new Api.Services.HybridCacheStats());
}
