using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Seeders;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.Infrastructure.Seeders;

/// <summary>
/// Tests for FeatureFlagSeeder default configuration seeding.
/// Issue #3674: Feature Flag Tier-Based Access Verification
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "SystemConfiguration")]
public sealed class FeatureFlagSeederTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;

    private static readonly Guid TestUserId = new("90000000-0000-0000-0000-000000000099");
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public FeatureFlagSeederTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"meepleai_featureflag_seed_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(connectionString, o => o.UseVector())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;

        var mockMediator = new Mock<MediatR.IMediator>();
        var mockEventCollector = new Mock<Api.SharedKernel.Application.Services.IDomainEventCollector>();
        mockEventCollector.Setup(x => x.GetAndClearEvents())
            .Returns(new List<Api.SharedKernel.Domain.Interfaces.IDomainEvent>().AsReadOnly());

        _dbContext = new MeepleAiDbContext(options, mockMediator.Object, mockEventCollector.Object);
        await _dbContext.Database.MigrateAsync(TestCancellationToken);

        // Seed required User for FK constraints
        await SeedTestUserAsync();
    }

    private async Task SeedTestUserAsync()
    {
        var user = new UserEntity
        {
            Id = TestUserId,
            Email = "test-featureflag-seed@meepleai.dev",
            DisplayName = "Test User FeatureFlag",
            Role = "admin",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext!.Set<UserEntity>().Add(user);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null)
            await _dbContext.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    [Fact]
    public async Task SeedFeatureFlagsAsync_ShouldCreateDefaultFlags()
    {
        // Arrange
        var logger = new Mock<Microsoft.Extensions.Logging.ILogger>();

        // Act
        await FeatureFlagSeeder.SeedFeatureFlagsAsync(
            _dbContext!, TestUserId, logger.Object, TestCancellationToken);

        // Assert
        var flags = await _dbContext!.Set<SystemConfigurationEntity>()
            .Where(c => c.Category == "FeatureFlags")
            .ToListAsync(TestCancellationToken);

        // Each feature has 1 global + 3 tier entries = 4 entries
        // 10 features * 4 = 40 entries
        flags.Should().HaveCount(40);
    }

    [Fact]
    public async Task SeedFeatureFlagsAsync_ShouldBeIdempotent()
    {
        // Arrange
        var logger = new Mock<Microsoft.Extensions.Logging.ILogger>();

        // Act - Run twice
        await FeatureFlagSeeder.SeedFeatureFlagsAsync(
            _dbContext!, TestUserId, logger.Object, TestCancellationToken);
        await FeatureFlagSeeder.SeedFeatureFlagsAsync(
            _dbContext!, TestUserId, logger.Object, TestCancellationToken);

        // Assert - Should still have same count
        var flags = await _dbContext!.Set<SystemConfigurationEntity>()
            .Where(c => c.Category == "FeatureFlags")
            .ToListAsync(TestCancellationToken);

        flags.Should().HaveCount(40);
    }

    [Fact]
    public async Task SeedFeatureFlagsAsync_BasicChatEnabledForAllTiers()
    {
        // Arrange
        var logger = new Mock<Microsoft.Extensions.Logging.ILogger>();

        // Act
        await FeatureFlagSeeder.SeedFeatureFlagsAsync(
            _dbContext!, TestUserId, logger.Object, TestCancellationToken);

        // Assert
        var basicChatFlags = await _dbContext!.Set<SystemConfigurationEntity>()
            .Where(c => c.Category == "FeatureFlags" && c.Key.StartsWith("basic_chat"))
            .ToListAsync(TestCancellationToken);

        basicChatFlags.Should().HaveCount(4); // global + 3 tiers

        var freeFlag = basicChatFlags.First(f => f.Key == "basic_chat.Tier.free");
        freeFlag.Value.Should().Be("true");

        var normalFlag = basicChatFlags.First(f => f.Key == "basic_chat.Tier.normal");
        normalFlag.Value.Should().Be("true");

        var premiumFlag = basicChatFlags.First(f => f.Key == "basic_chat.Tier.premium");
        premiumFlag.Value.Should().Be("true");
    }

    [Fact]
    public async Task SeedFeatureFlagsAsync_MultiAgentPremiumOnly()
    {
        // Arrange
        var logger = new Mock<Microsoft.Extensions.Logging.ILogger>();

        // Act
        await FeatureFlagSeeder.SeedFeatureFlagsAsync(
            _dbContext!, TestUserId, logger.Object, TestCancellationToken);

        // Assert
        var multiAgentFlags = await _dbContext!.Set<SystemConfigurationEntity>()
            .Where(c => c.Category == "FeatureFlags" && c.Key.StartsWith("multi_agent"))
            .ToListAsync(TestCancellationToken);

        var freeFlag = multiAgentFlags.First(f => f.Key == "multi_agent.Tier.free");
        freeFlag.Value.Should().Be("false");

        var normalFlag = multiAgentFlags.First(f => f.Key == "multi_agent.Tier.normal");
        normalFlag.Value.Should().Be("false");

        var premiumFlag = multiAgentFlags.First(f => f.Key == "multi_agent.Tier.premium");
        premiumFlag.Value.Should().Be("true");
    }

    [Fact]
    public async Task SeedFeatureFlagsAsync_AllFlagsHaveCorrectCategory()
    {
        // Arrange
        var logger = new Mock<Microsoft.Extensions.Logging.ILogger>();

        // Act
        await FeatureFlagSeeder.SeedFeatureFlagsAsync(
            _dbContext!, TestUserId, logger.Object, TestCancellationToken);

        // Assert
        var flags = await _dbContext!.Set<SystemConfigurationEntity>()
            .Where(c => c.Category == "FeatureFlags")
            .ToListAsync(TestCancellationToken);

        flags.Should().AllSatisfy(f =>
        {
            f.Category.Should().Be("FeatureFlags");
            f.ValueType.Should().Be("Boolean");
            f.IsActive.Should().BeTrue();
            f.RequiresRestart.Should().BeFalse();
            f.Environment.Should().Be("All");
            f.CreatedByUserId.Should().Be(TestUserId);
        });
    }
}
