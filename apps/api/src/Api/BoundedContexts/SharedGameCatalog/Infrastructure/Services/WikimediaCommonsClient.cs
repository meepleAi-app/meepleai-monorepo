using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Api.Observability;
using Api.SharedKernel.Infrastructure.Http;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;

/// <summary>
/// Typed <see cref="HttpClient"/> wrapper that calls the Wikimedia Commons
/// imageinfo API to retrieve license metadata for a file referenced by a
/// Wikidata <c>wdt:P18</c> IRI. Phase B foundation for issue #1823 (ADR DEC-3b /
/// DEC-3c / DEC-3e).
/// </summary>
/// <remarks>
/// <para>
/// API contract: <c>GET /w/api.php?action=query&amp;prop=imageinfo&amp;iiprop=extmetadata&amp;titles=File:{name}&amp;format=json</c>.
/// The Commons API returns a <c>query.pages.{pageId}.imageinfo[0].extmetadata</c>
/// blob; this client extracts only <c>LicenseShortName</c> and <c>Artist</c>,
/// validates the license against the DEC-3c whitelist via
/// <see cref="LicenseValidator"/>, and returns a structured result.
/// </para>
/// <para>
/// Failure semantics: 5xx responses, malformed JSON, missing pages, and missing
/// metadata all collapse into <see cref="CommonsLicenseResult.NotAvailable"/>
/// with a warning log line. The M8 orchestrator decides whether to skip the
/// game or queue a retry; this client never propagates exceptions for transient
/// failures. <see cref="OperationCanceledException"/> is the sole exception
/// rethrown, so caller cancellation is respected.
/// </para>
/// </remarks>
internal sealed class WikimediaCommonsClient : IWikimediaCommonsClient
{
    private const string ApiPath = "w/api.php";

    /// <summary>
    /// #3495 Slice D (finding H1) — ceilings and budgets for the two Commons calls, enforced by
    /// <see cref="HardenedRedirectFetch"/>. Before this slice the image download read the body with
    /// no limit at all, so a hostile or accidental multi-GB file was an unbounded allocation.
    /// The image cap is deliberately generous: Commons legitimately serves high-resolution
    /// photographs, and the WebP re-encode downstream is what normalizes them.
    /// </summary>
    private const long MaxImageBytes = 32 * 1024 * 1024;
    private const long MaxApiResponseBytes = 2 * 1024 * 1024;
    private static readonly TimeSpan ImageDeadline = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ApiDeadline = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Strips HTML tags from the Commons <c>Artist</c> field, which is returned
    /// as HTML fragment (typically a <c>&lt;a&gt;...&lt;/a&gt;</c> wrapping the user
    /// name). Anchored, non-greedy, DoS-safe via a 200ms timeout.
    /// </summary>
    private static readonly Regex HtmlTagPattern = new(
        @"<[^>]+>",
        RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(200));

    private readonly HttpClient _http;
    private readonly IWikimediaRateLimiter _rateLimiter;
    private readonly ILogger<WikimediaCommonsClient> _logger;

    public WikimediaCommonsClient(
        HttpClient http,
        IWikimediaRateLimiter rateLimiter,
        ILogger<WikimediaCommonsClient> logger)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<CommonsLicenseResult> FetchLicenseAsync(string filename, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(filename))
        {
            return CommonsLicenseResult.NotAvailable();
        }

        // DEC-3b pitfall: the filename arrives URL-encoded (the wdt:P18 IRI
        // routes through Special:FilePath, which percent-encodes spaces and
        // diacritics). The imageinfo API expects the raw filename as the
        // titles= value (the query-string encoding wraps it once, NOT twice).
        var decoded = Uri.UnescapeDataString(filename);
        var title = "File:" + decoded;

        // DEC-3e: acquire BEFORE the HTTP call so the 5 RPS token bucket is
        // consumed even when the upstream call ultimately fails (a 5xx still
        // counts against the rate cap from Wikimedia's perspective).
        await _rateLimiter.AcquireAsync(ct).ConfigureAwait(false);

        try
        {
            var path = $"{ApiPath}?action=query&prop=imageinfo&iiprop=extmetadata&titles={Uri.EscapeDataString(title)}&format=json";

            // #3495 Slice D: through the hardened gate — per-hop HTTPS/port re-validation, bounded
            // redirect follow, capped body, total deadline (the API answers 200 directly, but a
            // hijacked/misconfigured redirect must not be followed blindly either).
            var body = await HardenedRedirectFetch.FetchAsync(
                _http,
                path,
                MaxApiResponseBytes,
                MeepleAiMetrics.EgressSinks.Wikimedia,
                ApiDeadline,
                configureRequest: req => req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json")),
                ct).ConfigureAwait(false);

            return ParseResponse(Encoding.UTF8.GetString(body), filename, decoded);
        }
        catch (HardenedFetchException ex)
        {
            _logger.LogWarning(ex,
                "Commons imageinfo blocked by the egress gate ({Reason}) for file '{Filename}' (decoded='{Decoded}').",
                ex.Reason,
                filename,
                decoded);
            return CommonsLicenseResult.NotAvailable();
        }
        // Issue #2157: real caller cancellation propagates so the batch handler
        // can decide to abort. HttpClient.Timeout firing also raises
        // TaskCanceledException (subclass of OperationCanceledException) WITHOUT
        // cancelling the caller token; we MUST distinguish via the
        // `when (ct.IsCancellationRequested)` guard or the batch handler will
        // mistake a per-game upstream timeout for a batch-wide user cancel.
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex,
                "Commons imageinfo TIMEOUT for file '{Filename}' (decoded='{Decoded}'). Caller decides retry.",
                filename,
                decoded);
            return CommonsLicenseResult.NotAvailable();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex,
                "Commons imageinfo network failure for file '{Filename}' (decoded='{Decoded}').",
                filename,
                decoded);
            return CommonsLicenseResult.NotAvailable();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex,
                "Commons imageinfo returned malformed JSON for file '{Filename}' (decoded='{Decoded}').",
                filename,
                decoded);
            return CommonsLicenseResult.NotAvailable();
        }
    }

    /// <inheritdoc />
    public async Task<byte[]?> FetchImageBytesAsync(string filename, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(filename))
        {
            return null;
        }

        // Same DEC-3b pitfall as FetchLicenseAsync: the filename arrives
        // URL-encoded from the wdt:P18 Special:FilePath IRI. Decode internally
        // before re-encoding for the request URL so callers do not have to
        // worry about double-encoding.
        var decoded = Uri.UnescapeDataString(filename);
        var path = $"wiki/Special:FilePath/{Uri.EscapeDataString(decoded)}";

        // DEC-3e: acquire BEFORE the HTTP call so the 5 RPS token bucket is
        // consumed even when the upstream call ultimately fails.
        await _rateLimiter.AcquireAsync(ct).ConfigureAwait(false);

        try
        {
            // #3495 Slice D (H1) — the Special:FilePath 302 to upload.wikimedia.org is now followed
            // MANUALLY through the gate instead of by the handler: each hop is re-validated
            // (HTTPS-only, default port) and the body is read under a ceiling with a total deadline.
            // This REPLACES the #1823 M8 invariant "do NOT disable redirects": the redirect is still
            // followed — the image bytes stay reachable — but through the guarded path.
            var bytes = await HardenedRedirectFetch.FetchAsync(
                _http,
                path,
                MaxImageBytes,
                MeepleAiMetrics.EgressSinks.Wikimedia,
                ImageDeadline,
                // Accept any image content; Commons serves PNG/JPEG/GIF/WebP/SVG/TIFF.
                configureRequest: req => req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/*")),
                ct).ConfigureAwait(false);

            if (bytes.Length == 0)
            {
                _logger.LogWarning(
                    "Commons FilePath returned empty body for file '{Filename}' (decoded='{Decoded}').",
                    filename,
                    decoded);
                return null;
            }

            return bytes;
        }
        // #3495 Slice D: the gate refused a hop (scheme/port downgrade), the body blew the ceiling,
        // or the total deadline elapsed. Same fail-soft contract as the other failures here — the M9
        // scheduler records "image bytes not available" and moves on.
        catch (HardenedFetchException ex)
        {
            _logger.LogWarning(ex,
                "Commons FilePath blocked by the egress gate ({Reason}) for file '{Filename}' (decoded='{Decoded}').",
                ex.Reason,
                filename,
                decoded);
            return null;
        }
        // Issue #2157: real caller cancellation propagates so the batch handler
        // can decide to abort. HttpClient.Timeout firing also raises
        // TaskCanceledException (subclass of OperationCanceledException) WITHOUT
        // cancelling the caller token; we MUST distinguish via the
        // `when (ct.IsCancellationRequested)` guard so the batch handler can
        // record the per-game timeout and continue with the next id.
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex,
                "Commons FilePath TIMEOUT for file '{Filename}' (decoded='{Decoded}'). Caller decides retry.",
                filename,
                decoded);
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex,
                "Commons FilePath network failure for file '{Filename}' (decoded='{Decoded}').",
                filename,
                decoded);
            return null;
        }
    }

    /// <summary>
    /// Parses the imageinfo response body, returning <see cref="CommonsLicenseResult.NotAvailable"/>
    /// for any missing-data case (page id <c>-1</c>, no <c>imageinfo</c> array,
    /// no <c>LicenseShortName</c>) and logging a warning. Catches <see cref="JsonException"/>
    /// upstream — see <see cref="FetchLicenseAsync"/>.
    /// </summary>
    private CommonsLicenseResult ParseResponse(string body, string filename, string decoded)
    {
        using var doc = JsonDocument.Parse(body);

        if (!doc.RootElement.TryGetProperty("query", out var query) ||
            !query.TryGetProperty("pages", out var pages) ||
            pages.ValueKind != JsonValueKind.Object)
        {
            _logger.LogWarning(
                "Commons imageinfo missing query.pages for file '{Filename}' (decoded='{Decoded}').",
                filename,
                decoded);
            return CommonsLicenseResult.NotAvailable();
        }

        // The Commons API returns pages as an object keyed by pageId. A pageId
        // of "-1" indicates the file does not exist. There is exactly one entry
        // per request (we query a single title), so the first property is
        // sufficient — we deliberately do not iterate over additional entries.
        using var enumerator = pages.EnumerateObject().GetEnumerator();
        if (!enumerator.MoveNext())
        {
            _logger.LogWarning(
                "Commons imageinfo returned empty pages object for file '{Filename}' (decoded='{Decoded}').",
                filename,
                decoded);
            return CommonsLicenseResult.NotAvailable();
        }

        var page = enumerator.Current;
        if (string.Equals(page.Name, "-1", StringComparison.Ordinal))
        {
            _logger.LogInformation(
                "Commons file '{Filename}' (decoded='{Decoded}') not found (page id -1).",
                filename,
                decoded);
            return CommonsLicenseResult.NotAvailable();
        }

        return ExtractFromPage(page.Value, filename, decoded);
    }

    private CommonsLicenseResult ExtractFromPage(JsonElement page, string filename, string decoded)
    {
        if (!page.TryGetProperty("imageinfo", out var imageinfo) ||
            imageinfo.ValueKind != JsonValueKind.Array ||
            imageinfo.GetArrayLength() == 0)
        {
            _logger.LogWarning(
                "Commons imageinfo array missing or empty for file '{Filename}' (decoded='{Decoded}').",
                filename,
                decoded);
            return CommonsLicenseResult.NotAvailable();
        }

        var first = imageinfo[0];
        if (!first.TryGetProperty("extmetadata", out var extmetadata))
        {
            _logger.LogWarning(
                "Commons extmetadata missing for file '{Filename}' (decoded='{Decoded}').",
                filename,
                decoded);
            return CommonsLicenseResult.NotAvailable();
        }

        var rawLicense = TryReadStringProperty(extmetadata, "LicenseShortName");
        if (string.IsNullOrWhiteSpace(rawLicense))
        {
            _logger.LogWarning(
                "Commons LicenseShortName missing for file '{Filename}' (decoded='{Decoded}').",
                filename,
                decoded);
            return CommonsLicenseResult.NotAvailable();
        }

        var attribution = TryReadStringProperty(extmetadata, "Artist");
        var attributionText = string.IsNullOrWhiteSpace(attribution)
            ? null
            : StripHtmlTags(attribution!);

        var whitelisted = LicenseValidator.IsWhitelisted(rawLicense);
        return CommonsLicenseResult.Found(rawLicense, whitelisted, attributionText);
    }

    /// <summary>
    /// Reads <c>extmetadata.{name}.value</c> safely. Returns <c>null</c> when
    /// the property is missing, the <c>value</c> sub-property is missing, or
    /// the value is not a string.
    /// </summary>
    private static string? TryReadStringProperty(JsonElement extmetadata, string name)
    {
        if (!extmetadata.TryGetProperty(name, out var entry)) return null;
        if (!entry.TryGetProperty("value", out var value)) return null;
        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    /// <summary>
    /// Strips HTML tags from a Commons <c>Artist</c> field. The Commons API
    /// returns the artist credit as HTML (typically a
    /// <c>&lt;a&gt;User Name&lt;/a&gt;</c> anchor); this method extracts the
    /// inner text so it can be stored as plain text in
    /// <c>shared_games.cover_attribution</c>. Idempotent on tag-free input.
    /// </summary>
    private static string StripHtmlTags(string artistHtml)
    {
        return HtmlTagPattern.Replace(artistHtml, string.Empty).Trim();
    }
}
