namespace Api.Infrastructure.Entities.GameManagement;

/// <summary>
/// Table of the structured rule dispute v2, whose code was retired by #4936. Nothing reads or writes it;
/// it stays mapped only so that the drop happens in its own delivery (#4960, expand/contract).
/// </summary>
public class RuleDisputeEntity
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Guid GameId { get; set; }
    public Guid InitiatorPlayerId { get; set; }
    public Guid? RespondentPlayerId { get; set; }
    public string InitiatorClaim { get; set; } = string.Empty;
    public string? RespondentClaim { get; set; }
    public string? VerdictJson { get; set; }  // JSONB for DisputeVerdict
    public string? VotesJson { get; set; }  // JSONB for List<DisputeVote>
    public int FinalOutcome { get; set; }  // DisputeOutcome enum as int
    public string? OverrideRule { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? RelatedDisputeIdsJson { get; set; }  // JSONB for List<Guid>

    // Navigation (optional)
    public LiveGameSessionEntity? Session { get; set; }
}
