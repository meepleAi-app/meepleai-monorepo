namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Providers;

/// <summary>
/// Hard-coded whitelist of BGG XML API2 fields that may be imported by
/// MeepleAI. ANY change requires legal review per spec §8.5.6 pre-rollout
/// checklist. Unit test <c>BggImportFieldFilterTests</c> guards drift.
/// Issue #1903 / spec §7.2.
/// </summary>
internal static class BggImportFieldFilter
{
    public static readonly IReadOnlySet<string> AllowedFields = new HashSet<string>(StringComparer.Ordinal)
    {
        "name",
        "yearpublished",
        "minplayers", "maxplayers",
        "playingtime", "minplaytime", "maxplaytime",
        "minage",
        "link[type=boardgamedesigner]",
        "link[type=boardgamepublisher]",
        "link[type=boardgameartist]",
        "link[type=boardgamemechanic]",
        "link[type=boardgamecategory]",
        "link[type=boardgamefamily]",
    };

    /// <summary>
    /// Fields explicitly forbidden. Even if BGG response contains them, they
    /// are never mapped. Reason: copyright (description/comments — Feist),
    /// publisher copyright (image/thumbnail — handled by #1821/#1823), or
    /// DB sui generis EU (statistics — competing market).
    /// </summary>
    public static readonly IReadOnlySet<string> ForbiddenFields = new HashSet<string>(StringComparer.Ordinal)
    {
        "description",
        "image", "thumbnail",
        "statistics",
        "comments",
        "videos",
    };
}
