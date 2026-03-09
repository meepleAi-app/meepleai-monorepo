using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.Services;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.GameManagement.Infrastructure.Services;
using Api.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.Unit.GameManagement;

/// <summary>
/// Unit tests for SessionQuotaService.EnsureQuotaAsync method.
/// Issue #3671: Session limits enforcement with automatic termination.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "GameManagement")]
public sealed class SessionQuotaServiceEnsureQuotaTests : IDisposable
{
    private readonly Mock<IGameSessionRepository> _mockRepository;
    private readonly Mock<IConfigurationService> _mockConfigService;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ILogger<SessionQuotaService>> _mockLogger;
    private readonly SessionQuotaService _sut;

    public SessionQuotaServiceEnsureQuotaTests()
    {
        _mockRepository = new Mock<IGameSessionRepository>();
        _mockConfigService = new Mock<IConfigurationService>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockLogger = new Mock<ILogger<SessionQuotaService>>();

        _sut = new SessionQuotaService(
            _mockRepository.Object,
            _mockConfigService.Object,
            _mockUnitOfWork.Object,
            _mockLogger.Object);
    }

    public void Dispose()
    {
        _mockRepository.Reset();
        _mockConfigService.Reset();
        _mockUnitOfWork.Reset();
    }

    [Fact]
    public async Task EnsureQuotaAsync_QuotaAvailable_ReturnsSuccessWithoutTerminations()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userTier = UserTier.Free;
        var userRole = Role.User;

        // Free tier: 3 max, currently has 2 active
        _mockConfigService.Setup(x => x.GetValueAsync<int?>("SessionLimits:free:MaxSessions", null, default))
            .ReturnsAsync((int?)null); // Use defaults

        _mockRepository.Setup(x => x.CountActiveByUserIdAsync(userId, default))
            .ReturnsAsync(2);

        // Act
        var result = await _sut.EnsureQuotaAsync(userId, userTier, userRole);

        // Assert
        Assert.True(result.QuotaAvailable);
        Assert.Empty(result.TerminatedSessionIds);
        Assert.Null(result.Message);

        _mockRepository.Verify(x => x.FindOldestActiveByUserIdAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockRepository.Verify(x => x.UpdateAsync(It.IsAny<GameSession>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnsureQuotaAsync_QuotaExceeded_TerminatesOldestSession()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userTier = UserTier.Free;
        var userRole = Role.User;

        // Free tier: 3 max, currently has 3 active (at limit)
        _mockConfigService.Setup(x => x.GetValueAsync<int?>("SessionLimits:free:MaxSessions", null, default))
            .ReturnsAsync((int?)null);

        _mockRepository.Setup(x => x.CountActiveByUserIdAsync(userId, default))
            .ReturnsAsync(3);

        var oldestSession = CreateMockSession(userId, Guid.NewGuid(), DateTime.UtcNow.AddHours(-5));
        _mockRepository.Setup(x => x.FindOldestActiveByUserIdAsync(userId, 1, default))
            .ReturnsAsync(new List<GameSession> { oldestSession });

        _mockRepository.Setup(x => x.UpdateAsync(It.IsAny<GameSession>(), default))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.EnsureQuotaAsync(userId, userTier, userRole);

        // Assert
        Assert.True(result.QuotaAvailable);
        Assert.Single(result.TerminatedSessionIds);
        Assert.Contains(oldestSession.Id, result.TerminatedSessionIds);
        Assert.NotNull(result.Message);
        Assert.Contains("Terminated 1", result.Message);

        _mockRepository.Verify(x => x.FindOldestActiveByUserIdAsync(userId, 1, default), Times.Once);
        _mockRepository.Verify(x => x.UpdateAsync(It.Is<GameSession>(s => s.Id == oldestSession.Id), default), Times.Once);
    }

    [Fact]
    public async Task EnsureQuotaAsync_QuotaExceeded_TerminatesMultipleSessions()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userTier = UserTier.Free;
        var userRole = Role.User;

        // Free tier: 3 max, currently has 5 active (2 over limit)
        _mockConfigService.Setup(x => x.GetValueAsync<int?>("SessionLimits:free:MaxSessions", null, default))
            .ReturnsAsync((int?)null);

        _mockRepository.Setup(x => x.CountActiveByUserIdAsync(userId, default))
            .ReturnsAsync(5);

        var session1 = CreateMockSession(userId, Guid.NewGuid(), DateTime.UtcNow.AddHours(-10));
        var session2 = CreateMockSession(userId, Guid.NewGuid(), DateTime.UtcNow.AddHours(-8));
        var session3 = CreateMockSession(userId, Guid.NewGuid(), DateTime.UtcNow.AddHours(-6));

        _mockRepository.Setup(x => x.FindOldestActiveByUserIdAsync(userId, 3, default))
            .ReturnsAsync(new List<GameSession> { session1, session2, session3 });

        // Act
        var result = await _sut.EnsureQuotaAsync(userId, userTier, userRole);

        // Assert
        Assert.True(result.QuotaAvailable);
        Assert.Equal(3, result.TerminatedSessionIds.Count);
        Assert.Contains(session1.Id, result.TerminatedSessionIds);
        Assert.Contains(session2.Id, result.TerminatedSessionIds);
        Assert.Contains(session3.Id, result.TerminatedSessionIds);

        _mockRepository.Verify(x => x.UpdateAsync(It.IsAny<GameSession>(), default), Times.Exactly(3));
    }

    [Fact]
    public async Task EnsureQuotaAsync_AdminRole_ReturnsSuccessWithoutChecking()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userTier = UserTier.Free;
        var userRole = Role.Admin;

        // Act
        var result = await _sut.EnsureQuotaAsync(userId, userTier, userRole);

        // Assert
        Assert.True(result.QuotaAvailable);
        Assert.Empty(result.TerminatedSessionIds);

        // Should not count or find sessions for admin
        _mockRepository.Verify(x => x.CountActiveByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockRepository.Verify(x => x.FindOldestActiveByUserIdAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnsureQuotaAsync_EditorRole_ReturnsSuccessWithoutChecking()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userTier = UserTier.Normal;
        var userRole = Role.Editor;

        // Act
        var result = await _sut.EnsureQuotaAsync(userId, userTier, userRole);

        // Assert
        Assert.True(result.QuotaAvailable);
        Assert.Empty(result.TerminatedSessionIds);

        _mockRepository.Verify(x => x.CountActiveByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnsureQuotaAsync_PremiumTier_ReturnsSuccessWithoutTerminations()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userTier = UserTier.Premium;
        var userRole = Role.User;

        _mockRepository.Setup(x => x.CountActiveByUserIdAsync(userId, default))
            .ReturnsAsync(100); // Many sessions, but premium = unlimited

        // Act
        var result = await _sut.EnsureQuotaAsync(userId, userTier, userRole);

        // Assert
        Assert.True(result.QuotaAvailable);
        Assert.Empty(result.TerminatedSessionIds);

        _mockRepository.Verify(x => x.FindOldestActiveByUserIdAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnsureQuotaAsync_NormalTierAtLimit_TerminatesOldest()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userTier = UserTier.Normal;
        var userRole = Role.User;

        // Normal tier: 10 max, currently has 10 active
        _mockConfigService.Setup(x => x.GetValueAsync<int?>("SessionLimits:normal:MaxSessions", null, default))
            .ReturnsAsync((int?)null);

        _mockRepository.Setup(x => x.CountActiveByUserIdAsync(userId, default))
            .ReturnsAsync(10);

        var oldestSession = CreateMockSession(userId, Guid.NewGuid(), DateTime.UtcNow.AddDays(-2));
        _mockRepository.Setup(x => x.FindOldestActiveByUserIdAsync(userId, 1, default))
            .ReturnsAsync(new List<GameSession> { oldestSession });

        // Act
        var result = await _sut.EnsureQuotaAsync(userId, userTier, userRole);

        // Assert
        Assert.True(result.QuotaAvailable);
        Assert.Single(result.TerminatedSessionIds);
        Assert.Equal(oldestSession.Id, result.TerminatedSessionIds[0]);
    }

    [Fact]
    public async Task EnsureQuotaAsync_CustomConfiguredLimit_UsesConfigValue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userTier = UserTier.Free;
        var userRole = Role.User;

        // Custom configured limit: 5 (instead of default 3)
        _mockConfigService.Setup(x => x.GetValueAsync<int?>("SessionLimits:free:MaxSessions", null, default))
            .ReturnsAsync(5);

        _mockRepository.Setup(x => x.CountActiveByUserIdAsync(userId, default))
            .ReturnsAsync(4); // Under custom limit

        // Act
        var result = await _sut.EnsureQuotaAsync(userId, userTier, userRole);

        // Assert
        Assert.True(result.QuotaAvailable);
        Assert.Empty(result.TerminatedSessionIds);
    }

    /// <summary>
    /// Helper method to create a mock GameSession for testing.
    /// Uses reflection to bypass private constructor.
    /// </summary>
    private static GameSession CreateMockSession(Guid userId, Guid sessionId, DateTime startedAt)
    {
        var players = new List<SessionPlayer>
        {
            new SessionPlayer("TestPlayer1", 100)
        };

        var session = new GameSession(sessionId, Guid.NewGuid(), players, userId);

        // Set StartedAt via reflection (for testing oldest session logic)
        var startedAtProp = typeof(GameSession).GetProperty("StartedAt");
        startedAtProp?.SetValue(session, startedAt);

        return session;
    }
}
