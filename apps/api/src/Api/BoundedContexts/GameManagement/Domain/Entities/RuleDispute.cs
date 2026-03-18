using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.GameManagement.Domain.Models;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.GameManagement.Domain.Entities;

/// <summary>
/// Aggregate root representing a structured rule dispute during a live game session.
/// Supports AI verdict, player voting, and backward compatibility with the legacy RuleDisputeEntry model.
/// </summary>
internal sealed class RuleDispute : AggregateRoot<Guid>
{
    public Guid SessionId { get; private set; }
    public Guid GameId { get; private set; }
    public Guid InitiatorPlayerId { get; private set; }
    public Guid? RespondentPlayerId { get; private set; }
    public string InitiatorClaim { get; private set; } = string.Empty;
    public string? RespondentClaim { get; private set; }
    public DisputeVerdict? Verdict { get; private set; }

    private readonly List<DisputeVote> _votes = new();
    public IReadOnlyList<DisputeVote> Votes => _votes.AsReadOnly();

    public DisputeOutcome FinalOutcome { get; private set; }
    public string? OverrideRule { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private readonly List<Guid> _relatedDisputeIds = new();
    public IReadOnlyList<Guid> RelatedDisputeIds => _relatedDisputeIds.AsReadOnly();

    /// <summary>EF Core constructor.</summary>
    private RuleDispute() : base() { }

    private RuleDispute(Guid id, Guid sessionId, Guid gameId, Guid initiatorPlayerId, string initiatorClaim)
        : base(id)
    {
        SessionId = sessionId;
        GameId = gameId;
        InitiatorPlayerId = initiatorPlayerId;
        InitiatorClaim = initiatorClaim;
        FinalOutcome = DisputeOutcome.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Factory method to open a new rule dispute.
    /// </summary>
    public static RuleDispute Open(Guid sessionId, Guid gameId, Guid initiatorPlayerId, string initiatorClaim)
    {
        if (sessionId == Guid.Empty)
            throw new ArgumentException("Session ID is required.", nameof(sessionId));
        if (gameId == Guid.Empty)
            throw new ArgumentException("Game ID is required.", nameof(gameId));
        if (initiatorPlayerId == Guid.Empty)
            throw new ArgumentException("Initiator player ID is required.", nameof(initiatorPlayerId));
        if (string.IsNullOrWhiteSpace(initiatorClaim))
            throw new ArgumentException("Initiator claim is required.", nameof(initiatorClaim));

        return new RuleDispute(Guid.NewGuid(), sessionId, gameId, initiatorPlayerId, initiatorClaim);
    }

    /// <summary>
    /// Adds the respondent's counter-claim.
    /// </summary>
    public void AddRespondentClaim(Guid respondentPlayerId, string claim)
    {
        if (respondentPlayerId == Guid.Empty)
            throw new ArgumentException("Respondent player ID is required.", nameof(respondentPlayerId));
        if (string.IsNullOrWhiteSpace(claim))
            throw new ArgumentException("Respondent claim is required.", nameof(claim));

        RespondentPlayerId = respondentPlayerId;
        RespondentClaim = claim;
    }

    /// <summary>
    /// Sets the AI arbitrator's verdict. Does NOT change FinalOutcome (that requires TallyVotes).
    /// </summary>
    public void SetVerdict(DisputeVerdict verdict)
    {
        Verdict = verdict ?? throw new ArgumentNullException(nameof(verdict));
    }

    /// <summary>
    /// Links related dispute IDs for cross-referencing.
    /// </summary>
    public void SetRelatedDisputeIds(List<Guid> ids)
    {
        _relatedDisputeIds.Clear();
        if (ids is not null)
            _relatedDisputeIds.AddRange(ids);
    }

    /// <summary>
    /// Records a player's vote on the current verdict.
    /// </summary>
    public void CastVote(Guid playerId, bool acceptsVerdict)
    {
        if (playerId == Guid.Empty)
            throw new ArgumentException("Player ID is required.", nameof(playerId));

        if (_votes.Any(v => v.PlayerId == playerId))
            throw new InvalidOperationException($"Player {playerId} has already voted on this dispute.");

        _votes.Add(new DisputeVote { PlayerId = playerId, AcceptsVerdict = acceptsVerdict });
    }

    /// <summary>
    /// Tallies votes and determines the final outcome.
    /// Majority accepts -> VerdictAccepted; otherwise -> VerdictOverridden.
    /// Does NOT raise event — call <see cref="RaiseResolvedEvent"/> after setting override rule.
    /// </summary>
    public void TallyVotes()
    {
        if (Verdict is null)
            throw new InvalidOperationException("Cannot tally votes before a verdict has been set.");

        if (_votes.Count == 0)
            throw new InvalidOperationException("Cannot tally votes when no votes have been cast.");

        var accepts = _votes.Count(v => v.AcceptsVerdict);
        var rejects = _votes.Count - accepts;

        FinalOutcome = accepts > rejects
            ? DisputeOutcome.VerdictAccepted
            : DisputeOutcome.VerdictOverridden;
    }

    /// <summary>
    /// Raises the StructuredDisputeResolvedEvent. Call after TallyVotes and SetOverrideRule
    /// so the event captures the final OverrideRule value.
    /// </summary>
    public void RaiseResolvedEvent()
    {
        if (FinalOutcome == DisputeOutcome.Pending)
            throw new InvalidOperationException("Cannot raise resolved event before tallying votes.");

        AddDomainEvent(new StructuredDisputeResolvedEvent(
            Id, SessionId, GameId, Verdict!, FinalOutcome, OverrideRule));
    }

    /// <summary>
    /// Sets a house rule override. Only valid when the verdict was overridden.
    /// </summary>
    public void SetOverrideRule(string rule)
    {
        if (string.IsNullOrWhiteSpace(rule))
            throw new ArgumentException("Override rule is required.", nameof(rule));

        if (FinalOutcome != DisputeOutcome.VerdictOverridden)
            throw new InvalidOperationException("Override rule can only be set when the verdict has been overridden.");

        OverrideRule = rule;
    }

    /// <summary>
    /// Converts this structured dispute to a legacy RuleDisputeEntry for backward-compatible
    /// JSONB append on LiveGameSession.DisputesJson.
    /// </summary>
    public RuleDisputeEntry ToLegacyEntry()
    {
        if (Verdict is null)
            throw new InvalidOperationException("Cannot produce a legacy entry before a verdict has been set.");

        var references = Verdict.Citation is not null
            ? new List<string> { Verdict.Citation }
            : new List<string>();

        return new RuleDisputeEntry(
            Id,
            InitiatorClaim,
            Verdict.Reasoning,
            references,
            InitiatorPlayerId.ToString(),
            CreatedAt);
    }
}
