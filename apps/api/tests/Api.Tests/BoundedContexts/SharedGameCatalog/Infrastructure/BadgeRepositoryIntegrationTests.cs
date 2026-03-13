using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain;
using Api.SharedKernel.Domain.Interfaces;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure;

/// <summary>
/// Integration tests for BadgeRepository.
/// Tests: Badge CRUD operations with actual database.
/// ISSUE-2731: Infrastructure - EF Core Migrations e Repository
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class BadgeRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private MeepleAiDbContext _dbContext = null!;
    private IBadgeRepository _repository = null!;

    public BadgeRepositoryIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"badge_test_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        // Create unique test database
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);

        // Build DbContext with test database
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(connectionString, o => o.UseVector()) // Issue #3547
            .Options;

        // Mock dependencies
        var mockMediator = new Mock<IMediator>();
        var eventCollectorMock = new Mock<IDomainEventCollector>();
        eventCollectorMock.Setup(x => x.GetAndClearEvents())
            .Returns(new List<IDomainEvent>().AsReadOnly());

        _dbContext = new MeepleAiDbContext(options, mockMediator.Object, eventCollectorMock.Object);
        await _dbContext.Database.MigrateAsync();

        _repository = new BadgeRepository(_dbContext);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    #region GetAllActiveAsync Tests

    [Fact]
    public async Task GetAllActiveAsync_ReturnsOnlyActiveBadges()
    {
        // Arrange
        var activeBadge = Badge.Create(
            "ACTIVE_BADGE",
            "Active Badge",
            "An active badge",
            BadgeTier.Bronze,
            BadgeCategory.Contribution,
            BadgeRequirement.ForFirstContribution(),
            displayOrder: 10);

        var inactiveBadge = Badge.Create(
            "INACTIVE_BADGE",
            "Inactive Badge",
            "An inactive badge",
            BadgeTier.Bronze,
            BadgeCategory.Contribution,
            BadgeRequirement.ForFirstContribution(),
            displayOrder: 20);
        inactiveBadge.Deactivate();

        await SeedBadgeAsync(activeBadge);
        await SeedBadgeAsync(inactiveBadge);

        // Act
        var result = await _repository.GetAllActiveAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal("ACTIVE_BADGE", result[0].Code);
        Assert.True(result[0].IsActive);
    }

    [Fact]
    public async Task GetAllActiveAsync_OrdersByDisplayOrderThenTier()
    {
        // Arrange
        var badge1 = Badge.Create("BADGE_1", "First", "Desc", BadgeTier.Gold, BadgeCategory.Contribution,
            BadgeRequirement.ForFirstContribution(), displayOrder: 20);
        var badge2 = Badge.Create("BADGE_2", "Second", "Desc", BadgeTier.Bronze, BadgeCategory.Contribution,
            BadgeRequirement.ForFirstContribution(), displayOrder: 10);
        var badge3 = Badge.Create("BADGE_3", "Third", "Desc", BadgeTier.Silver, BadgeCategory.Contribution,
            BadgeRequirement.ForFirstContribution(), displayOrder: 10);

        await SeedBadgeAsync(badge1);
        await SeedBadgeAsync(badge2);
        await SeedBadgeAsync(badge3);

        // Act
        var result = await _repository.GetAllActiveAsync();

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal("BADGE_2", result[0].Code); // Display order 10, tier Bronze (0)
        Assert.Equal("BADGE_3", result[1].Code); // Display order 10, tier Silver (1)
        Assert.Equal("BADGE_1", result[2].Code); // Display order 20
    }

    #endregion

    #region GetByCodeAsync Tests

    [Fact]
    public async Task GetByCodeAsync_WithExistingCode_ReturnsBadge()
    {
        // Arrange
        var badge = Badge.Create(
            "TEST_BADGE",
            "Test Badge",
            "A test badge",
            BadgeTier.Silver,
            BadgeCategory.Quality,
            BadgeRequirement.ForContributionCount(5),
            displayOrder: 100);

        await SeedBadgeAsync(badge);

        // Act
        var result = await _repository.GetByCodeAsync("TEST_BADGE");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("TEST_BADGE", result.Code);
        Assert.Equal("Test Badge", result.Name);
        Assert.Equal(BadgeTier.Silver, result.Tier);
    }

    [Fact]
    public async Task GetByCodeAsync_WithNonExistingCode_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByCodeAsync("NON_EXISTING");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByCodeAsync_IsCaseInsensitive()
    {
        // Arrange
        var badge = Badge.Create("TEST_BADGE", "Name", "Desc", BadgeTier.Bronze,
            BadgeCategory.Contribution, BadgeRequirement.ForFirstContribution());

        await SeedBadgeAsync(badge);

        // Act
        var result = await _repository.GetByCodeAsync("test_badge");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("TEST_BADGE", result.Code);
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_WithExistingId_ReturnsBadge()
    {
        // Arrange
        var badge = Badge.Create("BADGE_ID", "Name", "Desc", BadgeTier.Gold,
            BadgeCategory.Engagement, BadgeRequirement.ForTopContributor(10));

        await SeedBadgeAsync(badge);

        // Act
        var result = await _repository.GetByIdAsync(badge.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(badge.Id, result.Id);
        Assert.Equal("BADGE_ID", result.Code);
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistingId_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByIdAsync(Guid.NewGuid());

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region Helper Methods

    private async Task SeedBadgeAsync(Badge badge)
    {
        var entity = new BadgeEntity
        {
            Id = badge.Id,
            Code = badge.Code,
            Name = badge.Name,
            Description = badge.Description,
            IconUrl = badge.IconUrl,
            Tier = (int)badge.Tier,
            Category = (int)badge.Category,
            IsActive = badge.IsActive,
            DisplayOrder = badge.DisplayOrder,
            RequirementJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                type = (int)badge.Requirement.Type,
                minContributions = badge.Requirement.MinContributions,
                minDocuments = badge.Requirement.MinDocuments,
                minApprovalRate = badge.Requirement.MinApprovalRate,
                consecutiveApprovalsWithoutChanges = badge.Requirement.ConsecutiveApprovalsWithoutChanges,
                topContributorRank = badge.Requirement.TopContributorRank,
                customRule = badge.Requirement.CustomRule
            }),
            CreatedAt = badge.CreatedAt,
            ModifiedAt = badge.ModifiedAt
        };

        _dbContext.Badges.Add(entity);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    #endregion
}
