using Api.BoundedContexts.Gamification.Application.Jobs;
using Api.BoundedContexts.Gamification.Application.Services;
using Api.BoundedContexts.Gamification.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.Infrastructure;
using Api.Services;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Gamification.Application.Jobs;

/// <summary>
/// Unit tests for AchievementEvaluationJob constructor guard clauses.
/// Execute method requires a real DbContext (queries Users DbSet) and is covered by integration tests.
/// Issue #3922: Achievement System and Badge Engine.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Gamification")]
public sealed class AchievementEvaluationJobTests
{
    private readonly Mock<IAchievementRepository> _achievementRepoMock = new();
    private readonly Mock<IUserAchievementRepository> _userAchievementRepoMock = new();
    private readonly Mock<IAchievementRuleEvaluator> _ruleEvaluatorMock = new();
    private readonly Mock<INotificationRepository> _notificationRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IHybridCacheService> _cacheMock = new();
    private readonly Mock<ILogger<AchievementEvaluationJob>> _loggerMock = new();

    #region Constructor Null Guard Tests

    [Fact]
    public void Constructor_NullAchievementRepository_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new AchievementEvaluationJob(
            null!,
            _userAchievementRepoMock.Object,
            _ruleEvaluatorMock.Object,
            _notificationRepoMock.Object,
            _unitOfWorkMock.Object,
            _cacheMock.Object,
            CreateInMemoryContext(),
            _loggerMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("achievementRepository");
    }

    [Fact]
    public void Constructor_NullUserAchievementRepository_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new AchievementEvaluationJob(
            _achievementRepoMock.Object,
            null!,
            _ruleEvaluatorMock.Object,
            _notificationRepoMock.Object,
            _unitOfWorkMock.Object,
            _cacheMock.Object,
            CreateInMemoryContext(),
            _loggerMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("userAchievementRepository");
    }

    [Fact]
    public void Constructor_NullRuleEvaluator_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new AchievementEvaluationJob(
            _achievementRepoMock.Object,
            _userAchievementRepoMock.Object,
            null!,
            _notificationRepoMock.Object,
            _unitOfWorkMock.Object,
            _cacheMock.Object,
            CreateInMemoryContext(),
            _loggerMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("ruleEvaluator");
    }

    [Fact]
    public void Constructor_NullNotificationRepository_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new AchievementEvaluationJob(
            _achievementRepoMock.Object,
            _userAchievementRepoMock.Object,
            _ruleEvaluatorMock.Object,
            null!,
            _unitOfWorkMock.Object,
            _cacheMock.Object,
            CreateInMemoryContext(),
            _loggerMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("notificationRepository");
    }

    [Fact]
    public void Constructor_NullUnitOfWork_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new AchievementEvaluationJob(
            _achievementRepoMock.Object,
            _userAchievementRepoMock.Object,
            _ruleEvaluatorMock.Object,
            _notificationRepoMock.Object,
            null!,
            _cacheMock.Object,
            CreateInMemoryContext(),
            _loggerMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("unitOfWork");
    }

    [Fact]
    public void Constructor_NullCache_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new AchievementEvaluationJob(
            _achievementRepoMock.Object,
            _userAchievementRepoMock.Object,
            _ruleEvaluatorMock.Object,
            _notificationRepoMock.Object,
            _unitOfWorkMock.Object,
            null!,
            CreateInMemoryContext(),
            _loggerMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("cache");
    }

    [Fact]
    public void Constructor_NullContext_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new AchievementEvaluationJob(
            _achievementRepoMock.Object,
            _userAchievementRepoMock.Object,
            _ruleEvaluatorMock.Object,
            _notificationRepoMock.Object,
            _unitOfWorkMock.Object,
            _cacheMock.Object,
            null!,
            _loggerMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("context");
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new AchievementEvaluationJob(
            _achievementRepoMock.Object,
            _userAchievementRepoMock.Object,
            _ruleEvaluatorMock.Object,
            _notificationRepoMock.Object,
            _unitOfWorkMock.Object,
            _cacheMock.Object,
            CreateInMemoryContext(),
            null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    #endregion

    #region Execute Tests

    [Fact]
    public async Task Execute_NoActiveAchievements_CompletesSuccessfully()
    {
        // Arrange
        using var context = CreateInMemoryContext();

        _achievementRepoMock.Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Api.BoundedContexts.Gamification.Domain.Entities.Achievement>());

        var job = new AchievementEvaluationJob(
            _achievementRepoMock.Object,
            _userAchievementRepoMock.Object,
            _ruleEvaluatorMock.Object,
            _notificationRepoMock.Object,
            _unitOfWorkMock.Object,
            _cacheMock.Object,
            context,
            _loggerMock.Object);

        var jobContextMock = new Mock<Quartz.IJobExecutionContext>();
        jobContextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        jobContextMock.Setup(c => c.FireTimeUtc).Returns(DateTimeOffset.UtcNow);

        // Act
        await job.Execute(jobContextMock.Object);

        // Assert - should not query users when there are no achievements
        _userAchievementRepoMock.Verify(
            r => r.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region Helpers

    private static MeepleAiDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);
    }

    #endregion
}
