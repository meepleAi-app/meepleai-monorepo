using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.Entities;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class RuleDisputeTests
{
    private static readonly Guid SessionId = Guid.NewGuid();
    private static readonly Guid GameId = Guid.NewGuid();
    private static readonly Guid InitiatorId = Guid.NewGuid();
    private static readonly Guid RespondentId = Guid.NewGuid();

    private static RuleDispute CreateDispute(string claim = "I can play 2 cards per turn") =>
        RuleDispute.Open(SessionId, GameId, InitiatorId, claim);

    private static DisputeVerdict CreateVerdict(
        RulingFor rulingFor = RulingFor.Initiator,
        string reasoning = "Rule 3.1 allows 2 cards",
        string? citation = "Page 5, Section 3.1",
        VerdictConfidence confidence = VerdictConfidence.High) =>
        new(rulingFor, reasoning, citation, confidence);

    #region Open

    [Fact]
    public void Open_ShouldCreateDisputeWithCorrectInitialState()
    {
        // Act
        var dispute = CreateDispute();

        // Assert
        dispute.Id.Should().NotBe(Guid.Empty);
        dispute.SessionId.Should().Be(SessionId);
        dispute.GameId.Should().Be(GameId);
        dispute.InitiatorPlayerId.Should().Be(InitiatorId);
        dispute.InitiatorClaim.Should().Be("I can play 2 cards per turn");
        dispute.RespondentPlayerId.Should().BeNull();
        dispute.RespondentClaim.Should().BeNull();
        dispute.Verdict.Should().BeNull();
        dispute.Votes.Should().BeEmpty();
        dispute.FinalOutcome.Should().Be(DisputeOutcome.Pending);
        dispute.OverrideRule.Should().BeNull();
        (dispute.CreatedAt <= DateTime.UtcNow).Should().BeTrue();
        dispute.RelatedDisputeIds.Should().BeEmpty();
    }

    [Fact]
    public void Open_EmptySessionId_ShouldThrow()
    {
        var act = () =>
            RuleDispute.Open(Guid.Empty, GameId, InitiatorId, "claim");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Open_EmptyGameId_ShouldThrow()
    {
        var act = () =>
            RuleDispute.Open(SessionId, Guid.Empty, InitiatorId, "claim");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Open_EmptyInitiatorPlayerId_ShouldThrow()
    {
        var act = () =>
            RuleDispute.Open(SessionId, GameId, Guid.Empty, "claim");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Open_NullClaim_ShouldThrow()
    {
        var act = () =>
            RuleDispute.Open(SessionId, GameId, InitiatorId, null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Open_WhitespaceClaim_ShouldThrow()
    {
        var act = () =>
            RuleDispute.Open(SessionId, GameId, InitiatorId, "   ");
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region AddRespondentClaim

    [Fact]
    public void AddRespondentClaim_ShouldSetRespondent()
    {
        // Arrange
        var dispute = CreateDispute();

        // Act
        dispute.AddRespondentClaim(RespondentId, "No, only 1 card per turn");

        // Assert
        dispute.RespondentPlayerId.Should().Be(RespondentId);
        dispute.RespondentClaim.Should().Be("No, only 1 card per turn");
    }

    [Fact]
    public void AddRespondentClaim_NullClaim_ShouldThrow()
    {
        var dispute = CreateDispute();
        var act = () =>
            dispute.AddRespondentClaim(RespondentId, null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddRespondentClaim_EmptyPlayerId_ShouldThrow()
    {
        var dispute = CreateDispute();
        var act = () =>
            dispute.AddRespondentClaim(Guid.Empty, "counter claim");
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region SetVerdict

    [Fact]
    public void SetVerdict_ShouldSetVerdictAndKeepPending()
    {
        // Arrange
        var dispute = CreateDispute();
        var verdict = CreateVerdict();

        // Act
        dispute.SetVerdict(verdict);

        // Assert
        dispute.Verdict.Should().NotBeNull();
        dispute.Verdict.RulingFor.Should().Be(RulingFor.Initiator);
        dispute.Verdict.Reasoning.Should().Be("Rule 3.1 allows 2 cards");
        dispute.FinalOutcome.Should().Be(DisputeOutcome.Pending);
    }

    [Fact]
    public void SetVerdict_Null_ShouldThrow()
    {
        var dispute = CreateDispute();
        ((Action)(() => dispute.SetVerdict(null!))).Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region CastVote

    [Fact]
    public void CastVote_ShouldAddVote()
    {
        // Arrange
        var dispute = CreateDispute();
        var playerId = Guid.NewGuid();

        // Act
        dispute.CastVote(playerId, true);

        // Assert
        dispute.Votes.Should().ContainSingle();
        dispute.Votes[0].PlayerId.Should().Be(playerId);
        (dispute.Votes[0].AcceptsVerdict).Should().BeTrue();
    }

    [Fact]
    public void CastVote_DuplicatePlayer_ShouldThrow()
    {
        // Arrange
        var dispute = CreateDispute();
        var playerId = Guid.NewGuid();
        dispute.CastVote(playerId, true);

        // Act & Assert
        var act = () =>
            dispute.CastVote(playerId, false);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CastVote_EmptyPlayerId_ShouldThrow()
    {
        var dispute = CreateDispute();
        var act = () =>
            dispute.CastVote(Guid.Empty, true);
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region TallyVotes

    [Fact]
    public void TallyVotes_MajorityAccepts_ShouldSetVerdictAccepted()
    {
        // Arrange
        var dispute = CreateDispute();
        dispute.SetVerdict(CreateVerdict());
        dispute.CastVote(Guid.NewGuid(), true);
        dispute.CastVote(Guid.NewGuid(), true);
        dispute.CastVote(Guid.NewGuid(), false);

        // Act
        dispute.TallyVotes();

        // Assert
        dispute.FinalOutcome.Should().Be(DisputeOutcome.VerdictAccepted);
    }

    [Fact]
    public void TallyVotes_MajorityRejects_ShouldSetVerdictOverridden()
    {
        // Arrange
        var dispute = CreateDispute();
        dispute.SetVerdict(CreateVerdict());
        dispute.CastVote(Guid.NewGuid(), false);
        dispute.CastVote(Guid.NewGuid(), false);
        dispute.CastVote(Guid.NewGuid(), true);

        // Act
        dispute.TallyVotes();

        // Assert
        dispute.FinalOutcome.Should().Be(DisputeOutcome.VerdictOverridden);
    }

    [Fact]
    public void TallyVotes_Tie_ShouldSetVerdictOverridden()
    {
        // Arrange — tie means NOT majority accepts → overridden
        var dispute = CreateDispute();
        dispute.SetVerdict(CreateVerdict());
        dispute.CastVote(Guid.NewGuid(), true);
        dispute.CastVote(Guid.NewGuid(), false);

        // Act
        dispute.TallyVotes();

        // Assert
        dispute.FinalOutcome.Should().Be(DisputeOutcome.VerdictOverridden);
    }

    [Fact]
    public void TallyVotes_ShouldRaiseStructuredDisputeResolvedEvent()
    {
        // Arrange
        var dispute = CreateDispute();
        dispute.SetVerdict(CreateVerdict());
        dispute.CastVote(Guid.NewGuid(), true);

        // Act — TallyVotes no longer raises the event; RaiseResolvedEvent is a separate step
        dispute.TallyVotes();
        dispute.RaiseResolvedEvent();

        // Assert
        var domainEvent = dispute.DomainEvents.Should().ContainSingle().Subject;
        domainEvent.Should().BeOfType<StructuredDisputeResolvedEvent>();
        var resolvedEvent = (StructuredDisputeResolvedEvent)domainEvent;
        resolvedEvent.DisputeId.Should().Be(dispute.Id);
        resolvedEvent.SessionId.Should().Be(SessionId);
        resolvedEvent.GameId.Should().Be(GameId);
        resolvedEvent.Verdict.Should().NotBeNull();
        resolvedEvent.FinalOutcome.Should().Be(DisputeOutcome.VerdictAccepted);
    }

    [Fact]
    public void TallyVotes_NoVerdict_ShouldThrow()
    {
        var dispute = CreateDispute();
        dispute.CastVote(Guid.NewGuid(), true);

        ((Action)(() => dispute.TallyVotes())).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void TallyVotes_NoVotes_ShouldThrow()
    {
        var dispute = CreateDispute();
        dispute.SetVerdict(CreateVerdict());

        ((Action)(() => dispute.TallyVotes())).Should().Throw<InvalidOperationException>();
    }

    #endregion

    #region SetOverrideRule

    [Fact]
    public void SetOverrideRule_WhenVerdictOverridden_ShouldSetRule()
    {
        // Arrange
        var dispute = CreateDispute();
        dispute.SetVerdict(CreateVerdict());
        dispute.CastVote(Guid.NewGuid(), false);
        dispute.TallyVotes();

        // Act
        dispute.SetOverrideRule("House rule: 2 cards allowed on first turn only");

        // Assert
        dispute.OverrideRule.Should().Be("House rule: 2 cards allowed on first turn only");
    }

    [Fact]
    public void SetOverrideRule_WhenPending_ShouldThrow()
    {
        var dispute = CreateDispute();

        var act = () =>
            dispute.SetOverrideRule("some rule");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void SetOverrideRule_WhenVerdictAccepted_ShouldThrow()
    {
        var dispute = CreateDispute();
        dispute.SetVerdict(CreateVerdict());
        dispute.CastVote(Guid.NewGuid(), true);
        dispute.TallyVotes();

        var act = () =>
            dispute.SetOverrideRule("some rule");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void SetOverrideRule_EmptyRule_ShouldThrow()
    {
        var dispute = CreateDispute();
        dispute.SetVerdict(CreateVerdict());
        dispute.CastVote(Guid.NewGuid(), false);
        dispute.TallyVotes();

        var act = () =>
            dispute.SetOverrideRule("   ");
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region ToLegacyEntry

    [Fact]
    public void ToLegacyEntry_WithVerdict_ShouldProduceCorrectEntry()
    {
        // Arrange
        var dispute = CreateDispute("Can I play 2 cards?");
        var verdict = CreateVerdict(
            RulingFor.Initiator,
            "Yes, rule 3.1 allows it",
            "Page 5",
            VerdictConfidence.High);
        dispute.SetVerdict(verdict);

        // Act
        var entry = dispute.ToLegacyEntry();

        // Assert
        entry.Id.Should().Be(dispute.Id);
        entry.Description.Should().Be("Can I play 2 cards?");
        entry.Verdict.Should().Be("Yes, rule 3.1 allows it");
        entry.RuleReferences.Should().ContainSingle();
        entry.RuleReferences.Should().Contain("Page 5");
        entry.RaisedByPlayerName.Should().Be(InitiatorId.ToString());
        entry.Timestamp.Should().Be(dispute.CreatedAt);
    }

    [Fact]
    public void ToLegacyEntry_WithoutVerdict_ShouldThrow()
    {
        // Arrange — no verdict set, so legacy entry cannot be produced
        // (RuleDisputeEntry requires a non-empty verdict)
        var dispute = CreateDispute("Some claim");

        // Act & Assert
        ((Action)(() => dispute.ToLegacyEntry())).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ToLegacyEntry_VerdictWithNoCitation_ShouldHaveEmptyReferences()
    {
        // Arrange
        var dispute = CreateDispute();
        dispute.SetVerdict(new DisputeVerdict(RulingFor.Ambiguous, "Unclear rule", null, VerdictConfidence.Low));

        // Act
        var entry = dispute.ToLegacyEntry();

        // Assert
        entry.RuleReferences.Should().BeEmpty();
    }

    #endregion

    #region SetRelatedDisputeIds

    [Fact]
    public void SetRelatedDisputeIds_ShouldSetIds()
    {
        // Arrange
        var dispute = CreateDispute();
        var ids = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };

        // Act
        dispute.SetRelatedDisputeIds(ids);

        // Assert
        dispute.RelatedDisputeIds.Count.Should().Be(2);
    }

    [Fact]
    public void SetRelatedDisputeIds_CalledTwice_ShouldReplace()
    {
        // Arrange
        var dispute = CreateDispute();
        dispute.SetRelatedDisputeIds(new List<Guid> { Guid.NewGuid() });

        // Act
        var newIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        dispute.SetRelatedDisputeIds(newIds);

        // Assert
        dispute.RelatedDisputeIds.Count.Should().Be(3);
    }

    #endregion

    #region DisputeVerdict value object

    [Fact]
    public void DisputeVerdict_EmptyReasoning_ShouldThrow()
    {
        var act = () =>
            new DisputeVerdict(RulingFor.Initiator, "", null, VerdictConfidence.High);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void DisputeVerdict_WhitespaceReasoning_ShouldThrow()
    {
        var act = () =>
            new DisputeVerdict(RulingFor.Initiator, "   ", null, VerdictConfidence.High);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void DisputeVerdict_ValidInput_ShouldCreateRecord()
    {
        var verdict = new DisputeVerdict(RulingFor.Respondent, "Rule says no", "p.3", VerdictConfidence.Medium);

        verdict.RulingFor.Should().Be(RulingFor.Respondent);
        verdict.Reasoning.Should().Be("Rule says no");
        verdict.Citation.Should().Be("p.3");
        verdict.Confidence.Should().Be(VerdictConfidence.Medium);
    }

    #endregion
}
