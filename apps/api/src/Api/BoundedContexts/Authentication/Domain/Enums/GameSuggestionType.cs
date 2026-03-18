namespace Api.BoundedContexts.Authentication.Domain.Enums;

/// <summary>
/// Indicates how a game suggestion was associated with an invitation.
/// </summary>
internal enum GameSuggestionType
{
    /// <summary>Game pre-added to the invitee's library upon activation.</summary>
    PreAdded = 0,

    /// <summary>Game suggested to the invitee (not auto-added).</summary>
    Suggested = 1
}
