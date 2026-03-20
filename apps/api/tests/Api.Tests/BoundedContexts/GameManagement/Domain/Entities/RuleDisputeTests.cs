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
        Assert.Null(dispute.RespondentPlayerId);
        Assert.Null(dispute.RespondentClaim);
        Assert.Null(dispute.Verdict);
        Assert.Empty(dispute.Votes);
        dispute.FinalOutcome.Should().Be(DisputeOutcome.Pending);
        Assert.Null(dispute.OverrideRule);
        Assert.True(dispute.CreatedAt <= DateTime.UtcNow);
        Assert.Empty(dispute.RelatedDisputeIds);
    }

    [Fact]
    public void Open_EmptySessionId_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            RuleDispute.Open(Guid.Empty, GameId, InitiatorId, "claim"));
    }

    [Fact]
    public void Open_EmptyGameId_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            RuleDispute.Open(SessionId, Guid.Empty, InitiatorId, "claim"));
    }

    [Fact]
    public void Open_EmptyInitiatorPlayerId_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            RuleDispute.Open(SessionId, GameId, Guid.Empty, "claim"));
    }

    [Fact]
    public void Open_NullClaim_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            RuleDispute.Open(SessionId, GameId, InitiatorId, null!));
    }

    [Fact]
    public void Open_WhitespaceClaim_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            RuleDispute.Open(SessionId, GameId, InitiatorId, "   "));
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
        Assert.Equal("No, only 1 card per turn", dispute.RespondentClaim);
    }

    [Fact]
    public void AddRespondentClaim_NullClaim_ShouldThrow()
    {
        var dispute = CreateDispute();
        Assert.Throws<ArgumentException>(() =>
            dispute.AddRespondentClaim(RespondentId, null!));
    }

    [Fact]
    public void AddRespondentClaim_EmptyPlayerId_ShouldThrow()
    {
        var dispute = CreateDispute();
        Assert.Throws<ArgumentException>(() =>
            dispute.AddRespondentClaim(Guid.Empty, "counter claim"));
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
        Assert.NotNull(dispute.Verdict);
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
        Assert.True(dispute.Votes[0].AcceptsVerdict);
    }

    [Fact]
    public void CastVote_DuplicatePlayer_ShouldThrow()
    {
        // Arrange
        var dispute = CreateDispute();
        var playerId = Guid.NewGuid();
        dispute.CastVote(playerId, true);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            dispute.CastVote(playerId, false));
    }

    [Fact]
    public void CastVote_EmptyPlayerId_ShouldThrow()
    {
        var dispute = CreateDispute();
        Assert.Throws<ArgumentException>(() =>
            dispute.CastVote(Guid.Empty, true));
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
        var resolvedEvent = Assert.IsType<StructuredDisputeResolvedEvent>(domainEvent);
        resolvedEvent.DisputeId.Should().Be(dispute.Id);
        resolvedEvent.SessionId.Should().Be(SessionId);
        resolvedEvent.GameId.Should().Be(GameId);
        Assert.NotNull(resolvedEvent.Verdict);
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

        Assert.Throws<InvalidOperationException>(() =>
            dispute.SetOverrideRule("some rule"));
    }

    [Fact]
    public void SetOverrideRule_WhenVerdictAccepted_ShouldThrow()
    {
        var dispute = CreateDispute();
        dispute.SetVerdict(CreateVerdict());
        dispute.CastVote(Guid.NewGuid(), true);
        dispute.TallyVotes();

        Assert.Throws<InvalidOperationException>(() =>
            dispute.SetOverrideRule("some rule"));
    }

    [Fact]
    public void SetOverrideRule_EmptyRule_ShouldThrow()
    {
        var dispute = CreateDispute();
        dispute.SetVerdict(CreateVerdict());
        dispute.CastVote(Guid.NewGuid(), false);
        dispute.TallyVotes();

        Assert.Throws<ArgumentException>(() =>
            dispute.SetOverrideRule("   "));
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
        Assert.Equal("Yes, rule 3.1 allows it", entry.Verdict);
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
        Assert.Empty(entry.RuleReferences);
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
        Assert.Throws<ArgumentException>(() =>
            new DisputeVerdict(RulingFor.Initiator, "", null, VerdictConfidence.High));
    }

    [Fact]
    public void DisputeVerdict_WhitespaceReasoning_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            new DisputeVerdict(RulingFor.Initiator, "   ", null, VerdictConfidence.High));
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
