using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetUserBadges;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.Administration;

/// <summary>
/// Integration tests for GET /admin/users/{userId}/badges endpoint (Issue #3140).
/// Verifies admin can view all user badges including hidden ones.
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Administration")]
[Trait("Issue", "3140")]
public sealed class GetUserBadgesAdminEndpointIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IMediator? _mediator;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public GetUserBadgesAdminEndpointIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_getuserbadgesadmin_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseNpgsql(connectionString, o => o.UseVector()); // Issue #3547: Enable pgvector
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        services.AddScoped<Api.SharedKernel.Application.Services.IDomainEventCollector, Api.SharedKernel.Application.Services.DomainEventCollector>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

        var serviceProvider = services.BuildServiceProvider();
        _dbContext = serviceProvider.GetRequiredService<MeepleAiDbContext>();
        _mediator = serviceProvider.GetRequiredService<IMediator>();

        // Run migrations with retry
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await _dbContext.Database.MigrateAsync(TestCancellationToken);
                break;
            }
            catch (NpgsqlException) when (attempt < 2)
            {
                await Task.Delay(TestConstants.Timing.RetryDelay, TestCancellationToken);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null) await _dbContext.DisposeAsync();
        if (!string.IsNullOrEmpty(_databaseName))
        {
            try { await _fixture.DropIsolatedDatabaseAsync(_databaseName); }
            catch { }
        }
    }

    [Fact(Timeout = 30000)]
    public async Task GetUserBadges_WithIncludeHidden_ReturnsAllBadges()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await SeedUserWithBadgesAsync(userId, visibleCount: 2, hiddenCount: 1);

        var query = new GetUserBadgesQuery(userId, IncludeHidden: true);

        // Act
        var result = await _mediator!.Send(query, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result.Should().Contain(b => b.IsDisplayed);
        result.Should().Contain(b => !b.IsDisplayed);
    }

    [Fact(Timeout = 30000)]
    public async Task GetUserBadges_WithoutIncludeHidden_ReturnsOnlyVisible()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await SeedUserWithBadgesAsync(userId, visibleCount: 2, hiddenCount: 1);

        var query = new GetUserBadgesQuery(userId, IncludeHidden: false);

        // Act
        var result = await _mediator!.Send(query, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().OnlyContain(b => b.IsDisplayed);
    }

    [Fact(Timeout = 30000)]
    public async Task GetUserBadges_WithNonExistentUser_ReturnsEmptyList()
    {
        // Arrange
        var nonExistentUserId = Guid.NewGuid();
        var query = new GetUserBadgesQuery(nonExistentUserId, IncludeHidden: true);

        // Act
        var result = await _mediator!.Send(query, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact(Timeout = 30000)]
    public async Task GetUserBadges_WithUserNoBadges_ReturnsEmptyList()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await SeedUserAsync(userId);

        var query = new GetUserBadgesQuery(userId, IncludeHidden: true);

        // Act
        var result = await _mediator!.Send(query, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact(Timeout = 30000)]
    public async Task GetUserBadges_WithAllHiddenBadges_ReturnsHiddenOnly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await SeedUserWithBadgesAsync(userId, visibleCount: 0, hiddenCount: 3);

        var query = new GetUserBadgesQuery(userId, IncludeHidden: true);

        // Act
        var result = await _mediator!.Send(query, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result.Should().OnlyContain(b => !b.IsDisplayed);
    }

    [Fact(Timeout = 30000)]
    public async Task GetUserBadges_WithNullQuery_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = async () => await _mediator!.Send(null!, TestCancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact(Timeout = 30000)]
    public async Task GetUserBadges_ReturnsBadgesOrderedCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await SeedUserWithBadgesAsync(userId, visibleCount: 3, hiddenCount: 0);

        var query = new GetUserBadgesQuery(userId, IncludeHidden: true);

        // Act
        var result = await _mediator!.Send(query, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        // Verify ordering (DisplayOrder, then EarnedAt desc)
        result.Should().BeInAscendingOrder(b => b.Code); // Proxy for DisplayOrder
    }

    [Fact(Timeout = 30000)]
    public async Task GetUserBadges_ExcludesRevokedBadges()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await SeedUserWithBadgesAsync(userId, visibleCount: 2, hiddenCount: 1, revokedCount: 1);

        var query = new GetUserBadgesQuery(userId, IncludeHidden: true);

        // Act
        var result = await _mediator!.Send(query, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3); // 2 visible + 1 hidden, excludes revoked
    }

    [Fact(Timeout = 30000)]
    public async Task GetUserBadges_ReturnsCompleteBadgeData()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await SeedUserWithBadgesAsync(userId, visibleCount: 1, hiddenCount: 0);

        var query = new GetUserBadgesQuery(userId, IncludeHidden: true);

        // Act
        var result = await _mediator!.Send(query, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        var badge = result[0];
        badge.Id.Should().NotBeEmpty();
        badge.Code.Should().NotBeNullOrEmpty();
        badge.Name.Should().NotBeNullOrEmpty();
        badge.Description.Should().NotBeNullOrEmpty();
        badge.EarnedAt.Should().BeAfter(DateTime.UtcNow.AddDays(-1));
        badge.IsDisplayed.Should().BeTrue();
    }

    // Helper methods
    private async Task SeedUserAsync(Guid userId)
    {
        var userEntity = CreateTestUserEntity(userId);
        _dbContext!.Users.Add(userEntity);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
        _dbContext.ChangeTracker.Clear();
    }

    private async Task SeedUserWithBadgesAsync(
        Guid userId,
        int visibleCount,
        int hiddenCount,
        int revokedCount = 0)
    {
        // Seed user
        var userEntity = CreateTestUserEntity(userId);
        _dbContext!.Users.Add(userEntity);

        // Seed badges
        var badges = new List<Api.Infrastructure.Entities.SharedGameCatalog.BadgeEntity>();
        for (int i = 0; i < visibleCount + hiddenCount + revokedCount; i++)
        {
            var badge = new Api.Infrastructure.Entities.SharedGameCatalog.BadgeEntity
            {
                Id = Guid.NewGuid(),
                Code = $"TEST_BADGE_{i + 1}",
                Name = $"Test Badge {i + 1}",
                Description = $"Test badge description {i + 1}",
                IconUrl = $"/badges/test_{i + 1}.png",
                Tier = 1, // Bronze
                Category = 1, // Generic
                IsActive = true,
                DisplayOrder = i + 1,
                RequirementJson = "{}",
                CreatedAt = DateTime.UtcNow
            };
            badges.Add(badge);
            _dbContext!.Badges.Add(badge);
        }

        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Seed user badges
        for (int i = 0; i < visibleCount + hiddenCount + revokedCount; i++)
        {
            var userBadge = new Api.Infrastructure.Entities.SharedGameCatalog.UserBadgeEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                BadgeId = badges[i].Id,
                EarnedAt = DateTime.UtcNow.AddDays(-i),
                IsDisplayed = i < visibleCount, // First N are visible
                RevokedAt = i >= (visibleCount + hiddenCount) ? DateTime.UtcNow : null // Last M are revoked
            };
            _dbContext!.UserBadges.Add(userBadge);
        }

        await _dbContext.SaveChangesAsync(TestCancellationToken);
        _dbContext.ChangeTracker.Clear();
    }

    private static Api.Infrastructure.Entities.UserEntity CreateTestUserEntity(Guid userId)
    {
        return new Api.Infrastructure.Entities.UserEntity
        {
            Id = userId,
            Email = $"test_{userId:N}@example.com",
            DisplayName = $"Test User {userId.ToString("N")[..8]}",
            Role = "user",
            Tier = "free",
            Level = 1,
            ExperiencePoints = 0,
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow
        };
    }
}
