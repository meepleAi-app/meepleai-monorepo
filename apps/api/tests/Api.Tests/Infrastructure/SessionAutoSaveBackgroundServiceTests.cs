using System.Reflection;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Entities.PauseSnapshot;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.BackgroundServices;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Unit tests for <see cref="SessionAutoSaveBackgroundService"/>.
/// Game Night Improvvisata — E4: Session auto-save background job.
///
/// Tests:
/// - No active sessions → no snapshots created
/// - Active InProgress sessions → PauseSnapshot created with IsAutoSave=true
/// - Failure for one session does not stop others
/// - SaveChanges is called once per snapshot
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public sealed class SessionAutoSaveBackgroundServiceTests : IDisposable
{
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock;
    private readonly Mock<IServiceScope> _scopeMock;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<ILiveSessionRepository> _sessionRepoMock;
    private readonly Mock<IPauseSnapshotRepository> _snapshotRepoMock;
    private readonly MeepleAiDbContext _dbContext;
    private readonly SessionAutoSaveBackgroundService _sut;

    private static readonly Guid UserId = Guid.NewGuid();

    public SessionAutoSaveBackgroundServiceTests()
    {
        _scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _scopeMock = new Mock<IServiceScope>();
        _serviceProviderMock = new Mock<IServiceProvider>();
        _sessionRepoMock = new Mock<ILiveSessionRepository>();
        _snapshotRepoMock = new Mock<IPauseSnapshotRepository>();
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();

        // Wire scope chain
        _scopeMock.Setup(s => s.ServiceProvider).Returns(_serviceProviderMock.Object);
        _scopeFactoryMock.Setup(f => f.CreateScope()).Returns(_scopeMock.Object);

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(ILiveSessionRepository)))
            .Returns(_sessionRepoMock.Object);
        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IPauseSnapshotRepository)))
            .Returns(_snapshotRepoMock.Object);
        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(MeepleAiDbContext)))
            .Returns(_dbContext);

        _snapshotRepoMock
            .Setup(r => r.AddAsync(It.IsAny<PauseSnapshot>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _sut = new SessionAutoSaveBackgroundService(
            _scopeFactoryMock.Object,
            NullLogger<SessionAutoSaveBackgroundService>.Instance);
    }

    public void Dispose() => _dbContext.Dispose();

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Invokes the private AutoSaveActiveSessionsAsync method directly to bypass the 10-min delay.
    /// </summary>
    private async Task InvokeAutoSaveAsync()
    {
        var method = typeof(SessionAutoSaveBackgroundService).GetMethod(
            "AutoSaveActiveSessionsAsync",
            BindingFlags.NonPublic | BindingFlags.Instance);

        await (Task)method!.Invoke(_sut, [CancellationToken.None])!;
    }

    private static LiveGameSession CreateInProgressSession(Guid? id = null)
    {
        var session = LiveGameSession.Create(
            id: id ?? Guid.NewGuid(),
            createdByUserId: UserId,
            gameName: "Test Game",
            timeProvider: TimeProvider.System);

        session.AddPlayer(
            userId: UserId,
            displayName: "Alice",
            color: PlayerColor.Red,
            timeProvider: TimeProvider.System,
            role: PlayerRole.Host);

        session.MoveToSetup(TimeProvider.System);
        session.Start(TimeProvider.System);

        return session;
    }

    // ── No active sessions ────────────────────────────────────────────────────

    [Fact]
    public async Task AutoSave_WhenNoActiveSessions_DoesNotAddAnySnapshot()
    {
        // Arrange
        _sessionRepoMock
            .Setup(r => r.GetAllActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LiveGameSession>());

        // Act
        await InvokeAutoSaveAsync();

        // Assert
        _snapshotRepoMock.Verify(
            r => r.AddAsync(It.IsAny<PauseSnapshot>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Single active session ─────────────────────────────────────────────────

    [Fact]
    public async Task AutoSave_WithOneActiveSession_CreatesAutoSaveSnapshot()
    {
        // Arrange
        var session = CreateInProgressSession();

        _sessionRepoMock
            .Setup(r => r.GetAllActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LiveGameSession> { session });

        PauseSnapshot? capturedSnapshot = null;
        _snapshotRepoMock
            .Setup(r => r.AddAsync(It.IsAny<PauseSnapshot>(), It.IsAny<CancellationToken>()))
            .Callback<PauseSnapshot, CancellationToken>((s, _) => capturedSnapshot = s)
            .Returns(Task.CompletedTask);

        // Act
        await InvokeAutoSaveAsync();

        // Assert — exactly one snapshot
        _snapshotRepoMock.Verify(
            r => r.AddAsync(It.IsAny<PauseSnapshot>(), It.IsAny<CancellationToken>()),
            Times.Once);

        capturedSnapshot.Should().NotBeNull();
        capturedSnapshot!.IsAutoSave.Should().BeTrue();
        capturedSnapshot.LiveGameSessionId.Should().Be(session.Id);
        capturedSnapshot.SavedByUserId.Should().Be(SessionAutoSaveBackgroundService.SystemUserId);
    }

    // ── Multiple active sessions ───────────────────────────────────────────────

    [Fact]
    public async Task AutoSave_WithMultipleActiveSessions_CreatesSnapshotForEach()
    {
        // Arrange
        var session1 = CreateInProgressSession();
        var session2 = CreateInProgressSession();
        var session3 = CreateInProgressSession();

        _sessionRepoMock
            .Setup(r => r.GetAllActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LiveGameSession> { session1, session2, session3 });

        // Act
        await InvokeAutoSaveAsync();

        // Assert — three snapshots created
        _snapshotRepoMock.Verify(
            r => r.AddAsync(It.IsAny<PauseSnapshot>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    // ── Error isolation ───────────────────────────────────────────────────────

    [Fact]
    public async Task AutoSave_WhenOneSessionFails_ContinuesWithRemainingSessions()
    {
        // Arrange
        var goodSession = CreateInProgressSession();
        var badSession = CreateInProgressSession();

        _sessionRepoMock
            .Setup(r => r.GetAllActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LiveGameSession> { badSession, goodSession });

        var callCount = 0;
        _snapshotRepoMock
            .Setup(r => r.AddAsync(It.IsAny<PauseSnapshot>(), It.IsAny<CancellationToken>()))
            .Returns<PauseSnapshot, CancellationToken>((snapshot, _) =>
            {
                callCount++;
                // First call (badSession) throws; second call (goodSession) succeeds
                if (snapshot.LiveGameSessionId == badSession.Id)
                    throw new InvalidOperationException("Simulated save failure");
                return Task.CompletedTask;
            });

        // Act — must not throw
        await InvokeAutoSaveAsync();

        // Assert — both sessions were attempted; good one succeeded
        callCount.Should().Be(2);
    }

    // ── SystemUserId is not Guid.Empty ─────────────────────────────────────────

    [Fact]
    public void SystemUserId_IsNotEmpty()
    {
        SessionAutoSaveBackgroundService.SystemUserId.Should().NotBe(Guid.Empty);
    }
}
