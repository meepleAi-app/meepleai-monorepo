namespace Api.Infrastructure.Entities.GameManagement;

/// <summary>
/// Infrastructure entity for RuleDispute aggregate.
/// Maps domain RuleDispute to database table for structured rule dispute persistence.
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
