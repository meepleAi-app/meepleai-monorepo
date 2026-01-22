using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetUserBadges;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.SharedGameCatalog.Application.Queries;

public sealed class GetUserBadgesQueryHandlerTests
{
    private readonly DbContextOptions<MeepleAiDbContext> _options;
    private readonly Mock<ILogger<GetUserBadgesQueryHandler>> _logger;
    private readonly Guid _userId = Guid.NewGuid();

    public GetUserBadgesQueryHandlerTests()
    {
        _options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _logger = new Mock<ILogger<GetUserBadgesQueryHandler>>();
    }

    [Fact]
    public async Task Handle_WithDisplayedBadges_ReturnsOnlyDisplayed()
    {
        // Arrange
        await using var context = new MeepleAiDbContext(_options);
        var badge = CreateBadgeEntity("FIRST_CONTRIBUTION");
        var userBadge1 = CreateUserBadgeEntity(_userId, badge.Id, isDisplayed: true);
        var userBadge2 = CreateUserBadgeEntity(_userId, Guid.NewGuid(), isDisplayed: false);

        await context.Badges.AddAsync(badge);
        await context.UserBadges.AddRangeAsync(userBadge1, userBadge2);
        await context.SaveChangesAsync();

        var handler = new GetUserBadgesQueryHandler(context, _logger.Object);
        var query = new GetUserBadgesQuery(_userId, IncludeHidden: false);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].IsDisplayed.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithIncludeHidden_ReturnsAllBadges()
    {
        // Arrange
        await using var context = new MeepleAiDbContext(_options);
        var badge = CreateBadgeEntity("FIRST_CONTRIBUTION");
        var userBadge1 = CreateUserBadgeEntity(_userId, badge.Id, isDisplayed: true);
        var userBadge2 = CreateUserBadgeEntity(_userId, badge.Id, isDisplayed: false);

        await context.Badges.AddAsync(badge);
        await context.UserBadges.AddRangeAsync(userBadge1, userBadge2);
        await context.SaveChangesAsync();

        var handler = new GetUserBadgesQueryHandler(context, _logger.Object);
        var query = new GetUserBadgesQuery(_userId, IncludeHidden: true);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_WithRevokedBadges_ExcludesRevoked()
    {
        // Arrange
        await using var context = new MeepleAiDbContext(_options);
        var badge = CreateBadgeEntity("FIRST_CONTRIBUTION");
        var userBadge1 = CreateUserBadgeEntity(_userId, badge.Id, isDisplayed: true);
        var userBadge2 = CreateUserBadgeEntity(_userId, badge.Id, isDisplayed: true, revokedAt: DateTime.UtcNow);

        await context.Badges.AddAsync(badge);
        await context.UserBadges.AddRangeAsync(userBadge1, userBadge2);
        await context.SaveChangesAsync();

        var handler = new GetUserBadgesQueryHandler(context, _logger.Object);
        var query = new GetUserBadgesQuery(_userId, IncludeHidden: true);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].Code.Should().Be("FIRST_CONTRIBUTION");
    }

    private static BadgeEntity CreateBadgeEntity(string code)
    {
        return new BadgeEntity
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = $"{code} Badge",
            Description = "Test description",
            IconUrl = null,
            Tier = (int)BadgeTier.Bronze,
            Category = (int)BadgeCategory.Contribution,
            IsActive = true,
            DisplayOrder = 1,
            RequirementJson = "{\"Type\":1}",
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = null
        };
    }

    private static UserBadgeEntity CreateUserBadgeEntity(Guid userId, Guid badgeId, bool isDisplayed, DateTime? revokedAt = null)
    {
        return new UserBadgeEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            BadgeId = badgeId,
            EarnedAt = DateTime.UtcNow,
            TriggeringShareRequestId = null,
            IsDisplayed = isDisplayed,
            RevokedAt = revokedAt,
            RevocationReason = revokedAt.HasValue ? "Test revocation" : null
        };
    }
}
