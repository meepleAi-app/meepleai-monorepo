using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;
using Api.Observability;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Providers;

/// <summary>
/// Primary catalog provider — Wikidata SPARQL endpoint.
/// Spec: 2026-06-04-admin-catalog-seed-design.md §7.1.
/// License: CC0 (all data) — no attribution required.
/// Issue #1903.
/// </summary>
internal sealed class WikidataCatalogProvider : ICatalogProvider
{
    public string Name => "wikidata";

    private const string SparqlPath = "sparql";

    // Validates Wikidata QID format (Q followed by digits) before SPARQL interpolation.
    // Anchored, no backtracking — DoS-safe. Timeout is defensive.
    private static readonly Regex WikidataQidPattern = new(
        @"^Q\d+$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(200));

    // Prefix matched against the Wikidata SPARQL IRI for P18 (image) bindings.
    // Wikidata embeds the Commons filename after this prefix (URL-encoded per Wikidata convention).
    // S1075 suppressed: this is the stable Wikidata IRI scheme returned by the SPARQL endpoint,
    // not an internal infra URL.
#pragma warning disable S1075 // URIs should not be hardcoded
    private const string CommonsFilePathPrefix = "http://commons.wikimedia.org/wiki/Special:FilePath/";
#pragma warning restore S1075

    private readonly HttpClient _http;
    private readonly ILogger<WikidataCatalogProvider> _logger;
    private readonly IWikimediaRateLimiter _rateLimiter;

    public WikidataCatalogProvider(
        HttpClient http,
        ILogger<WikidataCatalogProvider> logger,
        IWikimediaRateLimiter rateLimiter)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
    }

    public async Task<CatalogProviderResult> FetchAsync(CatalogProviderQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.BggId is null && query.WikidataQid is null && string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            return CatalogProviderResult.Empty("Missing query parameters");
        }

        if (query.WikidataQid is not null && !WikidataQidPattern.IsMatch(query.WikidataQid))
        {
            return CatalogProviderResult.Empty("Invalid WikidataQid format");
        }

        var sparql = BuildSparql(query);

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"{SparqlPath}?query={Uri.EscapeDataString(sparql)}&format=json");
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/sparql-results+json"));

            using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Wikidata SPARQL HTTP {Status} on query: {Sparql}", (int)resp.StatusCode, sparql);
                return CatalogProviderResult.Empty($"HTTP {(int)resp.StatusCode}");
            }

            return ParseResponse(body, query, sourceUrl: BuildSourceUrl(query));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Wikidata fetch failed");
            return CatalogProviderResult.Empty(ex.GetType().Name);
        }
    }

    /// <summary>
    /// Issue #1823 Phase B M3 — fetches the Wikidata Commons cover filename
    /// (<c>wdt:P18</c> claim) for the supplied QID.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Consumes the shared <see cref="IWikimediaRateLimiter"/> (ADR DEC-3e) BEFORE
    /// issuing the SPARQL request so concurrent Wikidata + Commons clients cannot
    /// burst past the published 5 RPS cap. Records latency on the
    /// <c>meepleai.wikidata.sparql.latency_seconds</c> histogram per ADR DEC-3g.
    /// </para>
    /// <para>
    /// Returns <see cref="WikidataCoverImageResult.NotFound(string)"/> when:
    /// <list type="bullet">
    ///   <item>the QID fails the safe regex (no SPARQL injection),</item>
    ///   <item>the QID exists but has no <c>P18</c> claim,</item>
    ///   <item>Wikidata returns a non-success HTTP status (caller decides retry),</item>
    ///   <item>the response body is malformed.</item>
    /// </list>
    /// Cancellation propagates as <see cref="OperationCanceledException"/>.
    /// </para>
    /// </remarks>
    public async Task<WikidataCoverImageResult> FetchCoverImageAsync(
        string qid,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(qid) || !WikidataQidPattern.IsMatch(qid))
        {
            return WikidataCoverImageResult.NotFound(qid ?? string.Empty);
        }

        // ADR DEC-3e: shared 5 RPS cap. Acquire BEFORE the HTTP round-trip so
        // Wikidata + Commons consumers cannot drift apart and exceed the cap.
        await _rateLimiter.AcquireAsync(ct).ConfigureAwait(false);

        var sparql = BuildCoverSparql(qid);
        var sourceUrl = $"https://www.wikidata.org/wiki/{qid}";
        var stopwatch = Stopwatch.StartNew();

        // ADR DEC-3g: record SPARQL latency on every COMPLETED round-trip
        // (success + non-success status) so ops can detect endpoint degradation.
        // The flag is flipped AFTER SendAsync returns a response — network setup
        // failures (DNS error, connection refused) that throw before a response
        // is received are NOT counted (no round-trip to measure).
        // The finally block guarantees emission across success, 5xx, body-read
        // failures, JSON parse errors, and cancellation rethrow.
        var roundTripCompleted = false;

        try
        {
            using var req = new HttpRequestMessage(
                HttpMethod.Get,
                $"{SparqlPath}?query={Uri.EscapeDataString(sparql)}&format=json");
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/sparql-results+json"));

            using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
            roundTripCompleted = true;
            var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Wikidata SPARQL cover fetch HTTP {Status} for QID {Qid}",
                    (int)resp.StatusCode,
                    qid);
                return WikidataCoverImageResult.NotFound(qid);
            }

            return ParseCoverResponse(body, qid, sourceUrl);
        }
        // Issue #2157: real caller cancellation propagates so the batch handler
        // can decide to abort. We MUST guard with `when (ct.IsCancellationRequested)`
        // because HttpClient.Timeout firing also raises a TaskCanceledException
        // (subclass of OperationCanceledException) WITHOUT cancelling the caller
        // token — without the guard we would conflate a per-game HTTP timeout
        // with a batch-wide user cancel, aborting the entire batch on the first
        // slow upstream response.
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        // Issue #2157: HttpClient.Timeout (TaskCanceledException) with caller
        // token NOT cancelled = upstream timeout. Map to NotFound so the batch
        // continues with the next game; the batch handler still records a
        // distinct outcome for ops via the per-game audit trail.
        catch (OperationCanceledException oce)
        {
            _logger.LogWarning(oce, "Wikidata cover fetch TIMEOUT for QID {Qid}", qid);
            return WikidataCoverImageResult.NotFound(qid);
        }
        // Issue #1823 Wave 3 M13 (M10 follow-up): the WikimediaCircuitBreakerHandler
        // (DEC-3f) throws BrokenCircuitException when its circuit is OPEN. Letting
        // the generic catch swallow it would silently map "upstream temporarily
        // unavailable" to "no P18 claim" (Skipped("image-not-available-p18")),
        // poisoning the audit trail. Rethrow so the M8 handler can record a
        // dedicated Failed("circuit-open") attempt that the M9 scheduler retries
        // after the breaker recovers. Detected reflection-side because Polly v7
        // + v8 export the same FQN — see CircuitBreakerExceptionDetector.
        catch (Exception ex) when (Api.BoundedContexts.SharedGameCatalog.Infrastructure.Resilience.CircuitBreakerExceptionDetector.IsBrokenCircuit(ex))
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Wikidata cover fetch failed for QID {Qid}", qid);
            return WikidataCoverImageResult.NotFound(qid);
        }
        finally
        {
            stopwatch.Stop();
            if (roundTripCompleted)
            {
                MeepleAiMetrics.WikidataSparqlLatency.Record(stopwatch.Elapsed.TotalSeconds);
            }
        }
    }

    private static string BuildCoverSparql(string qid) => $@"
SELECT ?image
WHERE {{
  BIND(wd:{qid} AS ?game)
  OPTIONAL {{ ?game wdt:P18 ?image. }}
}}
LIMIT 1";

    private static WikidataCoverImageResult ParseCoverResponse(string body, string qid, string sourceUrl)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("results", out var results) ||
                !results.TryGetProperty("bindings", out var bindings) ||
                bindings.ValueKind != JsonValueKind.Array ||
                bindings.GetArrayLength() == 0)
            {
                return WikidataCoverImageResult.NotFound(qid);
            }

            var row = bindings[0];
            if (!row.TryGetProperty("image", out var imageEl) ||
                !imageEl.TryGetProperty("value", out var valueEl))
            {
                return WikidataCoverImageResult.NotFound(qid);
            }

            var iri = valueEl.GetString();
            if (string.IsNullOrEmpty(iri) ||
                !iri.StartsWith(CommonsFilePathPrefix, StringComparison.Ordinal))
            {
                return WikidataCoverImageResult.NotFound(qid);
            }

            // Preserve raw URL-encoded filename — Commons API + R2 keys downstream
            // expect the Wikidata IRI convention (spaces as %20, etc.).
            var filename = iri[CommonsFilePathPrefix.Length..];
            if (string.IsNullOrEmpty(filename))
            {
                return WikidataCoverImageResult.NotFound(qid);
            }

            return WikidataCoverImageResult.Found(filename, sourceUrl);
        }
        catch (JsonException)
        {
            // Malformed response body — treat as no-result; caller decides retry.
            return WikidataCoverImageResult.NotFound(qid);
        }
    }

    private static string BuildSparql(CatalogProviderQuery q)
    {
        var bind = q.BggId.HasValue
            ? $"?game wdt:P2339 \"{q.BggId.Value}\"."
            : q.WikidataQid is not null
                ? $"BIND(wd:{q.WikidataQid} AS ?game)"
                : $"?game rdfs:label \"{q.SearchTerm}\"@en.";

        return $@"
SELECT ?game ?gameLabel ?yearPublished ?designerLabel ?publisherLabel
       ?minPlayers ?maxPlayers ?playingTimeMinutes
WHERE {{
  {bind}
  OPTIONAL {{ ?game wdt:P577 ?yearPublished. }}
  OPTIONAL {{ ?game wdt:P3300 ?designer. }}
  OPTIONAL {{ ?game wdt:P123 ?publisher. }}
  OPTIONAL {{ ?game wdt:P1873 ?minPlayers. }}
  OPTIONAL {{ ?game wdt:P1872 ?maxPlayers. }}
  OPTIONAL {{ ?game wdt:P2047 ?playingTimeMinutes. }}
  SERVICE wikibase:label {{ bd:serviceParam wikibase:language ""en"". }}
}}
LIMIT 1";
    }

    private static string BuildSourceUrl(CatalogProviderQuery q)
    {
        if (q.WikidataQid is not null) return $"https://www.wikidata.org/wiki/{q.WikidataQid}";
        if (q.BggId.HasValue) return $"https://query.wikidata.org/?bggid={q.BggId}";
        return "https://query.wikidata.org/";
    }

    private static CatalogProviderResult ParseResponse(string body, CatalogProviderQuery q, string sourceUrl)
    {
        var fetchedAt = DateTime.UtcNow;
        using var doc = JsonDocument.Parse(body);
        var bindings = doc.RootElement
            .GetProperty("results")
            .GetProperty("bindings");

        if (bindings.GetArrayLength() == 0)
        {
            return CatalogProviderResult.Empty("Wikidata: entity not found");
        }

        var row = bindings[0];
        var fields = new Dictionary<string, FieldProvenance>(StringComparer.Ordinal);

        string? Get(string key) => row.TryGetProperty(key, out var el) && el.TryGetProperty("value", out var v) ? v.GetString() : null;

        var title = Get("gameLabel");
        if (!string.IsNullOrWhiteSpace(title))
        {
            fields["title"] = new FieldProvenance("wikidata", sourceUrl, "labels.en", fetchedAt, title);
        }

        var gameUri = Get("game");
        if (gameUri is not null && gameUri.StartsWith("http://www.wikidata.org/entity/", StringComparison.Ordinal))
        {
            var qid = gameUri["http://www.wikidata.org/entity/".Length..];
            fields["wikidataQid"] = new FieldProvenance("wikidata", sourceUrl, "item URI", fetchedAt, qid);
        }

        var yearRaw = Get("yearPublished");
        if (yearRaw is not null && DateTimeOffset.TryParse(yearRaw, System.Globalization.CultureInfo.InvariantCulture, out var dt))
        {
            fields["yearPublished"] = new FieldProvenance("wikidata", sourceUrl, "P577", fetchedAt, dt.Year);
        }

        var designer = Get("designerLabel");
        if (!string.IsNullOrWhiteSpace(designer))
        {
            fields["designers"] = new FieldProvenance("wikidata", sourceUrl, "P3300", fetchedAt, new List<string> { designer });
        }

        var publisher = Get("publisherLabel");
        if (!string.IsNullOrWhiteSpace(publisher))
        {
            fields["publishers"] = new FieldProvenance("wikidata", sourceUrl, "P123", fetchedAt, new List<string> { publisher });
        }

        if (int.TryParse(Get("minPlayers"), System.Globalization.CultureInfo.InvariantCulture, out var mn))
        {
            fields["minPlayers"] = new FieldProvenance("wikidata", sourceUrl, "P1873", fetchedAt, mn);
        }
        if (int.TryParse(Get("maxPlayers"), System.Globalization.CultureInfo.InvariantCulture, out var mx))
        {
            fields["maxPlayers"] = new FieldProvenance("wikidata", sourceUrl, "P1872", fetchedAt, mx);
        }
        if (int.TryParse(Get("playingTimeMinutes"), System.Globalization.CultureInfo.InvariantCulture, out var pt))
        {
            fields["playingTimeMinutes"] = new FieldProvenance("wikidata", sourceUrl, "P2047", fetchedAt, pt);
        }

        return new CatalogProviderResult(fields, body, ErrorMessage: null);
    }
}
