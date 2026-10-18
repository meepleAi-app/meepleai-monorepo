using Api.BoundedContexts.UserLibrary.Domain.Entities;

namespace Api.BoundedContexts.UserLibrary.Domain.Repositories;

internal static class UserLibraryRepositoryExtensions
{
    /// <summary>
    /// Every library entry of a user, read page by page (#4187).
    /// Callers that wanted "the whole library" used to ask for one large page (1000 for the
    /// downgrade preview, 100 for the shared view): the repository serves at most
    /// <see cref="UserLibraryPaging.MaxPageSize"/>, so they silently worked on the first 100.
    /// </summary>
    /// <returns>The entries and the total the repository declared.</returns>
    public static async Task<(IReadOnlyList<UserLibraryEntry> Entries, int Total)> GetAllUserLibraryEntriesAsync(
        this IUserLibraryRepository repository,
        Guid userId,
        string sortBy,
        bool descending,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);

        var all = new List<UserLibraryEntry>();
        var page = 1;
        int total;
        do
        {
            var (entries, pageTotal) = await repository.GetUserLibraryPaginatedAsync(
                userId,
                search: null,
                favoritesOnly: null,
                stateFilter: null,
                hasKb: null,
                sortBy,
                descending,
                page,
                UserLibraryPaging.MaxPageSize,
                cancellationToken).ConfigureAwait(false);

            total = pageTotal;
            all.AddRange(entries);
            // The total and the pages are read at different moments: if entries disappear in
            // between, the total stays higher than what exists. An empty page ends the loop.
            if (entries.Count == 0) break;
            page++;
        } while (all.Count < total);

        return (all, total);
    }
}
