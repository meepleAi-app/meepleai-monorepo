using Api.BoundedContexts.KnowledgeBase.Application.Commands.SubmitKbFeedback;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Unit tests for SubmitKbFeedbackCommandHandler.
/// KB-06: User Feedback on Chat Response.
/// #4168 (ADR-095 D2): il feedback valuta una risposta salvata nel thread dell'utente e ne prende la
/// versione del profilo e la lingua; un id che non porta a una risposta sua è un 404.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class SubmitKbFeedbackCommandHandlerTests
{
    private static readonly Guid VersionId = Guid.NewGuid();

    private readonly IKbUserFeedbackRepository _repo = Substitute.For<IKbUserFeedbackRepository>();
    private readonly IChatThreadRepository _threads = Substitute.For<IChatThreadRepository>();
    private readonly SubmitKbFeedbackCommandHandler _sut;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly ChatThread _thread;

    public SubmitKbFeedbackCommandHandlerTests()
    {
        _sut = new SubmitKbFeedbackCommandHandler(_repo, _threads);
        _thread = new ChatThread(Guid.NewGuid(), _userId, _gameId);
        _thread.AddUserMessage("How many cards?");
        _thread.AddAssistantMessage("Five cards.", VersionId, "it");
        _threads.GetByIdAsync(_thread.Id, Arg.Any<CancellationToken>()).Returns(_thread);
    }

    private Guid AnswerId => _thread.Messages.Single(m => m.IsAssistantMessage).Id;

    private SubmitKbFeedbackCommand Feedback(Guid? messageId = null, string outcome = "helpful", string? comment = null,
        Guid? userId = null, Guid? gameId = null) =>
        new(userId ?? _userId, gameId ?? _gameId, _thread.Id, messageId ?? AnswerId, outcome, comment);

    [Fact]
    public async Task Handle_AnswerInTheUsersThread_RecordsItsVersionAndLanguage()
    {
        await _sut.Handle(Feedback(), CancellationToken.None);

        await _repo.Received(1).AddAsync(
            Arg.Is<KbUserFeedback>(f =>
                f.Outcome == "helpful"
                && f.GameId == _gameId
                && f.MessageId == AnswerId
                && f.ProfileVersionId == VersionId
                && f.Language == "it"),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("helpful")]
    [InlineData("not_helpful")]
    public async Task Handle_BothValidOutcomes_DoNotThrow(string outcome)
    {
        await _sut.Handle(Feedback(outcome: outcome), CancellationToken.None);

        await _repo.Received(1).AddAsync(Arg.Any<KbUserFeedback>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithComment_ThrowsIfOver500Chars()
    {
        var act = () => _sut.Handle(Feedback(comment: new string('x', 600)), CancellationToken.None);

        // domain factory must throw, not silently truncate
        await Assert.ThrowsAsync<ArgumentException>(act);
    }

    [Fact]
    public async Task Handle_NullCommand_ThrowsArgumentNullException()
    {
        var act = () => _sut.Handle(null!, CancellationToken.None);

        await Assert.ThrowsAsync<ArgumentNullException>(act);
    }

    [Fact]
    public async Task Handle_UnknownThread_IsNotFound()
    {
        var command = Feedback() with { ChatSessionId = Guid.NewGuid() };

        await AssertNotFoundAsync(command);
    }

    [Fact]
    public async Task Handle_ThreadOfAnotherUser_IsNotFound() =>
        await AssertNotFoundAsync(Feedback(userId: Guid.NewGuid()));

    [Fact]
    public async Task Handle_ThreadOfAnotherGame_IsNotFound() =>
        await AssertNotFoundAsync(Feedback(gameId: Guid.NewGuid()));

    [Fact]
    public async Task Handle_MessageNotInTheThread_IsNotFound() =>
        await AssertNotFoundAsync(Feedback(messageId: Guid.NewGuid()));

    [Fact]
    public async Task Handle_UserMessage_IsNotFound() =>
        await AssertNotFoundAsync(Feedback(messageId: _thread.Messages.Single(m => m.IsUserMessage).Id));

    private async Task AssertNotFoundAsync(SubmitKbFeedbackCommand command)
    {
        var act = () => _sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        await _repo.DidNotReceive().AddAsync(Arg.Any<KbUserFeedback>(), Arg.Any<CancellationToken>());
    }
}
