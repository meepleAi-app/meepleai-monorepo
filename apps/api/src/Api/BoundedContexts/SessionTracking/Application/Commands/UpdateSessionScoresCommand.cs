using Api.BoundedContexts.SessionTracking.Domain.Enums;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Asse A semantic alignment #1896 (T10, DEC-1): polymorphic mid-game score update.
///
/// <para>Distinct from <see cref="UpdateScoreCommand"/> (which adds a single per-participant
/// score entry to the score history) — this command replaces the session's polymorphic
/// <c>ScoringType</c> + <c>ScoreData</c> payload atomically. Validation is delegated to
/// FluentValidation + <c>ScoringStrategyFactory</c> (T6+T7+T8) so each <see cref="ScoreType"/>
/// enforces its own schema (Points / BinaryWin / Objectives / Ranking).</para>
///
/// <para>Use <c>FinalizeSessionCommand</c> for final-rank submission with session-status transition
/// to <c>Finalized</c>; this command keeps the session in its current status.</para>
/// </summary>
public record UpdateSessionScoresCommand(
    Guid SessionId,
    ScoreType ScoringType,
    string ScoreData
) : IRequest<UpdateSessionScoresResult>;

/// <summary>
/// Result payload for <see cref="UpdateSessionScoresCommand"/>: echoes the persisted
/// scoring type and surfaces the computed winner (when the strategy can determine one).
/// </summary>
public record UpdateSessionScoresResult(
    Guid SessionId,
    ScoreType ScoringType,
    Guid? ComputedWinnerId);
