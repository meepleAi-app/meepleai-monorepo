using Api.BoundedContexts.GameManagement.Domain.Entities.PauseSnapshot;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class PauseSnapshotTests
{
    private static readonly Guid ValidSessionId = Guid.NewGuid();
    private static readonly Guid ValidUserId = Guid.NewGuid();

    private static List<PlayerScoreSnapshot> SampleScores() =>
        new()
        {
            new PlayerScoreSnapshot("Alice", 42m, 1),
            new PlayerScoreSnapshot("Bob", 37m, 2),
        };

    private static List<Guid> SampleAttachmentIds() =>
        new() { Guid.NewGuid(), Guid.NewGuid() };

    private static RuleDisputeEntry SampleDispute() =>
        new(
            Guid.NewGuid(),
            "Can I play 2 cards per turn?",
            "No — the rulebook states 1 card per turn.",
            new List<string> { "Page 5" },
            "Alice",
            DateTime.UtcNow);

    // ─── Create with valid data ──────────────────────────────────────────────

    [Fact]
    public void Create_WithValidData_SetsLiveGameSessionId()
    {
        var snapshot = PauseSnapshot.Create(
            ValidSessionId, 3, "Round 3", SampleScores(), ValidUserId, false);

        Assert.Equal(ValidSessionId, snapshot.LiveGameSessionId);
    }

    [Fact]
    public void Create_WithValidData_SetsCurrentTurn()
    {
        var snapshot = PauseSnapshot.Create(
            ValidSessionId, 5, null, SampleScores(), ValidUserId, false);

        Assert.Equal(5, snapshot.CurrentTurn);
    }

    [Fact]
    public void Create_WithValidData_SetsCurrentPhase()
    {
        var snapshot = PauseSnapshot.Create(
            ValidSessionId, 1, "Setup", SampleScores(), ValidUserId, false);

        Assert.Equal("Setup", snapshot.CurrentPhase);
    }

    [Fact]
    public void Create_WithValidData_SetsPlayerScores()
    {
        var scores = SampleScores();
        var snapshot = PauseSnapshot.Create(
            ValidSessionId, 1, null, scores, ValidUserId, false);

        Assert.Equal(2, snapshot.PlayerScores.Count);
        Assert.Contains(snapshot.PlayerScores, s => s.PlayerName == "Alice" && s.Score == 42m);
        Assert.Contains(snapshot.PlayerScores, s => s.PlayerName == "Bob" && s.Score == 37m);
    }

    [Fact]
    public void Create_WithValidData_SetsSavedByUserId()
    {
        var snapshot = PauseSnapshot.Create(
            ValidSessionId, 1, null, SampleScores(), ValidUserId, false);

        Assert.Equal(ValidUserId, snapshot.SavedByUserId);
    }

    [Fact]
    public void Create_WithValidData_SetsIsAutoSave_True()
    {
        var snapshot = PauseSnapshot.Create(
            ValidSessionId, 1, null, SampleScores(), ValidUserId, true);

        Assert.True(snapshot.IsAutoSave);
    }

    [Fact]
    public void Create_WithValidData_SetsIsAutoSave_False()
    {
        var snapshot = PauseSnapshot.Create(
            ValidSessionId, 1, null, SampleScores(), ValidUserId, false);

        Assert.False(snapshot.IsAutoSave);
    }

    [Fact]
    public void Create_WithAttachmentIds_SetsAttachmentIds()
    {
        var ids = SampleAttachmentIds();
        var snapshot = PauseSnapshot.Create(
            ValidSessionId, 1, null, SampleScores(), ValidUserId, false,
            attachmentIds: ids);

        Assert.Equal(2, snapshot.AttachmentIds.Count);
        Assert.Equal(ids[0], snapshot.AttachmentIds[0]);
        Assert.Equal(ids[1], snapshot.AttachmentIds[1]);
    }

    [Fact]
    public void Create_WithDisputes_SetsDisputes()
    {
        var dispute = SampleDispute();
        var snapshot = PauseSnapshot.Create(
            ValidSessionId, 1, null, SampleScores(), ValidUserId, false,
            disputes: new List<RuleDisputeEntry> { dispute });

        Assert.Single(snapshot.Disputes);
        Assert.Equal(dispute.Description, snapshot.Disputes[0].Description);
    }

    [Fact]
    public void Create_WithGameStateJson_SetsGameStateJson()
    {
        const string json = """{"board":"state"}""";
        var snapshot = PauseSnapshot.Create(
            ValidSessionId, 1, null, SampleScores(), ValidUserId, false,
            gameStateJson: json);

        Assert.Equal(json, snapshot.GameStateJson);
    }

    [Fact]
    public void Create_GeneratesNonEmptyId()
    {
        var snapshot = PauseSnapshot.Create(
            ValidSessionId, 1, null, SampleScores(), ValidUserId, false);

        Assert.NotEqual(Guid.Empty, snapshot.Id);
    }

    // ─── SavedAt ────────────────────────────────────────────────────────────

    [Fact]
    public void Create_SetsSavedAt_ApproximatelyNow()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var snapshot = PauseSnapshot.Create(
            ValidSessionId, 1, null, SampleScores(), ValidUserId, false);
        var after = DateTime.UtcNow.AddSeconds(1);

        Assert.InRange(snapshot.SavedAt, before, after);
    }

    // ─── Null optional parameters ─────────────────────────────────────────────

    [Fact]
    public void Create_WithNullPlayerScores_DefaultsToEmptyList()
    {
        var snapshot = PauseSnapshot.Create(
            ValidSessionId, 1, null, null!, ValidUserId, false);

        Assert.NotNull(snapshot.PlayerScores);
        Assert.Empty(snapshot.PlayerScores);
    }

    [Fact]
    public void Create_WithNullAttachmentIds_DefaultsToEmptyList()
    {
        var snapshot = PauseSnapshot.Create(
            ValidSessionId, 1, null, SampleScores(), ValidUserId, false,
            attachmentIds: null);

        Assert.NotNull(snapshot.AttachmentIds);
        Assert.Empty(snapshot.AttachmentIds);
    }

    [Fact]
    public void Create_WithNullDisputes_DefaultsToEmptyList()
    {
        var snapshot = PauseSnapshot.Create(
            ValidSessionId, 1, null, SampleScores(), ValidUserId, false,
            disputes: null);

        Assert.NotNull(snapshot.Disputes);
        Assert.Empty(snapshot.Disputes);
    }

    [Fact]
    public void Create_WithNullCurrentPhase_IsNull()
    {
        var snapshot = PauseSnapshot.Create(
            ValidSessionId, 1, null, SampleScores(), ValidUserId, false);

        Assert.Null(snapshot.CurrentPhase);
    }

    // ─── AgentConversationSummary initially null ────────────────────────────

    [Fact]
    public void Create_AgentConversationSummary_IsInitiallyNull()
    {
        var snapshot = PauseSnapshot.Create(
            ValidSessionId, 1, null, SampleScores(), ValidUserId, false);

        Assert.Null(snapshot.AgentConversationSummary);
    }

    // ─── UpdateSummary ───────────────────────────────────────────────────────

    [Fact]
    public void UpdateSummary_SetsAgentConversationSummary()
    {
        var snapshot = PauseSnapshot.Create(
            ValidSessionId, 1, null, SampleScores(), ValidUserId, false);

        snapshot.UpdateSummary("Players are winning. Board state: round 3.");

        Assert.Equal("Players are winning. Board state: round 3.", snapshot.AgentConversationSummary);
    }

    [Fact]
    public void UpdateSummary_CanOverwritePreviousSummary()
    {
        var snapshot = PauseSnapshot.Create(
            ValidSessionId, 1, null, SampleScores(), ValidUserId, false);

        snapshot.UpdateSummary("First summary.");
        snapshot.UpdateSummary("Updated summary.");

        Assert.Equal("Updated summary.", snapshot.AgentConversationSummary);
    }

    [Fact]
    public void UpdateSummary_WithEmptyString_Throws()
    {
        var snapshot = PauseSnapshot.Create(
            ValidSessionId, 1, null, SampleScores(), ValidUserId, false);

        Assert.Throws<ArgumentException>(() => snapshot.UpdateSummary(string.Empty));
    }

    [Fact]
    public void UpdateSummary_WithWhitespace_Throws()
    {
        var snapshot = PauseSnapshot.Create(
            ValidSessionId, 1, null, SampleScores(), ValidUserId, false);

        Assert.Throws<ArgumentException>(() => snapshot.UpdateSummary("   "));
    }

    // ─── IsAutoSave flag ─────────────────────────────────────────────────────

    [Fact]
    public void Create_IsAutoSave_PreservedCorrectly_WhenTrue()
    {
        var snapshot = PauseSnapshot.Create(
            ValidSessionId, 1, null, SampleScores(), ValidUserId, isAutoSave: true);

        Assert.True(snapshot.IsAutoSave);
    }

    [Fact]
    public void Create_IsAutoSave_PreservedCorrectly_WhenFalse()
    {
        var snapshot = PauseSnapshot.Create(
            ValidSessionId, 1, null, SampleScores(), ValidUserId, isAutoSave: false);

        Assert.False(snapshot.IsAutoSave);
    }

    // ─── Guard clauses ───────────────────────────────────────────────────────

    [Fact]
    public void Create_WithEmptySessionId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            PauseSnapshot.Create(Guid.Empty, 1, null, SampleScores(), ValidUserId, false));
    }

    [Fact]
    public void Create_WithEmptyUserId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            PauseSnapshot.Create(ValidSessionId, 1, null, SampleScores(), Guid.Empty, false));
    }

    [Fact]
    public void Create_WithNegativeTurn_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PauseSnapshot.Create(ValidSessionId, -1, null, SampleScores(), ValidUserId, false));
    }

    [Fact]
    public void Create_WithZeroTurn_Succeeds()
    {
        // Turn 0 is valid (session just started)
        var snapshot = PauseSnapshot.Create(
            ValidSessionId, 0, null, SampleScores(), ValidUserId, false);

        Assert.Equal(0, snapshot.CurrentTurn);
    }

    // ─── PlayerScoreSnapshot record ──────────────────────────────────────────

    [Fact]
    public void PlayerScoreSnapshot_StoresPlayerNameAndScore()
    {
        var record = new PlayerScoreSnapshot("Marco", 99.5m, 42);

        Assert.Equal("Marco", record.PlayerName);
        Assert.Equal(99.5m, record.Score);
        Assert.Equal(42, record.PlayerId);
    }

    [Fact]
    public void PlayerScoreSnapshot_WithNullPlayerId_IsOptional()
    {
        var record = new PlayerScoreSnapshot("Guest", 10m);

        Assert.Equal("Guest", record.PlayerName);
        Assert.Null(record.PlayerId);
    }
}
