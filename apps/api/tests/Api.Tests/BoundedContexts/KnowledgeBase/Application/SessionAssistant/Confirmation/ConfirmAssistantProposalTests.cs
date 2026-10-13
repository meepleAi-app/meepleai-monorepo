using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Confirmation;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Turn;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using FluentValidation.TestHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;
using ValidationException = Api.SharedKernel.Domain.Exceptions.ValidationException;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Confirmation;

/// <summary>
/// Spec 2026-10-11 §5.3: la conferma è l'unico ponte verso le scritture. «Prima prenota, poi applica»:
/// la prenotazione è l'idempotenza, il controllo dello stato atteso protegge da una partita cambiata.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4277")]
public sealed class ConfirmAssistantProposalTests
{
    private static readonly Guid Session = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid Thread = Guid.Parse("00000000-0000-0000-0000-0000000000a2");
    private static readonly Guid Owner = Guid.Parse("00000000-0000-0000-0000-0000000000b1");
    private static readonly Guid Version = Guid.Parse("00000000-0000-0000-0000-0000000000d1");
    private static readonly Guid Marco = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly Mock<IAssistantProposalRepository> _proposals = new();
    private readonly Mock<IAssistantSessionDirectory> _directory = new();
    private readonly Mock<ISessionAssistantReader> _reader = new();
    private readonly Mock<IAssistantProposalApplier> _applier = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 12, 20, 0, 0, TimeSpan.Zero));

    public ConfirmAssistantProposalTests()
    {
        _directory.Setup(d => d.GetAccessAsync(Session, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssistantSessionAccess(Owner, "InProgress", true));
        _directory.Setup(d => d.CreateReader(Session, Owner)).Returns(_reader.Object);
        Table(cell: 7, turnIndex: 4);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    /// <summary>Lo stato della partita al momento della conferma: la cella di Marco al round 2 e l'indice di turno.</summary>
    private void Table(int? cell, int turnIndex) =>
        _reader.Setup(r => r.GetStateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new AssistantSessionState(
            GameId: null,
            GameName: "Azul",
            Status: "InProgress",
            CanRecordScores: true,
            CanAdvanceTurn: true,
            Players: [new(Marco, "Marco", cell ?? 0, true)],
            CurrentTurnPlayerId: Marco,
            CurrentTurnIndex: turnIndex,
            CurrentRound: 2,
            CurrentPhase: null,
            Dimensions: ["punti"],
            Scores: cell is { } value
                ? new List<AssistantScoreCell> { new(Marco, 2, "PUNTI", value) }
                : new List<AssistantScoreCell>(),
            HouseRules: []));

    private AssistantProposal ScoreProposal(int points = 5, int cellBefore = 7)
    {
        var proposal = AssistantProposal.ForScore(Session, Thread, Guid.NewGuid(), Version, Marco, "Marco", "punti", 2, points, cellBefore, Now);
        _proposals.Setup(p => p.GetAsync(proposal.Id, It.IsAny<CancellationToken>())).ReturnsAsync(proposal);
        return proposal;
    }

    private AssistantProposal TurnProposal(int turnIndex = 4)
    {
        var proposal = AssistantProposal.ForAdvanceTurn(Session, Thread, Guid.NewGuid(), Version, turnIndex, Now);
        _proposals.Setup(p => p.GetAsync(proposal.Id, It.IsAny<CancellationToken>())).ReturnsAsync(proposal);
        return proposal;
    }

    private ConfirmAssistantProposalCommandHandler Handler() =>
        new(_proposals.Object, _directory.Object, _applier.Object, _unitOfWork.Object, _time);

    private Task<AssistantProposalDto> ConfirmAsync(AssistantProposal proposal, int? corrected = null, Guid? user = null, Guid? session = null) =>
        Handler().Handle(new ConfirmAssistantProposalCommand(session ?? Session, proposal.Id, user ?? Owner, corrected), Ct);

    private void VerifyNothingApplied()
    {
        _applier.Verify(a => a.ApplyScoreAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        _applier.Verify(a => a.AdvanceTurnAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Confirm_ReservesFirst_ThenWritesTheCellPlusTheDelta_AndAccepts()
    {
        var proposal = ScoreProposal(points: 5, cellBefore: 7);
        AssistantProposalStatus? statusWhenApplied = null;
        var savesBeforeApply = -1;
        _applier.Setup(a => a.ApplyScoreAsync(Session, Owner, Marco, 2, "punti", 12, It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                statusWhenApplied = proposal.Status;
                savesBeforeApply = _unitOfWork.Invocations.Count;
            })
            .Returns(Task.CompletedTask);

        var dto = await ConfirmAsync(proposal);

        statusWhenApplied.Should().Be(AssistantProposalStatus.Applying, "la prenotazione precede la scrittura");
        savesBeforeApply.Should().Be(1, "ed è già salvata quando la scrittura parte");
        proposal.Status.Should().Be(AssistantProposalStatus.Accepted);
        proposal.ResolvedBy.Should().Be(Owner);
        dto.Status.Should().Be("Accepted");
        dto.ResultingValue.Should().Be(12);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Confirm_WithACorrection_WritesTheCorrectedDelta_AndIsEdited()
    {
        var proposal = ScoreProposal(points: 5, cellBefore: 7);

        var dto = await ConfirmAsync(proposal, corrected: 3);

        _applier.Verify(a => a.ApplyScoreAsync(Session, Owner, Marco, 2, "punti", 10, It.IsAny<CancellationToken>()), Times.Once);
        proposal.Status.Should().Be(AssistantProposalStatus.Edited);
        dto.ConfirmedPoints.Should().Be(3);
        dto.ResultingValue.Should().Be(10);
    }

    [Fact]
    public async Task Confirm_WithTheSamePointsAsProposed_IsAccepted_NotEdited()
    {
        var proposal = ScoreProposal(points: 5);

        await ConfirmAsync(proposal, corrected: 5);

        proposal.Status.Should().Be(AssistantProposalStatus.Accepted);
        proposal.ConfirmedPayload.Should().BeNull();
    }

    [Fact]
    public async Task Confirm_OnACellThatDidNotExist_ExpectsZero()
    {
        Table(cell: null, turnIndex: 4);
        var proposal = ScoreProposal(points: 5, cellBefore: 0);

        await ConfirmAsync(proposal);

        _applier.Verify(a => a.ApplyScoreAsync(Session, Owner, Marco, 2, "punti", 5, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Review Focus 4: qualcuno ha scritto la stessa cella fra la proposta e la conferma.</summary>
    [Fact]
    public async Task Confirm_WhenTheCellChanged_IsStaleAndDoesNotWrite()
    {
        var proposal = ScoreProposal(points: 5, cellBefore: 7);
        Table(cell: 9, turnIndex: 4);

        var act = () => ConfirmAsync(proposal);

        (await act.Should().ThrowAsync<ConflictException>()).Which.ErrorCode.Should().Be("proposal_stale");
        proposal.Status.Should().Be(AssistantProposalStatus.Stale);
        VerifyNothingApplied();
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2), "prenotazione ed esito");
    }

    [Fact]
    public async Task Confirm_ATurn_AdvancesIt()
    {
        var proposal = TurnProposal(turnIndex: 4);

        var dto = await ConfirmAsync(proposal);

        _applier.Verify(a => a.AdvanceTurnAsync(Session, It.IsAny<CancellationToken>()), Times.Once);
        dto.Status.Should().Be("Accepted");
    }

    [Fact]
    public async Task Confirm_ATurn_WhenTheTurnAlreadyMoved_IsStale()
    {
        var proposal = TurnProposal(turnIndex: 4);
        Table(cell: 7, turnIndex: 5);

        var act = () => ConfirmAsync(proposal);

        (await act.Should().ThrowAsync<ConflictException>()).Which.ErrorCode.Should().Be("proposal_stale");
        VerifyNothingApplied();
    }

    [Fact]
    public async Task Confirm_ByAnyoneButTheOwner_IsForbidden_AndNothingIsReserved()
    {
        var proposal = ScoreProposal();

        var act = () => ConfirmAsync(proposal, user: Guid.NewGuid());

        await act.Should().ThrowAsync<ForbiddenException>();
        proposal.Status.Should().Be(AssistantProposalStatus.Pending);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Confirm_AProposalOfAnotherSession_IsNotFound()
    {
        // L'host possiede anche l'altra sessione: l'id della proposta non basta a confermarla da lì.
        var other = Guid.NewGuid();
        _directory.Setup(d => d.GetAccessAsync(other, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssistantSessionAccess(Owner, "InProgress", true));
        var proposal = ScoreProposal();

        var act = () => ConfirmAsync(proposal, session: other);

        await act.Should().ThrowAsync<NotFoundException>();
        proposal.Status.Should().Be(AssistantProposalStatus.Pending);
    }

    [Theory]
    [InlineData("session_state_invalid")]
    [InlineData("proposal_not_applicable")]
    public async Task Confirm_WhenTheDomainRefuses_IsFailedWithTheCode(string failureCode)
    {
        var proposal = ScoreProposal();
        _applier.Setup(a => a.ApplyScoreAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AssistantProposalApplyException(failureCode, "the domain said no", new InvalidOperationException()));

        var act = () => ConfirmAsync(proposal);

        (await act.Should().ThrowAsync<ConflictException>()).Which.ErrorCode.Should().Be(failureCode);
        proposal.Status.Should().Be(AssistantProposalStatus.Failed);
        proposal.FailureCode.Should().Be(failureCode);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    /// <summary>
    /// Review Focus 8: dopo la prenotazione lo stato della partita non si legge (la sessione è sparita).
    /// Nulla è stato scritto, quindi la proposta si chiude: restando Applying avrebbe una card che non si
    /// può più né confermare né rifiutare.
    /// </summary>
    [Fact]
    public async Task Confirm_WhenTheStateCannotBeRead_FailsTheProposal_InsteadOfLeavingItApplying()
    {
        var proposal = ScoreProposal();
        _reader.Setup(r => r.GetStateAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("LiveGameSession", Session.ToString()));

        var act = () => ConfirmAsync(proposal);

        await act.Should().ThrowAsync<NotFoundException>();
        proposal.Status.Should().Be(AssistantProposalStatus.Failed);
        proposal.FailureCode.Should().Be("session_state_unavailable");
        VerifyNothingApplied();
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2), "prenotazione ed esito");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ASecondTap_OnAProposalAlreadyConfirmed_ReturnsItsState_WithoutApplyingAgain(bool edited)
    {
        var proposal = ScoreProposal();
        proposal.BeginApplying(Owner);
        if (edited)
        {
            proposal.MarkEdited(3, Now);
        }
        else
        {
            proposal.MarkAccepted(Now);
        }

        var dto = await ConfirmAsync(proposal);

        dto.Status.Should().Be(edited ? "Edited" : "Accepted");
        VerifyNothingApplied();
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Confirm_ARejectedProposal_IsNotPending()
    {
        var proposal = ScoreProposal();
        proposal.Reject(Owner, Now);

        var act = () => ConfirmAsync(proposal);

        (await act.Should().ThrowAsync<ConflictException>()).Which.ErrorCode.Should().Be("proposal_not_pending");
        VerifyNothingApplied();
    }

    /// <summary>
    /// Review Focus 3: due tap nello stesso istante. Entrambi caricano la proposta Pending; il secondo che
    /// salva la prenotazione perde su xmin, non scrive e riceve lo stato lasciato dal primo.
    /// </summary>
    [Fact]
    public async Task Confirm_WhenTheReservationLosesTheRace_ReturnsTheCurrentState_WithoutWriting()
    {
        var proposal = ScoreProposal();
        var winner = AssistantProposal.ForScore(Session, Thread, proposal.MessageId, Version, Marco, "Marco", "punti", 2, 5, 7, Now);
        winner.BeginApplying(Owner);
        winner.MarkAccepted(Now);
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new DbUpdateConcurrencyException("xmin"));
        _proposals.Setup(p => p.GetReadOnlyAsync(proposal.Id, It.IsAny<CancellationToken>())).ReturnsAsync(winner);

        var dto = await ConfirmAsync(proposal);

        dto.Status.Should().Be("Accepted");
        VerifyNothingApplied();
    }

    [Fact]
    public async Task Confirm_WhenTheReservationLosesToAnExpiry_IsNotPending()
    {
        var proposal = ScoreProposal();
        var expired = AssistantProposal.ForScore(Session, Thread, proposal.MessageId, Version, Marco, "Marco", "punti", 2, 5, 7, Now);
        expired.Expire(Now);
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new DbUpdateConcurrencyException("xmin"));
        _proposals.Setup(p => p.GetReadOnlyAsync(proposal.Id, It.IsAny<CancellationToken>())).ReturnsAsync(expired);

        var act = () => ConfirmAsync(proposal);

        (await act.Should().ThrowAsync<ConflictException>()).Which.ErrorCode.Should().Be("proposal_not_pending");
        VerifyNothingApplied();
    }

    [Fact]
    public async Task AfterTheReservation_TheConfirmNoLongerFollowsTheClientCancellation()
    {
        var proposal = ScoreProposal();
        using var client = new CancellationTokenSource();

        await Handler().Handle(new ConfirmAssistantProposalCommand(Session, proposal.Id, Owner, null), client.Token);

        _applier.Verify(a => a.ApplyScoreAsync(
            Session, Owner, Marco, 2, "punti", 12, It.Is<CancellationToken>(token => !token.CanBeCanceled)), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.Is<CancellationToken>(token => token.CanBeCanceled)), Times.Once, "la prenotazione");
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.Is<CancellationToken>(token => !token.CanBeCanceled)), Times.Once, "l'esito");
        _reader.Verify(r => r.GetStateAsync(It.Is<CancellationToken>(token => !token.CanBeCanceled)), Times.Once);
    }

    [Fact]
    public async Task ACorrectionOnATurnProposal_IsAValidationError_AndNothingIsReserved()
    {
        var proposal = TurnProposal();

        var act = () => ConfirmAsync(proposal, corrected: 1);

        await act.Should().ThrowAsync<ValidationException>();
        proposal.Status.Should().Be(AssistantProposalStatus.Pending);
    }

    [Fact]
    public void TheValidator_WantsTheIds_AndACorrectionWithinTheLimit()
    {
        var validator = new ConfirmAssistantProposalCommandValidator();

        validator.TestValidate(new ConfirmAssistantProposalCommand(Session, Guid.NewGuid(), Owner, 3)).ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(new ConfirmAssistantProposalCommand(Session, Guid.NewGuid(), Owner, null)).ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(new ConfirmAssistantProposalCommand(Guid.Empty, Guid.NewGuid(), Owner, null))
            .ShouldHaveValidationErrorFor(c => c.LiveSessionId);
        validator.TestValidate(new ConfirmAssistantProposalCommand(Session, Guid.Empty, Owner, null))
            .ShouldHaveValidationErrorFor(c => c.ProposalId);
        validator.TestValidate(new ConfirmAssistantProposalCommand(Session, Guid.NewGuid(), Owner, AssistantProposal.MaxPointsPerProposal + 1))
            .ShouldHaveValidationErrorFor(c => c.CorrectedPoints);
    }

    /// <summary>
    /// Dalla review di A2: il valore da scrivere si calcola prima della prenotazione. Una correzione che
    /// l'aggregato rifiuta (il validator della pipeline è la prima difesa, non l'unica) deve lasciare la
    /// proposta Pending: calcolata dopo, la lascerebbe Applying, senza nessuno che la chiuda.
    /// </summary>
    [Fact]
    public async Task ACorrectionTheAggregateRefuses_IsRefusedBeforeTheReservation()
    {
        var proposal = ScoreProposal();

        var act = () => ConfirmAsync(proposal, corrected: AssistantProposal.MaxPointsPerProposal + 1);

        await act.Should().ThrowAsync<ValidationException>();
        proposal.Status.Should().Be(AssistantProposalStatus.Pending);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        VerifyNothingApplied();
    }
}
