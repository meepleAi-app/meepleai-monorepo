using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// Spec 2026-10-11 §5.2 e §5.3: la proposta dell'assistente è l'unica cosa che l'assistente produce oltre
/// al testo. La macchina a stati è la garanzia che una proposta non si applichi due volte.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4275")]
public sealed class AssistantProposalTests
{
    private static readonly Guid Session = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid Thread = Guid.Parse("00000000-0000-0000-0000-0000000000a2");
    private static readonly Guid Message = Guid.Parse("00000000-0000-0000-0000-0000000000a3");
    private static readonly Guid Version = Guid.Parse("00000000-0000-0000-0000-0000000000a4");
    private static readonly Guid Marco = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Host = Guid.Parse("00000000-0000-0000-0000-0000000000b1");
    private static readonly DateTime Now = new(2026, 10, 12, 20, 0, 0, DateTimeKind.Utc);

    private static AssistantProposal Score(int points = 5, int cellBefore = 7) =>
        AssistantProposal.ForScore(Session, Thread, Message, Version, Marco, "Marco", "punti", 2, points, cellBefore, Now);

    private static AssistantProposal Turn() =>
        AssistantProposal.ForAdvanceTurn(Session, Thread, Message, Version, currentTurnIndex: 4, Now);

    [Fact]
    public void ForScore_IsPending_AndKeepsWhatTheHostWillConfirm()
    {
        var proposal = Score();

        proposal.Status.Should().Be(AssistantProposalStatus.Pending);
        proposal.Kind.Should().Be(AssistantProposalKind.Score);
        proposal.Payload.Should().Be(new AssistantProposalPayload(Marco, "Marco", "punti", 2, 5));
        proposal.Expected.Should().Be(new AssistantProposalExpectation(CellValue: 7, TurnIndex: null));
        proposal.LiveSessionId.Should().Be(Session);
        proposal.ChatThreadId.Should().Be(Thread);
        proposal.MessageId.Should().Be(Message);
        proposal.ProfileVersionId.Should().Be(Version);
        proposal.CreatedAt.Should().Be(Now);
        proposal.IsOpen.Should().BeTrue();
        proposal.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void ForAdvanceTurn_ExpectsTheTurnIndexItWasProposedOn()
    {
        var proposal = Turn();

        proposal.Kind.Should().Be(AssistantProposalKind.AdvanceTurn);
        proposal.Payload.Should().Be(new AssistantProposalPayload(null, null, null, null, null));
        proposal.Expected.Should().Be(new AssistantProposalExpectation(CellValue: null, TurnIndex: 4));
    }

    [Theory]
    [InlineData("", "punti", 1)]
    [InlineData("Marco", " ", 1)]
    [InlineData("Marco", "punti", 0)]
    public void ForScore_WithoutAPlayerNameADimensionOrAValidRound_IsRejected(string name, string dimension, int round)
    {
        var act = () => AssistantProposal.ForScore(Session, Thread, Message, Version, Marco, name, dimension, round, 5, 0, Now);

        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void NegativePoints_AreAValidDelta()
    {
        var proposal = Score(points: -2, cellBefore: 7);

        proposal.ScoreValueToWrite(correctedPoints: null).Should().Be(5);
    }

    /// <summary>
    /// I punti li scrive un modello: un numero assurdo, sommato alla cella, farebbe il giro dell'intero e
    /// finirebbe nel punteggio vero. Il limite vale per la proposta e per la correzione dell'host.
    /// </summary>
    [Theory]
    [InlineData(AssistantProposal.MaxPointsPerProposal + 1)]
    [InlineData(-AssistantProposal.MaxPointsPerProposal - 1)]
    [InlineData(int.MinValue)]
    public void PointsBeyondTheLimit_AreRejected_InTheProposalAndInTheCorrection(int points)
    {
        var propose = () => Score(points: points);
        var reserved = Score();
        reserved.BeginApplying(Host);
        var correct = () => reserved.MarkEdited(points, Now);

        propose.Should().Throw<ValidationException>();
        correct.Should().Throw<ValidationException>();
        reserved.Status.Should().Be(AssistantProposalStatus.Applying, "una correzione rifiutata non chiude la proposta");
    }

    /// <summary>Con la cella entro il suo limite e i punti entro il loro, la somma non può fare il giro dell'intero.</summary>
    [Theory]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    [InlineData(AssistantProposal.MaxCellValue + 1)]
    public void ACellBeyondAnyRealScore_IsRejected(int cellBefore)
    {
        var act = () => Score(points: 5, cellBefore: cellBefore);

        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void ScoreValueToWrite_AddsTheDeltaToTheExpectedCell_OrTheCorrectedOne()
    {
        var proposal = Score(points: 5, cellBefore: 7);

        proposal.ScoreValueToWrite(correctedPoints: null).Should().Be(12);
        proposal.ScoreValueToWrite(correctedPoints: 3).Should().Be(10);
    }

    [Fact]
    public void Confirm_GoesThroughApplying_AndEndsAccepted()
    {
        var proposal = Score();

        proposal.BeginApplying(Host);
        proposal.Status.Should().Be(AssistantProposalStatus.Applying);
        proposal.ResolvedBy.Should().Be(Host);
        proposal.IsOpen.Should().BeTrue("la prenotazione non è ancora un esito");

        proposal.MarkAccepted(Now.AddSeconds(1));

        proposal.Status.Should().Be(AssistantProposalStatus.Accepted);
        proposal.ResolvedAt.Should().Be(Now.AddSeconds(1));
        proposal.IsOpen.Should().BeFalse();
    }

    [Fact]
    public void Confirm_WithACorrection_EndsEdited_AndKeepsTheConfirmedPoints()
    {
        var proposal = Score(points: 5);
        proposal.BeginApplying(Host);

        proposal.MarkEdited(confirmedPoints: 3, Now.AddSeconds(1));

        proposal.Status.Should().Be(AssistantProposalStatus.Edited);
        proposal.ConfirmedPayload.Should().Be(new AssistantProposalConfirmation(3));
        proposal.Payload.Points.Should().Be(5, "la proposta originale resta leggibile");
    }

    [Fact]
    public void ATurnProposal_CannotBeEdited()
    {
        var proposal = Turn();
        proposal.BeginApplying(Host);

        var act = () => proposal.MarkEdited(1, Now);

        act.Should().Throw<ConflictException>();
    }

    [Fact]
    public void ASecondConfirm_IsRejectedWithTheNotPendingCode()
    {
        var proposal = Score();
        proposal.BeginApplying(Host);

        var act = () => proposal.BeginApplying(Host);

        act.Should().Throw<ConflictException>().Which.ErrorCode.Should().Be(AssistantProposal.NotPendingCode);
    }

    [Fact]
    public void Failed_KeepsTheDomainCode()
    {
        var proposal = Score();
        proposal.BeginApplying(Host);

        proposal.MarkFailed("session_state_invalid", Now.AddSeconds(1));

        proposal.Status.Should().Be(AssistantProposalStatus.Failed);
        proposal.FailureCode.Should().Be("session_state_invalid");
        proposal.ResolvedAt.Should().Be(Now.AddSeconds(1));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Stale_IsReachableFromPendingAndFromApplying(bool reserved)
    {
        var proposal = Score();
        if (reserved)
        {
            proposal.BeginApplying(Host);
        }

        proposal.MarkStale(Now.AddSeconds(1));

        proposal.Status.Should().Be(AssistantProposalStatus.Stale);
    }

    [Fact]
    public void RejectAndExpire_OnlyFromPending()
    {
        var rejected = Score();
        rejected.Reject(Host, Now.AddSeconds(1));
        rejected.Status.Should().Be(AssistantProposalStatus.Rejected);
        rejected.ResolvedBy.Should().Be(Host);

        var expired = Score();
        expired.Expire(Now.AddSeconds(1));
        expired.Status.Should().Be(AssistantProposalStatus.Expired);

        var reserved = Score();
        reserved.BeginApplying(Host);
        var rejectReserved = () => reserved.Reject(Host, Now);
        var expireReserved = () => reserved.Expire(Now);
        rejectReserved.Should().Throw<ConflictException>();
        expireReserved.Should().Throw<ConflictException>();
    }

    public static TheoryData<string> ClosedStatuses() => new() { "Accepted", "Edited", "Failed", "Rejected", "Expired", "Stale" };

    [Theory]
    [MemberData(nameof(ClosedStatuses))]
    public void AClosedProposal_AcceptsNoFurtherTransition(string status)
    {
        var proposal = Closed(status);

        var transitions = new Action[]
        {
            () => proposal.BeginApplying(Host),
            () => proposal.MarkAccepted(Now),
            () => proposal.MarkEdited(1, Now),
            () => proposal.MarkFailed("x", Now),
            () => proposal.MarkStale(Now),
            () => proposal.Reject(Host, Now),
            () => proposal.Expire(Now),
        };

        foreach (var transition in transitions)
        {
            transition.Should().Throw<ConflictException>($"una proposta {status} è chiusa");
        }

        proposal.Status.ToString().Should().Be(status);
    }

    private static AssistantProposal Closed(string status)
    {
        var proposal = Score();
        switch (status)
        {
            case "Accepted": proposal.BeginApplying(Host); proposal.MarkAccepted(Now); break;
            case "Edited": proposal.BeginApplying(Host); proposal.MarkEdited(3, Now); break;
            case "Failed": proposal.BeginApplying(Host); proposal.MarkFailed("x", Now); break;
            case "Rejected": proposal.Reject(Host, Now); break;
            case "Expired": proposal.Expire(Now); break;
            default: proposal.MarkStale(Now); break;
        }

        return proposal;
    }

    // ─── Review finale di A2 ────────────────────────────────────────────────────

    /// <summary>
    /// <c>ScoreValueToWrite</c> è l'ultimo passo prima della scrittura vera: il limite sulla correzione
    /// deve valere anche qui, non solo in <c>MarkEdited</c>, che arriva dopo la scrittura. Senza, una
    /// correzione assurda farebbe il giro dell'intero e finirebbe nel punteggio.
    /// </summary>
    [Theory]
    [InlineData(int.MaxValue)]
    [InlineData(AssistantProposal.MaxPointsPerProposal + 1)]
    [InlineData(-AssistantProposal.MaxPointsPerProposal - 1)]
    public void ScoreValueToWrite_RefusesACorrectionBeyondTheLimit(int corrected)
    {
        var act = () => Score().ScoreValueToWrite(corrected);

        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void ScoreValueToWrite_OnATurnProposal_IsRefused()
    {
        var act = () => Turn().ScoreValueToWrite(correctedPoints: null);

        act.Should().Throw<ConflictException>();
    }

    /// <summary>
    /// Una riga malformata (una proposta di punteggio senza la cella attesa) non deve diventare «scrivi la
    /// cella più zero»: deve fallire. Lo stato si costruisce come lo materializzerebbe EF.
    /// </summary>
    [Fact]
    public void ScoreValueToWrite_OnAScoreProposalWithoutItsExpectedCell_Fails()
    {
        var proposal = Score();
        typeof(AssistantProposal).GetProperty(nameof(AssistantProposal.Expected))!
            .SetValue(proposal, new AssistantProposalExpectation(CellValue: null, TurnIndex: null));

        var act = () => proposal.ScoreValueToWrite(correctedPoints: null);

        act.Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// «Prima prenota, poi applica» (spec §5.3): un esito senza la prenotazione salvata lascerebbe chiudere
    /// la stessa proposta a due conferme. È la regola per cui l'aggregato esiste.
    /// </summary>
    [Fact]
    public void AnOutcome_NeedsTheReservation()
    {
        var accept = () => Score().MarkAccepted(Now);
        var edit = () => Score().MarkEdited(3, Now);
        var fail = () => Score().MarkFailed("session_state_invalid", Now);

        accept.Should().Throw<ConflictException>().Which.ErrorCode.Should().Be(AssistantProposal.NotPendingCode);
        edit.Should().Throw<ConflictException>();
        fail.Should().Throw<ConflictException>();
    }
}
