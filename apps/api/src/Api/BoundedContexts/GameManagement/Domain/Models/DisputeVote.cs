namespace Api.BoundedContexts.GameManagement.Domain.Models;

/// <summary>
/// Mutable model representing a player's vote on an AI dispute verdict.
/// Stored as JSONB payload on the RuleDispute aggregate.
/// </summary>
internal sealed class DisputeVote
{
    /// <summary>The player casting the vote.</summary>
    public Guid PlayerId { get; set; }

    /// <summary>Whether the player accepts the AI verdict.</summary>
    public bool AcceptsVerdict { get; set; }
}
