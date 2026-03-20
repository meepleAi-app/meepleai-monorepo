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
using FluentAssertions;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure;

/// <summary>
/// Integration tests for UserBadgeRepository.
/// Tests: UserBadge CRUD operations and queries with actual database.
/// ISSUE-2731: Infrastructure - EF Core Migrations e Repository
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class UserBadgeRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private MeepleAiDbContext _dbContext = null!;
    private IUserBadgeRepository _repository = null!;
    private static readonly Guid TestUserId1 = Guid.NewGuid();
    private static readonly Guid TestUserId2 = Guid.NewGuid();
    private Badge _testBadge = null!;

    public UserBadgeRepositoryIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"userbadge_test_{Guid.NewGuid():N}";
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

        _repository = new UserBadgeRepository(_dbContext);

        // Seed test badge
        _testBadge = Badge.Create(
            "TEST_BADGE",
            "Test Badge",
            "A test badge",
            BadgeTier.Bronze,
            BadgeCategory.Contribution,
            BadgeRequirement.ForFirstContribution());

        await SeedBadgeAsync(_testBadge);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    #region AddAsync Tests

    [Fact]
    public async Task AddAsync_WithValidUserBadge_PersistsToDatabase()
    {
        // Arrange
        var userBadge = UserBadge.Award(TestUserId1, _testBadge.Id, _testBadge.Code);

        // Act
        await _repository.AddAsync(userBadge);
        await _dbContext.SaveChangesAsync();

        // Assert
        var entity = await _dbContext.UserBadges.FirstOrDefaultAsync(e => e.Id == userBadge.Id);
        entity.Should().NotBeNull();
        entity.UserId.Should().Be(TestUserId1);
        entity.BadgeId.Should().Be(_testBadge.Id);
        entity.IsDisplayed.Should().BeTrue();
        entity.RevokedAt.Should().BeNull();
    }

    #endregion

    #region GetBadgeIdsByUserAsync Tests

    [Fact]
    public async Task GetBadgeIdsByUserAsync_ReturnsOnlyActiveBadges()
    {
        // Arrange
        var badge1 = await CreateAndSeedBadgeAsync("BADGE_1", "Name 1");
        var badge2 = await CreateAndSeedBadgeAsync("BADGE_2", "Name 2");

        var userBadge1 = UserBadge.Award(TestUserId1, badge1.Id, badge1.Code);
        var userBadge2 = UserBadge.Award(TestUserId1, badge2.Id, badge2.Code);
        userBadge2.Revoke("Test revocation");

        await SeedUserBadgeAsync(userBadge1);
        await SeedUserBadgeAsync(userBadge2);

        // Act
        var result = await _repository.GetBadgeIdsByUserAsync(TestUserId1);

        // Assert
        result.Should().ContainSingle();
        result.Should().Contain(badge1.Id);
        result.Should().NotContain(badge2.Id);
    }

    [Fact]
    public async Task GetBadgeIdsByUserAsync_WithNoBadges_ReturnsEmptySet()
    {
        // Act
        var result = await _repository.GetBadgeIdsByUserAsync(Guid.NewGuid());

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region GetByUserIdAsync Tests

    [Fact]
    public async Task GetByUserIdAsync_ReturnsUserBadgesOrderedByEarnedDate()
    {
        // Arrange
        var badge1 = await CreateAndSeedBadgeAsync("BADGE_1", "First");
        var badge2 = await CreateAndSeedBadgeAsync("BADGE_2", "Second");

        var userBadge1 = UserBadge.Award(TestUserId1, badge1.Id, badge1.Code);
        await Task.Delay(10); // Ensure different timestamps
        var userBadge2 = UserBadge.Award(TestUserId1, badge2.Id, badge2.Code);

        await SeedUserBadgeAsync(userBadge1);
        await SeedUserBadgeAsync(userBadge2);

        // Act
        var result = await _repository.GetByUserIdAsync(TestUserId1);

        // Assert
        result.Count.Should().Be(2);
        result[0].BadgeId.Should().Be(badge2.Id); // Most recent first
        result[1].BadgeId.Should().Be(badge1.Id);
    }

    [Fact]
    public async Task GetByUserIdAsync_ExcludesHiddenByDefault()
    {
        // Arrange
        var userBadge = UserBadge.Award(TestUserId1, _testBadge.Id, _testBadge.Code);
        userBadge.Hide();

        await SeedUserBadgeAsync(userBadge);

        // Act
        var result = await _repository.GetByUserIdAsync(TestUserId1, includeHidden: false);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByUserIdAsync_IncludesHiddenWhenRequested()
    {
        // Arrange
        var userBadge = UserBadge.Award(TestUserId1, _testBadge.Id, _testBadge.Code);
        userBadge.Hide();

        await SeedUserBadgeAsync(userBadge);

        // Act
        var result = await _repository.GetByUserIdAsync(TestUserId1, includeHidden: true);

        // Assert
        result.Should().ContainSingle();
        result[0].IsDisplayed.Should().BeFalse();
    }

    #endregion

    #region GetUsersByBadgeCodeAsync Tests

    [Fact]
    public async Task GetUsersByBadgeCodeAsync_ReturnsAllUsersWithBadge()
    {
        // Arrange
        var userBadge1 = UserBadge.Award(TestUserId1, _testBadge.Id, _testBadge.Code);
        var userBadge2 = UserBadge.Award(TestUserId2, _testBadge.Id, _testBadge.Code);

        await SeedUserBadgeAsync(userBadge1);
        await SeedUserBadgeAsync(userBadge2);

        // Act
        var result = await _repository.GetUsersByBadgeCodeAsync("TEST_BADGE");

        // Assert
        result.Count.Should().Be(2);
        result.Should().Contain(ub => ub.UserId == TestUserId1);
        result.Should().Contain(ub => ub.UserId == TestUserId2);
    }

    [Fact]
    public async Task GetUsersByBadgeCodeAsync_ExcludesRevokedBadges()
    {
        // Arrange
        var userBadge = UserBadge.Award(TestUserId1, _testBadge.Id, _testBadge.Code);
        userBadge.Revoke("Test");

        await SeedUserBadgeAsync(userBadge);

        // Act
        var result = await _repository.GetUsersByBadgeCodeAsync("TEST_BADGE");

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region GetByUserAndBadgeAsync Tests

    [Fact]
    public async Task GetByUserAndBadgeAsync_WithExistingMatch_ReturnsUserBadge()
    {
        // Arrange
        var userBadge = UserBadge.Award(TestUserId1, _testBadge.Id, _testBadge.Code);
        await SeedUserBadgeAsync(userBadge);

        // Act
        var result = await _repository.GetByUserAndBadgeAsync(TestUserId1, _testBadge.Id);

        // Assert
        result.Should().NotBeNull();
        result.UserId.Should().Be(TestUserId1);
        result.BadgeId.Should().Be(_testBadge.Id);
    }

    [Fact]
    public async Task GetByUserAndBadgeAsync_WithNoMatch_ReturnsNull()
    {
        // Act
        var result = await _repository.GetByUserAndBadgeAsync(Guid.NewGuid(), _testBadge.Id);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region Helper Methods

    private async Task<Badge> CreateAndSeedBadgeAsync(string code, string name)
    {
        var badge = Badge.Create(code, name, "Description", BadgeTier.Bronze,
            BadgeCategory.Contribution, BadgeRequirement.ForFirstContribution());
        await SeedBadgeAsync(badge);
        return badge;
    }

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

    private async Task SeedUserBadgeAsync(UserBadge userBadge)
    {
        var entity = new UserBadgeEntity
        {
            Id = userBadge.Id,
            UserId = userBadge.UserId,
            BadgeId = userBadge.BadgeId,
            EarnedAt = userBadge.EarnedAt,
            TriggeringShareRequestId = userBadge.TriggeringShareRequestId,
            IsDisplayed = userBadge.IsDisplayed,
            RevokedAt = userBadge.RevokedAt,
            RevocationReason = userBadge.RevocationReason
        };

        _dbContext.UserBadges.Add(entity);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    #endregion
}