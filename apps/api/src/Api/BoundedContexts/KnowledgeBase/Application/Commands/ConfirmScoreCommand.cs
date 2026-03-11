using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to confirm and record a previously parsed score.
/// Delegates to RecordLiveSessionScoreCommand via mediator.
/// </summary>
internal sealed record ConfirmScoreCommand : ICommand
{
    /// <summary>
    /// The live session ID.
    /// </summary>
    public required Guid SessionId { get; init; }

    /// <summary>
    /// The resolved player ID.
    /// </summary>
    public required Guid PlayerId { get; init; }

    /// <summary>
    /// The scoring dimension (e.g., "points", "roads").
    /// </summary>
    public required string Dimension { get; init; }

    /// <summary>
    /// The score value to record.
    /// </summary>
    public required int Value { get; init; }

    /// <summary>
    /// The round number for the score.
    /// </summary>
    public required int Round { get; init; }
}
