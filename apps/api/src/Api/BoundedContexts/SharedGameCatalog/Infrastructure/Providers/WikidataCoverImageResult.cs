namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Providers;

/// <summary>
/// Result of <see cref="WikidataCatalogProvider.FetchCoverImageAsync(string, CancellationToken)"/>.
/// Encapsulates the optional Wikidata Commons filename (P18 property) and the canonical
/// source URL for attribution purposes.
/// </summary>
/// <param name="Filename">
/// Raw, URL-encoded Commons filename (e.g. <c>Brass%20Birmingham%20cover.jpg</c>). Preserved
/// in encoded form because downstream Commons API + R2 keys expect the raw Wikidata IRI
/// convention. <see langword="null"/> when no <c>wdt:P18</c> claim exists for the QID.
/// </param>
/// <param name="SourceUrl">
/// Canonical Wikidata entity URL (e.g. <c>https://www.wikidata.org/wiki/Q98056728</c>).
/// Always non-null — used by callers for provenance even when no image was found.
/// </param>
/// <remarks>
/// Issue #1823 Phase B M3. ADR DEC-3a (extend existing provider).
/// </remarks>
public sealed record WikidataCoverImageResult(string? Filename, string SourceUrl)
{
    /// <summary>Convenience flag — <see langword="true"/> when <see cref="Filename"/> is non-null.</summary>
    public bool HasImage => Filename is not null;

    /// <summary>Constructs a result for the success case (image found).</summary>
    public static WikidataCoverImageResult Found(string filename, string sourceUrl) =>
        new(filename, sourceUrl);

    /// <summary>Constructs a result for the not-found case (QID invalid, missing P18, or HTTP error).</summary>
    public static WikidataCoverImageResult NotFound(string qid) =>
        new(null, $"https://www.wikidata.org/wiki/{qid}");
}
