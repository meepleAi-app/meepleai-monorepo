namespace Api.BoundedContexts.Authentication.Application.Commands.Waitlist;

/// <summary>
/// Result of enrolling (or attempting to enroll) into the public Alpha waitlist.
/// </summary>
/// <param name="IsAlreadyOnList">True when the email was already enrolled — endpoint returns HTTP 409.</param>
/// <param name="Position">Either the freshly assigned position or, when already on the list, the existing position.</param>
/// <param name="EstimatedWeeks">Ceiling(Position / 100) — coarse ETA published to the user.</param>
internal record JoinWaitlistResult(
    bool IsAlreadyOnList,
    int Position,
    int EstimatedWeeks);
