namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;

/// <summary>
/// Fetches license metadata for a Wikimedia Commons file. Phase B foundation for
/// issue #1823 L2 cover enrichment. Consumed by the cover enrichment orchestrator
/// (M8) AFTER the Wikidata SPARQL pass (M3) has resolved a <c>wdt:P18</c> IRI to
/// a filename.
/// </summary>
/// <remarks>
/// <para>
/// ADR <c>adr-2026-06-09-wikidata-enrichment-architecture.md</c>:
/// <list type="bullet">
///   <item>DEC-3b — Commons fetcher is intentionally separate from
///   <c>WikidataCatalogProvider</c> so each client owns a single responsibility
///   (catalog seed vs license fetch). Both consume the shared
///   <see cref="IWikimediaRateLimiter"/> to coordinate the 5 RPS cap.</item>
///   <item>DEC-3c — License whitelist is enforced by
///   <see cref="LicenseValidator"/>; only the whitelisted licenses (PD / CC0 /
///   CC-BY / CC-BY-SA, any version) survive into <c>shared_games.cover_license</c>.</item>
///   <item>DEC-3e — 5 RPS shared cap across Wikidata SPARQL + Commons API,
///   enforced by <see cref="IWikimediaRateLimiter.AcquireAsync"/> consumed
///   BEFORE every HTTP call.</item>
/// </list>
/// </para>
/// </remarks>
internal interface IWikimediaCommonsClient
{
    /// <summary>
    /// Fetches license metadata for a Commons file via the imageinfo API.
    /// </summary>
    /// <param name="filename">
    /// Filename in URL-encoded form (e.g. <c>"Brass%20Birmingham%20cover.jpg"</c>) as
    /// extracted from a Wikidata <c>wdt:P18</c> IRI (Special:FilePath). The implementation
    /// internally calls <see cref="Uri.UnescapeDataString(string)"/> before constructing the API
    /// call, because the Commons API expects the raw filename, not the URL-encoded form.
    /// DO NOT pre-decode the filename upstream — pass it through as received from Wikidata
    /// to avoid double-decoding bugs (e.g. <c>%2520</c> → <c>%20</c> → <c>space</c>
    /// vs. <c>%20</c> → <c>space</c>).
    /// </param>
    /// <param name="ct">Cancellation token. <see cref="OperationCanceledException"/> is rethrown.</param>
    /// <returns>
    /// A <see cref="CommonsLicenseResult"/> that is either:
    /// <list type="bullet">
    ///   <item><see cref="CommonsLicenseResult.NotAvailable"/> when the file is missing,
    ///   the response is malformed, or the API returns 5xx (caller decides retry);</item>
    ///   <item><see cref="CommonsLicenseResult.Found"/> with the raw license, a
    ///   whitelist flag (<see cref="LicenseValidator.IsWhitelisted"/>), and an optional
    ///   attribution string (extracted from <c>extmetadata.Artist</c>).</item>
    /// </list>
    /// </returns>
    Task<CommonsLicenseResult> FetchLicenseAsync(string filename, CancellationToken ct);

    /// <summary>
    /// Issue #1823 Phase B M8 — downloads the raw image bytes for a Commons file
    /// via the <c>Special:FilePath/{filename}</c> redirect endpoint. Consumed by
    /// the cover enrichment orchestrator AFTER the M4 license check succeeds, so
    /// only whitelisted images incur the (possibly large) image-download cost.
    /// </summary>
    /// <param name="filename">
    /// Same URL-encoded contract as <see cref="FetchLicenseAsync"/>: filename
    /// arrives in the raw form extracted from a Wikidata <c>wdt:P18</c> IRI.
    /// The implementation decodes via <see cref="Uri.UnescapeDataString(string)"/>
    /// then re-escapes when constructing the request URL — DO NOT pre-decode
    /// upstream (same double-decoding pitfall documented on FetchLicenseAsync).
    /// </param>
    /// <param name="ct">Cancellation token. <see cref="OperationCanceledException"/> is rethrown.</param>
    /// <returns>
    /// The raw image bytes on success, or <see langword="null"/> when:
    /// <list type="bullet">
    ///   <item><paramref name="filename"/> is null/empty/whitespace;</item>
    ///   <item>the Commons CDN returns 404 (file deleted);</item>
    ///   <item>the Commons CDN returns 5xx (caller decides retry);</item>
    ///   <item>the response body is empty.</item>
    /// </list>
    /// </returns>
    /// <remarks>
    /// Consumes the shared <see cref="IWikimediaRateLimiter"/> token bucket
    /// BEFORE issuing the HTTP request (DEC-3e). HttpClient follows the
    /// <c>Special:FilePath/{filename}</c> 302 redirect to
    /// <c>upload.wikimedia.org/...</c> automatically (default redirect policy).
    /// </remarks>
    Task<byte[]?> FetchImageBytesAsync(string filename, CancellationToken ct);
}
