namespace Api.BoundedContexts.UserLibrary.Application.Commands.PrivateGames;

/// <summary>
/// #5079: the error codes of a private game's proposal to the catalog, which the frontend reads (<c>error.code</c>)
/// to tell the user why a proposal, a deletion or a withdrawal was refused.
/// </summary>
internal static class PrivateGameProposalCodes
{
    /// <summary>A proposal for the game is pending, in review or waiting for changes.</summary>
    public const string AlreadyOpen = "proposal_already_open";

    /// <summary>A proposal for the game was approved: the game is already in the catalog.</summary>
    public const string AlreadyApproved = "proposal_already_approved";

    /// <summary>The user is at the share-request limits (pending, monthly, or the pause after a rejection).</summary>
    public const string LimitReached = "share_request_limit_reached";

    /// <summary>An admin holds the proposal in review: it can be withdrawn after the decision.</summary>
    public const string InReview = "proposal_in_review";
}
