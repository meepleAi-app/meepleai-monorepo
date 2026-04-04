using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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

        // Create WebApplicationFactory using shared factory
        _factory = IntegrationWebApplicationFactory.Create(connectionString);

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
