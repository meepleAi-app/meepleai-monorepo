namespace Api.BoundedContexts.UserLibrary.Domain.Repositories;

/// <summary>
/// The page that <c>GET /api/v1/library</c> actually serves (#4187).
/// The repository applies these bounds, and the paginated response must report the same
/// numbers. Before, it echoed the requested page size: a client asking for 500 received 100
/// entries, <c>TotalPages: 1</c> and <c>HasNextPage: false</c>, and could not tell that more existed.
/// </summary>
internal static class UserLibraryPaging
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public static int NormalizePage(int page) => page < 1 ? 1 : page;

    public static int NormalizePageSize(int pageSize) =>
        pageSize < 1 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);
}
