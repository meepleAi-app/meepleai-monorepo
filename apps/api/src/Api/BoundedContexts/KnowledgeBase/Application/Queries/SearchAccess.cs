namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Who stands behind a <see cref="SearchQuery"/>: either a user, for whom
/// <c>SearchQueryHandler</c> passes the RAG gate before it searches, or a sender that has passed
/// the gate already.
/// </summary>
/// <remarks>
/// Issue #4294. The query used to carry an optional <c>UserId</c>, and the handler checked access
/// only when it was there: a sender that left it out skipped the check, and compiled. Two routes
/// did exactly that and returned the passages of games the caller could not query. There is no
/// default here on purpose, so every sender has to say which of the two it is.
/// </remarks>
internal sealed record SearchAccess
{
    private SearchAccess(Guid? userId, string? userRole)
    {
        UserId = userId;
        UserRole = userRole;
    }

    /// <summary>The user to check, or null when the sender has already passed the gate.</summary>
    public Guid? UserId { get; }

    /// <summary>The role of <see cref="UserId"/>, as the session reports it.</summary>
    public string? UserRole { get; }

    /// <summary>The handler checks that this user may query the game before it searches.</summary>
    public static SearchAccess OfUser(Guid userId, string? userRole) => new(userId, userRole);

    /// <summary>
    /// The sender has already passed the RAG gate for this game, or is reachable only through an
    /// admin route. The handler does not check again. Say where, next to the call.
    /// </summary>
    public static SearchAccess AlreadyChecked { get; } = new(null, null);
}
