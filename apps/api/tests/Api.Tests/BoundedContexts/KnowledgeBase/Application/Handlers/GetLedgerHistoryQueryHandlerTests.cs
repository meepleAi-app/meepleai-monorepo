using System.Text.Json;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Unit tests for GetLedgerHistoryQueryHandler.
/// Issue #2468 - Ledger Mode Test Suite
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetLedgerHistoryQueryHandlerTests
{
    private readonly Mock<IGameSessionStateRepository> _mockSessionStateRepo;
    private readonly Mock<ILogger<GetLedgerHistoryQueryHandler>> _mockLogger;
    private readonly GetLedgerHistoryQueryHandler _handler;
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public GetLedgerHistoryQueryHandlerTests()
    {
        _mockSessionStateRepo = new Mock<IGameSessionStateRepository>();
        _mockLogger = new Mock<ILogger<GetLedgerHistoryQueryHandler>>();

        _handler = new GetLedgerHistoryQueryHandler(
            _mockSessionStateRepo.Object,
            _mockLogger.Object);
    }

    #region Basic Query Tests

    [Fact]
    public async Task Handle_WithNullQuery_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _handler.Handle(null!, TestCancellationToken));
    }

    [Fact]
    public async Task Handle_WithNonExistentSession_ThrowsInvalidOperationException()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        _mockSessionStateRepo.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameSessionState?)null);

        var query = new GetLedgerHistoryQuery(sessionId, 50);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _handler.Handle(query, TestCancellationToken));

        exception.Message.Should().Contain(sessionId.ToString());
    }

    [Fact]
    public async Task Handle_WithValidSession_ReturnsLedgerHistoryDto()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var sessionState = CreateTestSessionStateWithSnapshots(sessionId, snapshotCount: 3);

        _mockSessionStateRepo.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionState);

        var query = new GetLedgerHistoryQuery(sessionId, 50);

        // Act
        var result = await _handler.Handle(query, TestCancellationToken);

        // Assert
        result.SessionId.Should().Be(sessionId);
        result.Changes.Should().HaveCount(3);
        result.TotalChanges.Should().Be(3);
    }

    #endregion

    #region Limit Tests

    [Fact]
    public async Task Handle_WithLimit_ReturnsLimitedResults()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var sessionState = CreateTestSessionStateWithSnapshots(sessionId, snapshotCount: 10);

        _mockSessionStateRepo.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionState);

        var query = new GetLedgerHistoryQuery(sessionId, Limit: 5);

        // Act
        var result = await _handler.Handle(query, TestCancellationToken);

        // Assert
        result.Changes.Should().HaveCount(5);
        result.TotalChanges.Should().Be(10); // Total in session
    }

    [Fact]
    public async Task Handle_WithLimitGreaterThanSnapshots_ReturnsAllSnapshots()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var sessionState = CreateTestSessionStateWithSnapshots(sessionId, snapshotCount: 3);

        _mockSessionStateRepo.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionState);

        var query = new GetLedgerHistoryQuery(sessionId, Limit: 100);

        // Act
        var result = await _handler.Handle(query, TestCancellationToken);

        // Assert
        result.Changes.Should().HaveCount(3);
    }

    #endregion

    #region Ordering Tests

    [Fact]
    public async Task Handle_ReturnsSnapshotsInDescendingOrder()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var sessionState = CreateTestSessionStateWithSnapshots(sessionId, snapshotCount: 5);

        _mockSessionStateRepo.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionState);

        var query = new GetLedgerHistoryQuery(sessionId, 50);

        // Act
        var result = await _handler.Handle(query, TestCancellationToken);

        // Assert
        var versions = result.Changes.Select(c => c.Version).ToList();
        versions.Should().BeInDescendingOrder();
    }

    #endregion

    #region Empty Results Tests

    [Fact]
    public async Task Handle_WithNoSnapshots_ReturnsEmptyChanges()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var sessionState = CreateTestSessionStateWithSnapshots(sessionId, snapshotCount: 0);

        _mockSessionStateRepo.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionState);

        var query = new GetLedgerHistoryQuery(sessionId, 50);

        // Act
        var result = await _handler.Handle(query, TestCancellationToken);

        // Assert
        result.Changes.Should().BeEmpty();
        result.TotalChanges.Should().Be(0);
    }

    #endregion

    #region DTO Mapping Tests

    [Fact]
    public async Task Handle_MapsSnapshotDataToDto()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var sessionState = CreateTestSessionStateWithSnapshots(sessionId, snapshotCount: 1);

        _mockSessionStateRepo.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionState);

        var query = new GetLedgerHistoryQuery(sessionId, 50);

        // Act
        var result = await _handler.Handle(query, TestCancellationToken);

        // Assert
        var change = result.Changes.First();
        change.Version.Should().BeGreaterThanOrEqualTo(1);
        change.Changes.Should().ContainKey("snapshot");
        change.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(5));
    }

    [Fact]
    public async Task Handle_ReturnsCurrentVersion()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var sessionState = CreateTestSessionStateWithSnapshots(sessionId, snapshotCount: 5);

        _mockSessionStateRepo.Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionState);

        var query = new GetLedgerHistoryQuery(sessionId, 50);

        // Act
        var result = await _handler.Handle(query, TestCancellationToken);

        // Assert
        result.CurrentVersion.Should().BeGreaterThanOrEqualTo(0);
    }

    #endregion

    #region Helper Methods

    private static GameSessionState CreateTestSessionStateWithSnapshots(Guid sessionId, int snapshotCount)
    {
        var initialState = JsonDocument.Parse("""{"score": 0}""");
        var sessionState = GameSessionState.Create(
            id: Guid.NewGuid(),
            gameSessionId: sessionId,
            templateId: Guid.NewGuid(),
            initialState: initialState,
            createdBy: "test-user");

        // Add snapshots
        for (int i = 1; i <= snapshotCount; i++)
        {
            sessionState.CreateSnapshot(
                turnNumber: i,
                description: $"Turn {i} snapshot",
                createdBy: "test-user");
        }

        return sessionState;
    }

    #endregion
}
