using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Turn;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Turn;

/// <summary>
/// Spec 2026-10-11 §5.2: l'unica uscita degli strumenti di proposta è una proposta salvata. Il sink la
/// lega alla sessione, al thread, al messaggio e alla versione del profilo del turno.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4276")]
public sealed class RepositoryProposalSinkTests
{
    private static readonly Guid Session = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid Thread = Guid.Parse("00000000-0000-0000-0000-0000000000a2");
    private static readonly Guid Message = Guid.Parse("00000000-0000-0000-0000-0000000000a3");
    private static readonly Guid Version = Guid.Parse("00000000-0000-0000-0000-0000000000a4");
    private static readonly Guid Marco = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly Mock<IAssistantProposalRepository> _repository = new();
    private readonly List<AssistantProposal> _added = [];
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 12, 20, 0, 0, TimeSpan.Zero));

    public RepositoryProposalSinkTests()
    {
        _repository.Setup(r => r.AddAsync(It.IsAny<AssistantProposal>(), It.IsAny<CancellationToken>()))
            .Callback<AssistantProposal, CancellationToken>((p, _) => _added.Add(p))
            .Returns(Task.CompletedTask);
    }

    private RepositoryProposalSink Sink() => new(_repository.Object, Session, Thread, Message, Version, _time);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ProposeScore_AddsAPendingProposal_BoundToTheTurn()
    {
        var sink = Sink();

        var receipt = await sink.ProposeScoreAsync(new ScoreProposalDraft(Marco, "Marco", "punti", 2, 5, 7), Ct);

        var proposal = _added.Should().ContainSingle().Subject;
        proposal.Status.Should().Be(AssistantProposalStatus.Pending);
        proposal.Payload.Should().Be(new AssistantProposalPayload(Marco, "Marco", "punti", 2, 5));
        proposal.Expected.Should().Be(new AssistantProposalExpectation(7, null));
        (proposal.LiveSessionId, proposal.ChatThreadId, proposal.MessageId, proposal.ProfileVersionId)
            .Should().Be((Session, Thread, Message, Version));
        proposal.CreatedAt.Should().Be(_time.GetUtcNow().UtcDateTime);
        receipt.Should().Be(new ProposalReceipt(proposal.Id, "+5 punti a Marco, round 2"));
        sink.Created.Should().Equal(proposal);
    }

    [Fact]
    public async Task ProposeTurn_AddsAProposalThatExpectsTheCurrentTurnIndex()
    {
        var receipt = await Sink().ProposeTurnAsync(new TurnProposalDraft(4, Marco), Ct);

        var proposal = _added.Should().ContainSingle().Subject;
        proposal.Kind.Should().Be(AssistantProposalKind.AdvanceTurn);
        proposal.Expected.Should().Be(new AssistantProposalExpectation(null, 4));
        receipt.Summary.Should().Be("Passare il turno al giocatore successivo");
    }

    /// <summary>
    /// Il sink non unisce due chiamate uguali. Un modello che ripete una chiamata lascia due card, e a
    /// vederlo dev'essere il set di valutazione (che conta le proposte di troppo, spec §7.1): se la
    /// produzione le unisse, il gate misurerebbe un difetto che l'host non vede più. La seconda non può
    /// comunque applicarsi: dopo la prima conferma la cella non è più quella attesa.
    /// </summary>
    [Fact]
    public async Task EveryCall_IsItsOwnProposal()
    {
        var sink = Sink();
        var draft = new ScoreProposalDraft(Marco, "Marco", "punti", 2, 5, 7);

        var first = await sink.ProposeScoreAsync(draft, Ct);
        var second = await sink.ProposeScoreAsync(draft, Ct);

        second.ProposalId.Should().NotBe(first.ProposalId);
        sink.Created.Should().HaveCount(2);
        _added.Should().HaveCount(2);
    }

    [Fact]
    public void TheDto_ShowsWhatTheHostConfirms_AndTheResultingCell()
    {
        var proposal = AssistantProposal.ForScore(Session, Thread, Message, Version, Marco, "Marco", "punti", 2, -2, 7, _time.GetUtcNow().UtcDateTime);

        var dto = AssistantProposalDto.From(proposal);

        dto.Should().Be(new AssistantProposalDto(
            proposal.Id, "Score", "Pending", Message, Marco, "Marco", "punti", 2, -2,
            CellValueBefore: 7, ResultingValue: 5, ExpectedTurnIndex: null, ConfirmedPoints: null, FailureCode: null,
            Summary: "-2 punti a Marco, round 2"));
    }

    [Fact]
    public void TheDto_OfAnEditedProposal_UsesTheConfirmedPoints()
    {
        var proposal = AssistantProposal.ForScore(Session, Thread, Message, Version, Marco, "Marco", "punti", 2, 5, 7, _time.GetUtcNow().UtcDateTime);
        proposal.BeginApplying(Guid.NewGuid());
        proposal.MarkEdited(3, _time.GetUtcNow().UtcDateTime);

        var dto = AssistantProposalDto.From(proposal);

        dto.Status.Should().Be("Edited");
        dto.Points.Should().Be(5);
        dto.ConfirmedPoints.Should().Be(3);
        dto.ResultingValue.Should().Be(10);
        dto.Summary.Should().Be("+3 punti a Marco, round 2");
    }

    [Fact]
    public void TheDto_OfATurnProposal_HasNoScoreFields()
    {
        var proposal = AssistantProposal.ForAdvanceTurn(Session, Thread, Message, Version, 4, _time.GetUtcNow().UtcDateTime);

        var dto = AssistantProposalDto.From(proposal);

        dto.Kind.Should().Be("AdvanceTurn");
        dto.ExpectedTurnIndex.Should().Be(4);
        dto.ResultingValue.Should().BeNull();
        dto.PlayerId.Should().BeNull();
    }

    [Theory]
    [InlineData(AssistantProposalStatus.Pending, "in attesa di conferma")]
    [InlineData(AssistantProposalStatus.Applying, "in applicazione")]
    [InlineData(AssistantProposalStatus.Accepted, "confermata dall'host")]
    [InlineData(AssistantProposalStatus.Edited, "confermata dall'host con una correzione")]
    [InlineData(AssistantProposalStatus.Rejected, "rifiutata dall'host")]
    [InlineData(AssistantProposalStatus.Expired, "scaduta senza conferma")]
    [InlineData(AssistantProposalStatus.Stale, "superata: la partita era cambiata")]
    [InlineData(AssistantProposalStatus.Failed, "non applicata")]
    public void EveryStatus_HasAnOutcomeTheModelCanRead(AssistantProposalStatus status, string expected) =>
        AssistantProposalSummary.Outcome(status).Should().Be(expected);
}
