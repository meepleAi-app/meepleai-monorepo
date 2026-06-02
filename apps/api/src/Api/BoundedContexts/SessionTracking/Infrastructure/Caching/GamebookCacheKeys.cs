using System.Globalization;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Caching;

/// <summary>
/// DEC-6 (#1559): Stable cache key namespace for gamebook caching.
/// Keys: "gamebook:&lt;resource&gt;:&lt;id&gt;".
/// </summary>
internal static class GamebookCacheKeys
{
    public static string PhotoOcr(Guid photoId)
        => string.Create(CultureInfo.InvariantCulture, $"gamebook:photo-ocr:{photoId:D}");
}
