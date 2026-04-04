using Api.BoundedContexts.GameManagement.Domain.Entities.PauseSnapshot;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Application.EventHandlers;
using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.EventHandlers;

/// <summary>
/// Unit tests for <see cref="UpdateSnapshotSummaryHandler"/>.
/// Game Night Improvvisata — E4: persisting AI summary to PauseSnapshot.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class UpdateSnapshotSummaryHandlerTests
{
    // ─── Fixtures ─────────────────────────────────────────────────────────────

    private static readonly Guid TestSnapshotId = Guid.NewGuid();
    private static readonly Guid TestSessionId = Guid.NewGuid();
    private static readonly Guid TestUserId = Guid.NewGuid();

    private const string TestSummary = "Quando avete messo in pausa, turno 4 era in corso.";

    private readonly Mock<IPauseSnapshotRepository> _snapshotRepoMock;
    private readonly UpdateSnapshotSummaryHandler _sut;

    public UpdateSnapshotSummaryHandlerTests()
    {
        _snapshotRepoMock = new Mock<IPauseSnapshotRepository>();

        _snapshotRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<PauseSnapshot>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _sut = new UpdateSnapshotSummaryHandler(
            _snapshotRepoMock.Object,
            NullLogger<UpdateSnapshotSummaryHandler>.Instance);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private PauseSnapshot CreateSnapshot(Guid? id = null)
    {
        var snapshot = PauseSnapshot.Create(
            liveGameSessionId: TestSessionId,
            currentTurn: 4,
            currentPhase: null,
            playerScores: new List<PlayerScoreSnapshot>(),
            savedByUserId: TestUserId,
            isAutoSave: false);

        var snapshotId = id ?? TestSnapshotId;

        // Restore ID via reflection (same pattern used by repository)
        var prop = typeof(PauseSnapshot).BaseType?.GetProperty("Id",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        prop?.SetValue(snapshot, snapshotId);

        _snapshotRepoMock
            .Setup(r => r.GetByIdAsync(snapshotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        return snapshot;
    }

    private static AgentSummaryGeneratedEvent BuildEvent(
        Guid? snapshotId = null,
        string? summary = null)
        => new AgentSummaryGeneratedEvent(
            pauseSnapshotId: snapshotId ?? TestSnapshotId,
            summary: summary ?? TestSummary);

    // ─── Happy path ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_HappyPath_UpdatesSnapshotWithSummary()
    {
        // Arrange
        var snapshot = CreateSnapshot();
        var notification = BuildEvent();

        PauseSnapshot? capturedSnapshot = null;
        _snapshotRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<PauseSnapshot>(), It.IsAny<CancellationToken>()))
            .Callback<PauseSnapshot, CancellationToken>((s, _) => capturedSnapshot = s)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.Handle(notification, CancellationToken.None);

        // Assert
        capturedSnapshot.Should().NotBeNull();
        capturedSnapshot!.AgentConversationSummary.Should().Be(TestSummary);
    }

    [Fact]
    public async Task Handle_HappyPath_CallsRepositoryUpdate()
    {
        // Arrange
        CreateSnapshot();
        var notification = BuildEvent();

        // Act
        await _sut.Handle(notification, CancellationToken.None);

        // Assert
        _snapshotRepoMock.Verify(
            r => r.UpdateAsync(It.IsAny<PauseSnapshot>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ─── Edge cases ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenSnapshotNotFound_DoesNotThrowAndSkipsUpdate()
    {
        // Arrange — repository returns null
        _snapshotRepoMock
            .Setup(r => r.GetByIdAsync(TestSnapshotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PauseSnapshot?)null);

        var notification = BuildEvent();

        // Act — should not throw
        var exception = await Record.ExceptionAsync(
            () => _sut.Handle(notification, CancellationToken.None));

        // Assert
        exception.Should().BeNull();

        // UpdateAsync should NOT be called when snapshot was not found
        _snapshotRepoMock.Verify(
            r => r.UpdateAsync(It.IsAny<PauseSnapshot>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
