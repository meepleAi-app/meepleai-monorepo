namespace Api.BoundedContexts.UserLibrary.Domain.Repositories;

/// <summary>
/// When a library game "has a KB": at least one PDF of the shared game, not tied to a private
/// game, whose processing completed. <c>GetUserLibraryQueryHandler</c> computes <c>HasKb</c>
/// with it in memory, and the repository filters <c>hasKb</c> with it in SQL (#4227): the two
/// must say the same thing, so the states live here once.
/// </summary>
internal static class LibraryKbStates
{
    /// <summary>
    /// Completed processing states, lower-case. <c>ProcessingState</c> is stored as the enum
    /// member name (<c>Ready</c>); legacy rows carry <c>Indexed</c> or <c>completed</c>.
    /// </summary>
    public static readonly string[] CompletedLowerCase = ["ready", "indexed", "completed"];

    public static bool IsCompleted(string? state) =>
        state is not null && CompletedLowerCase.Contains(state, StringComparer.OrdinalIgnoreCase);
}
