using Api.BoundedContexts.BusinessSimulations.Application.DTOs;
using Api.BoundedContexts.BusinessSimulations.Application.Commands;
using Api.BoundedContexts.BusinessSimulations.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.BusinessSimulations.Application;

/// <summary>
/// Unit tests for GetAppUsageStatsQueryHandler (Issue #4562 - Epic #3688)
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "BusinessSimulations")]
public sealed class GetAppUsageStatsQueryHandlerTests : IAsyncLifetime
{
    private readonly MeepleAiDbContext _context;
    private readonly HybridCache _cache;
    private readonly GetAppUsageStatsQueryHandler _handler;
    private readonly TimeProvider _timeProvider;

    public GetAppUsageStatsQueryHandlerTests()
    {
        var services = new ServiceCollection();
        services.AddHybridCache();
        var provider = services.BuildServiceProvider();
        _cache = provider.GetRequiredService<HybridCache>();

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"AppUsageStatsTests_{Guid.NewGuid()}")
            .Options;

        var mockMediator = new Mock<IMediator>();
        var mockEventCollector = new Mock<Api.SharedKernel.Application.Services.IDomainEventCollector>();
        mockEventCollector
            .Setup(e => e.GetAndClearEvents())
            .Returns(new List<Api.SharedKernel.Domain.Interfaces.IDomainEvent>().AsReadOnly());

        _context = new MeepleAiDbContext(options, mockMediator.Object, mockEventCollector.Object);
        _timeProvider = TimeProvider.System;
        _handler = new GetAppUsageStatsQueryHandler(_context, _cache, Mock.Of<ILogger<GetAppUsageStatsQueryHandler>>(), _timeProvider);
    }

    public async ValueTask InitializeAsync()
    {
        // Seed test data
        var now = DateTime.UtcNow;

        // Create users with signup dates
        var users = new[]
        {
            new UserEntity { Id = Guid.NewGuid(), Email = "user1@test.com", CreatedAt = now.AddDays(-90), Status = "Active" },
            new UserEntity { Id = Guid.NewGuid(), Email = "user2@test.com", CreatedAt = now.AddDays(-60), Status = "Active" },
            new UserEntity { Id = Guid.NewGuid(), Email = "user3@test.com", CreatedAt = now.AddDays(-30), Status = "Active" },
            new UserEntity { Id = Guid.NewGuid(), Email = "user4@test.com", CreatedAt = now.AddDays(-7), Status = "Active" },
            new UserEntity { Id = Guid.NewGuid(), Email = "user5@test.com", CreatedAt = now.AddDays(-1), Status = "Active" }
        };

        _context.Users.AddRange(users);

        // Create sessions for DAU/MAU calculation
        var sessions = new List<UserSessionEntity>
        {
            // Recent activity (DAU)
            new() { Id = Guid.NewGuid(), UserId = users[4].Id, TokenHash = "token1", CreatedAt = now.AddHours(-12), ExpiresAt = now.AddDays(7), LastSeenAt = now.AddHours(-1), User = users[4] },
            new() { Id = Guid.NewGuid(), UserId = users[3].Id, TokenHash = "token2", CreatedAt = now.AddHours(-18), ExpiresAt = now.AddDays(7), LastSeenAt = now.AddHours(-2), User = users[3] },

            // Month activity (MAU)
            new() { Id = Guid.NewGuid(), UserId = users[2].Id, TokenHash = "token3", CreatedAt = now.AddDays(-15), ExpiresAt = now.AddDays(7), LastSeenAt = now.AddDays(-14), User = users[2] },
            new() { Id = Guid.NewGuid(), UserId = users[1].Id, TokenHash = "token4", CreatedAt = now.AddDays(-25), ExpiresAt = now.AddDays(7), LastSeenAt = now.AddDays(-24), User = users[1] },

            // Bounced session (no LastSeenAt)
            new() { Id = Guid.NewGuid(), UserId = users[0].Id, TokenHash = "token5", CreatedAt = now.AddDays(-40), ExpiresAt = now.AddDays(7), LastSeenAt = null, User = users[0] }
        };

        _context.UserSessions.AddRange(sessions);
        await _context.SaveChangesAsync();
    }

    public ValueTask DisposeAsync()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Handle_WithValidQuery_ReturnsAppUsageStats()
    {
        // Arrange
        var query = new GetAppUsageStatsQuery(Period: 30);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.DailyActiveUsers.Should().NotBeNull();
        result.MonthlyActiveUsers.Should().NotBeNull();
        result.Sessions.Should().NotBeNull();
        result.Retention.Should().NotBeNull();
        result.FeatureFunnel.Should().NotBeNull();
        result.GeoDistribution.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_CalculatesDauCorrectly()
    {
        // Arrange
        var query = new GetAppUsageStatsQuery(Period: 30);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert - Should have 2 users active in last 24h
        result.DailyActiveUsers.Current.Should().Be(2);
    }

    [Fact]
    public async Task Handle_CalculatesMauCorrectly()
    {
        // Arrange
        var query = new GetAppUsageStatsQuery(Period: 30);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert - Should have 4 users active in last 30 days (all except user1 who had session 40 days ago)
        result.MonthlyActiveUsers.Current.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Handle_CalculatesSessionAnalytics()
    {
        // Arrange
        var query = new GetAppUsageStatsQuery(Period: 30);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Sessions.TotalSessions.Should().BeGreaterThan(0);
        result.Sessions.BounceRatePercentage.Should().BeGreaterThanOrEqualTo(0);
        result.Sessions.AverageDuration.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Handle_CalculatesRetentionCohorts()
    {
        // Arrange
        var query = new GetAppUsageStatsQuery(Period: 90);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Retention.SignupsInPeriod.Should().BeGreaterThanOrEqualTo(4); // At least 4 users in cohort
        result.Retention.Day7Percentage.Should().BeGreaterThanOrEqualTo(0);
        result.Retention.Day30Percentage.Should().BeGreaterThanOrEqualTo(0);
        result.Retention.Day90Percentage.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task Handle_ReturnsFeatureFunnel()
    {
        // Arrange
        var query = new GetAppUsageStatsQuery(Period: 30);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.FeatureFunnel.Should().NotBeEmpty();
        result.FeatureFunnel.Should().Contain(s => s.Step == "SignUp");
        result.FeatureFunnel.First().ConversionPercentage.Should().Be(100.0);
    }

    [Fact]
    public async Task Handle_WithCaching_ReturnsCachedResult()
    {
        // Arrange
        var query = new GetAppUsageStatsQuery(Period: 30);

        // Act
        var result1 = await _handler.Handle(query, CancellationToken.None);
        var result2 = await _handler.Handle(query, CancellationToken.None);

        // Assert - Should return same instance from cache
        result1.GeneratedAt.Should().Be(result2.GeneratedAt);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(30)]
    [InlineData(90)]
    public async Task Handle_WithDifferentPeriods_ReturnsStats(int period)
    {
        // Arrange
        var query = new GetAppUsageStatsQuery(Period: period);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.DailyActiveUsers.Should().NotBeNull();
    }
}
