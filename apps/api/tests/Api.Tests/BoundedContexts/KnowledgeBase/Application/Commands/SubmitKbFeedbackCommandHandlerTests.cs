using Api.BoundedContexts.KnowledgeBase.Application.Commands.SubmitKbFeedback;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Unit tests for SubmitKbFeedbackCommandHandler.
/// KB-06: User Feedback on Chat Response.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class SubmitKbFeedbackCommandHandlerTests
{
    private readonly Mock<IKbUserFeedbackRepository> _repoMock;
    private readonly SubmitKbFeedbackCommandHandler _sut;

    public SubmitKbFeedbackCommandHandlerTests()
    {
        _repoMock = new Mock<IKbUserFeedbackRepository>();
        _sut = new SubmitKbFeedbackCommandHandler(_repoMock.Object);
    }

    [Fact]
    public async Task Handle_ValidFeedback_CallsAddAsync()
    {
        // Arrange
        var cmd = new SubmitKbFeedbackCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "helpful", null);

        // Act
        await _sut.Handle(cmd, CancellationToken.None);

        // Assert
        _repoMock.Verify(r => r.AddAsync(
            It.Is<KbUserFeedback>(f => f.Outcome == "helpful" && f.GameId == cmd.GameId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("helpful")]
    [InlineData("not_helpful")]
    public async Task Handle_BothValidOutcomes_DoNotThrow(string outcome)
    {
        // Arrange
        var cmd = new SubmitKbFeedbackCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), outcome, null);

        // Act
        await _sut.Handle(cmd, CancellationToken.None);

        // Assert
        _repoMock.Verify(r => r.AddAsync(
            It.IsAny<KbUserFeedback>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithComment_TruncatesIfOver500Chars()
    {
        // Arrange
        var longComment = new string('x', 600);
        var cmd = new SubmitKbFeedbackCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "helpful", longComment);

        // Act
        await _sut.Handle(cmd, CancellationToken.None);

        // Assert
        _repoMock.Verify(r => r.AddAsync(
            It.Is<KbUserFeedback>(f => f.Comment != null && f.Comment.Length == 500),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NullCommand_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _sut.Handle(null!, CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<ArgumentNullException>(act);
    }
}
