using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Tests.Constants;
using Xunit;

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
        Assert.NotEqual(Guid.Empty, dispute.Id);
        Assert.Equal(SessionId, dispute.SessionId);
        Assert.Equal(GameId, dispute.GameId);
        Assert.Equal(InitiatorId, dispute.InitiatorPlayerId);
        Assert.Equal("I can play 2 cards per turn", dispute.InitiatorClaim);
        Assert.Null(dispute.RespondentPlayerId);
        Assert.Null(dispute.RespondentClaim);
        Assert.Null(dispute.Verdict);
        Assert.Empty(dispute.Votes);
        Assert.Equal(DisputeOutcome.Pending, dispute.FinalOutcome);
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
        Assert.Equal(RespondentId, dispute.RespondentPlayerId);
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
        Assert.Equal(RulingFor.Initiator, dispute.Verdict.RulingFor);
        Assert.Equal("Rule 3.1 allows 2 cards", dispute.Verdict.Reasoning);
        Assert.Equal(DisputeOutcome.Pending, dispute.FinalOutcome);
    }

    [Fact]
    public void SetVerdict_Null_ShouldThrow()
    {
        var dispute = CreateDispute();
        Assert.Throws<ArgumentNullException>(() => dispute.SetVerdict(null!));
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
        Assert.Single(dispute.Votes);
        Assert.Equal(playerId, dispute.Votes[0].PlayerId);
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
        Assert.Equal(DisputeOutcome.VerdictAccepted, dispute.FinalOutcome);
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
        Assert.Equal(DisputeOutcome.VerdictOverridden, dispute.FinalOutcome);
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
        Assert.Equal(DisputeOutcome.VerdictOverridden, dispute.FinalOutcome);
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
        var domainEvent = Assert.Single(dispute.DomainEvents);
        var resolvedEvent = Assert.IsType<StructuredDisputeResolvedEvent>(domainEvent);
        Assert.Equal(dispute.Id, resolvedEvent.DisputeId);
        Assert.Equal(SessionId, resolvedEvent.SessionId);
        Assert.Equal(GameId, resolvedEvent.GameId);
        Assert.NotNull(resolvedEvent.Verdict);
        Assert.Equal(DisputeOutcome.VerdictAccepted, resolvedEvent.FinalOutcome);
    }

    [Fact]
    public void TallyVotes_NoVerdict_ShouldThrow()
    {
        var dispute = CreateDispute();
        dispute.CastVote(Guid.NewGuid(), true);

        Assert.Throws<InvalidOperationException>(() => dispute.TallyVotes());
    }

    [Fact]
    public void TallyVotes_NoVotes_ShouldThrow()
    {
        var dispute = CreateDispute();
        dispute.SetVerdict(CreateVerdict());

        Assert.Throws<InvalidOperationException>(() => dispute.TallyVotes());
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
        Assert.Equal("House rule: 2 cards allowed on first turn only", dispute.OverrideRule);
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
        Assert.Equal(dispute.Id, entry.Id);
        Assert.Equal("Can I play 2 cards?", entry.Description);
        Assert.Equal("Yes, rule 3.1 allows it", entry.Verdict);
        Assert.Single(entry.RuleReferences);
        Assert.Contains("Page 5", entry.RuleReferences);
        Assert.Equal(InitiatorId.ToString(), entry.RaisedByPlayerName);
        Assert.Equal(dispute.CreatedAt, entry.Timestamp);
    }

    [Fact]
    public void ToLegacyEntry_WithoutVerdict_ShouldThrow()
    {
        // Arrange — no verdict set, so legacy entry cannot be produced
        // (RuleDisputeEntry requires a non-empty verdict)
        var dispute = CreateDispute("Some claim");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => dispute.ToLegacyEntry());
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
        Assert.Equal(2, dispute.RelatedDisputeIds.Count);
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
        Assert.Equal(3, dispute.RelatedDisputeIds.Count);
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

        Assert.Equal(RulingFor.Respondent, verdict.RulingFor);
        Assert.Equal("Rule says no", verdict.Reasoning);
        Assert.Equal("p.3", verdict.Citation);
        Assert.Equal(VerdictConfidence.Medium, verdict.Confidence);
    }

    #endregion
}
