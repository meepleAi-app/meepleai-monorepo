using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Tests.Constants;
using MediatR;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Tests for ConfirmScoreCommandHandler.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class ConfirmScoreCommandHandlerTests
{
    private readonly Mock<IMediator> _mockMediator;
    private readonly ConfirmScoreCommandHandler _handler;

    public ConfirmScoreCommandHandlerTests()
    {
        _mockMediator = new Mock<IMediator>();
        _handler = new ConfirmScoreCommandHandler(_mockMediator.Object);
    }

    [Fact]
    public async Task Handle_ValidCommand_DelegatesToRecordLiveSessionScoreCommand()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var command = new ConfirmScoreCommand
        {
            SessionId = sessionId,
            PlayerId = playerId,
            Dimension = "points",
            Value = 10,
            Round = 2
        };

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockMediator.Verify(m => m.Send(
            It.Is<RecordLiveSessionScoreCommand>(c =>
                c.SessionId == sessionId &&
                c.PlayerId == playerId &&
                c.Dimension == "points" &&
                c.Value == 10 &&
                c.Round == 2),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_MediatorThrows_PropagatesException()
    {
        // Arrange
        var command = new ConfirmScoreCommand
        {
            SessionId = Guid.NewGuid(),
            PlayerId = Guid.NewGuid(),
            Dimension = "points",
            Value = 5,
            Round = 1
        };

        _mockMediator
            .Setup(m => m.Send(It.IsAny<RecordLiveSessionScoreCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Session not found"));

        // Act & Assert
        Func<Task> act = () => _handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
