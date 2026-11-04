using Api.BoundedContexts.KnowledgeBase.Application.Services.Answering;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Events;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.BoundedContexts.SessionTracking.Infrastructure.Services;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.Services.ImageProcessing;
using Api.Services.LlmClients;
using Api.SharedKernel.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Handlers;

/// <summary>
/// #4837 — <c>POST /game-sessions/{id}/chat/ask-agent</c> asked only for "an authenticated user". The question is
/// saved in the table's chat AS the participant <c>SenderId</c> and the answer is published to everyone at the table,
/// and a participant id is readable by anyone sitting at it: whoever knew a (session, participant) pair of somebody
/// else's session could write into their chat. The sibling that sends a message has had the check since #3756.
///
/// The tests run the handler with the REAL <see cref="SessionAccessGuard"/> over a real <see cref="Session"/>, so
/// they are the attack and not a statement about what the handler calls: a caller who is nobody at the table, with the
/// sender chosen from the table's participants.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
[Trait("Issue", "4837")]
[Collection("AgentGroundingMetrics")]
public sealed class AskSessionAgentAccessGuardTests
{
    private readonly Guid _sessionId = Guid.NewGuid();
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _registeredUserId = Guid.NewGuid();
    private readonly Guid _registeredParticipantId = Guid.NewGuid();
    private readonly Guid _guestParticipantId = Guid.NewGuid();

    private readonly Mock<ISessionRepository> _sessions = new();
    private readonly Mock<ISessionChatRepository> _chat = new();
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<ILlmService> _llm = new();
    private readonly Mock<IGameStateExtractor> _extractor = new();

    public AskSessionAgentAccessGuardTests()
    {
        var session = Session.Create(_ownerId, Guid.NewGuid(), SessionType.GameSpecific);
        typeof(Session).GetProperty("Id")!.SetValue(session, _sessionId);
        var participants = (List<Participant>)typeof(Session)
            .GetField("_participants", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(session)!;
        participants.Add(new Participant { Id = _registeredParticipantId, SessionId = _sessionId, UserId = _registeredUserId });
        participants.Add(new Participant { Id = _guestParticipantId, SessionId = _sessionId, UserId = null });

        _sessions.Setup(r => r.GetByIdAsync(_sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(session);
        _llm.Setup(s => s.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess("Test agent response"));
    }

    private AskSessionAgentCommandHandler CreateHandler(ISessionAccessGuard guard) =>
        new(_sessions.Object,
            _chat.Object,
            _mediator.Object,
            _llm.Object,
            new Mock<IImagePreprocessor>().Object,
            _extractor.Object,
            new Mock<IAnswerPreconditions>().Object,
            guard,
            new Mock<ILogger<AskSessionAgentCommandHandler>>().Object);

    private AskSessionAgentCommandHandler HandlerWithTheRealGuard() =>
        CreateHandler(new SessionAccessGuard(_sessions.Object));

    private AskSessionAgentCommand Ask(Guid senderId, Guid requestedBy) =>
        new(_sessionId, senderId, "Come si conta il punteggio?", 1, RequestedBy: requestedBy);

    private void NothingWasSavedPaidOrPublished()
    {
        _chat.Verify(r => r.AddAsync(It.IsAny<SessionChatMessage>(), It.IsAny<CancellationToken>()), Times.Never);
        _chat.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _llm.Verify(s => s.GenerateCompletionAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()), Times.Never);
        _extractor.Verify(e => e.ExtractIfNeededAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        _mediator.Verify(m => m.Publish(It.IsAny<SessionChatMessageSentEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── The attack ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ACallerWhoIsNotAtTheTable_WritingAsARealParticipant_IsRefused_AndNothingHappens()
    {
        var stranger = Guid.NewGuid();

        var act = () => HandlerWithTheRealGuard().Handle(
            Ask(senderId: _registeredParticipantId, requestedBy: stranger), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ForbiddenException>();
        NothingWasSavedPaidOrPublished();
    }

    [Fact]
    public async Task Handle_ACallerWhoIsNotAtTheTable_WritingAsAGuest_IsRefused_AndNothingHappens()
    {
        var act = () => HandlerWithTheRealGuard().Handle(
            Ask(senderId: _guestParticipantId, requestedBy: Guid.NewGuid()), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ForbiddenException>();
        NothingWasSavedPaidOrPublished();
    }

    [Fact]
    public async Task Handle_WithNoCaller_IsRefused_NotTakenForSomebodyAtTheTable()
    {
        // RequestedBy is Guid.Empty when an endpoint forgets to take it from the principal; a guest participant has a
        // null UserId, so an empty id must not match anything.
        var act = () => HandlerWithTheRealGuard().Handle(
            Ask(senderId: _guestParticipantId, requestedBy: Guid.Empty), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ForbiddenException>();
        NothingWasSavedPaidOrPublished();
    }

    // ── Who is let through ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ARegisteredParticipant_Asking_Passes()
    {
        var result = await HandlerWithTheRealGuard().Handle(
            Ask(senderId: _registeredParticipantId, requestedBy: _registeredUserId), TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        _chat.Verify(r => r.AddAsync(It.IsAny<SessionChatMessage>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task Handle_TheOwner_WritingForAGuestWhoHasNoAccount_Passes()
    {
        // The host writes from their phone for who has no account: the check is on the caller, not on the sender.
        var result = await HandlerWithTheRealGuard().Handle(
            Ask(senderId: _guestParticipantId, requestedBy: _ownerId), TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        _chat.Verify(r => r.AddAsync(It.IsAny<SessionChatMessage>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    // ── What the guard is asked, and when ───────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_TheGuardIsAskedAboutTheCaller_NeverAboutTheParticipantTheyWriteAs()
    {
        var guard = new Mock<ISessionAccessGuard>();
        var caller = Guid.NewGuid();

        await CreateHandler(guard.Object).Handle(
            Ask(senderId: _registeredParticipantId, requestedBy: caller), TestContext.Current.CancellationToken);

        guard.Verify(g => g.EnsureOwnerOrParticipantAsync(_sessionId, caller, It.IsAny<CancellationToken>()), Times.Once);
        guard.Verify(g => g.EnsureOwnerOrParticipantAsync(
            It.IsAny<Guid>(), _registeredUserId, It.IsAny<CancellationToken>()), Times.Never);
        guard.Verify(g => g.EnsureOwnerOrParticipantAsync(
            It.IsAny<Guid>(), _registeredParticipantId, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTheGuardRefuses_NotEvenTheSessionIsRead()
    {
        var guard = new Mock<ISessionAccessGuard>();
        guard.Setup(g => g.EnsureOwnerOrParticipantAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ForbiddenException("no"));

        var act = () => CreateHandler(guard.Object).Handle(
            Ask(senderId: _registeredParticipantId, requestedBy: Guid.NewGuid()), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ForbiddenException>();
        _sessions.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        NothingWasSavedPaidOrPublished();
    }

    [Fact]
    public void Constructor_WithoutTheGuard_Throws()
    {
        var act = () => CreateHandler(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
