using Api.BoundedContexts.GameManagement.Domain.Enums;

namespace Api.BoundedContexts.GameManagement.Domain.Exceptions;

/// <summary>
/// The machine-readable codes of the 409s a live session answers when an action does not fit its
/// <see cref="LiveSessionStatus"/> (#4844). One code per CAUSE, not per action: «record a score» and
/// «end the game» on a session nobody started are the same thing for the player. The HTTP body carries
/// the code in <c>error</c> (the middleware copies <c>HttpException.ErrorCode</c> there) and the client
/// translates by code; the English sentence stays in the message, for the logs.
/// <para>
/// Style: lower snake case, like <c>turn_changed</c>, <c>score_changed</c> and <c>kb_not_ready</c>.
/// <see cref="MaxLiveSessionsExceededException.Code"/> predates the rule and keeps its upper case,
/// because the client compares the exact string; <c>ConflictErrorCodeStyleArchitectureTests</c> pins
/// both facts.
/// </para>
/// </summary>
public static class LiveSessionConflictCodes
{
    /// <summary>Created or Setup, on an action that wants the game started.</summary>
    public const string SessionNotStarted = "session_not_started";

    /// <summary>Paused, on an action that wants the game running (a rule dispute, a turn, a pause).</summary>
    public const string SessionPaused = "session_paused";

    /// <summary>Completed: nothing changes a finished game.</summary>
    public const string SessionCompleted = "session_completed";

    /// <summary>InProgress on <c>start</c>.</summary>
    public const string SessionAlreadyStarted = "session_already_started";

    /// <summary>InProgress on <c>resume</c>.</summary>
    public const string SessionNotPaused = "session_not_paused";

    /// <summary>
    /// The code for a <paramref name="status"/> the action rejects. Deciding WHICH statuses are rejected
    /// is the caller's guard; this only names the cause. InProgress is the one state whose rejection
    /// depends on the action (<c>start</c> → already started, <c>resume</c> → not paused), hence
    /// <paramref name="codeWhenInProgress"/>.
    /// </summary>
    public static string ForRejectedStatus(LiveSessionStatus status, string codeWhenInProgress)
    {
        return status switch
        {
            LiveSessionStatus.Created or LiveSessionStatus.Setup => SessionNotStarted,
            LiveSessionStatus.Paused => SessionPaused,
            LiveSessionStatus.Completed => SessionCompleted,
            LiveSessionStatus.InProgress => codeWhenInProgress,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown live session status"),
        };
    }
}
