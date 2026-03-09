using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to request an arbiter verdict on a dispute between players.
/// The arbiter searches the rulebook, cites exact passages, and provides a verdict.
/// Issue #5585: Arbiter Mode — dispute arbitration with citations and verdict.
/// </summary>
public record AskArbiterCommand : IRequest<ArbiterVerdictDto>
{
    /// <summary>
    /// The agent to use for the arbiter query (must have KB documents).
    /// </summary>
    public required Guid AgentId { get; init; }

    /// <summary>
    /// The game session context for the dispute.
    /// </summary>
    public required Guid SessionId { get; init; }

    /// <summary>
    /// Description of the disputed situation.
    /// </summary>
    public required string Situation { get; init; }

    /// <summary>
    /// Position of player/side A in the dispute.
    /// </summary>
    public required string PositionA { get; init; }

    /// <summary>
    /// Position of player/side B in the dispute.
    /// </summary>
    public required string PositionB { get; init; }

    /// <summary>
    /// The authenticated user making the request.
    /// </summary>
    public Guid UserId { get; init; }
}
