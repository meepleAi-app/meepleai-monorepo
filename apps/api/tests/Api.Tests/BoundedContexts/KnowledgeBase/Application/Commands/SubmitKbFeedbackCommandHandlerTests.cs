using Api.BoundedContexts.KnowledgeBase.Application.Commands.SubmitKbFeedback;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Tests.Constants;
using NSubstitute;
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
    private readonly IKbUserFeedbackRepository _repo;
    private readonly SubmitKbFeedbackCommandHandler _sut;

    public SubmitKbFeedbackCommandHandlerTests()
    {
        _repo = Substitute.For<IKbUserFeedbackRepository>();
        _sut = new SubmitKbFeedbackCommandHandler(_repo);
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
        await _repo.Received(1).AddAsync(
            Arg.Is<KbUserFeedback>(f => f.Outcome == "helpful" && f.GameId == cmd.GameId),
            Arg.Any<CancellationToken>());
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
        await _repo.Received(1).AddAsync(
            Arg.Any<KbUserFeedback>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithComment_ThrowsIfOver500Chars()
    {
        // Arrange
        var longComment = new string('x', 600);
        var cmd = new SubmitKbFeedbackCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "helpful", longComment);

        // Act
        var act = () => _sut.Handle(cmd, CancellationToken.None);

        // Assert — domain factory must throw, not silently truncate
        await Assert.ThrowsAsync<ArgumentException>(act);
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
