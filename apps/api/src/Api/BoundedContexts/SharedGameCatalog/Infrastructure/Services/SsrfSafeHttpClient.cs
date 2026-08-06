using Api.Observability;
using Api.SharedKernel.Infrastructure.Http;

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;

/// <summary>
/// Hardened egress client for the manual / arbitrary-URL download path (epic #3470 Slice 3
/// prerequisite, issue #3495 fix 5/N).
/// <para>
/// Two layers guard this sink and neither is optional:
/// </para>
/// <list type="bullet">
///   <item>the connect-pin (<see cref="SsrfPinnedConnect"/>, applied via
///   <c>ConfigureSsrfPin(allowAutoRedirect: false)</c>) owns the IP boundary — every connection
///   resolves once and dials a validated public address, closing DNS-rebinding and
///   redirect-to-internal by construction;</item>
///   <item><see cref="HardenedRedirectFetch"/> owns everything the pin cannot express — per-hop
///   HTTPS-only + default-port re-validation, bounded redirect follow with loop detection, a
///   streamed byte ceiling, and a TOTAL wall-clock budget (#3495 Slice D, findings C4/H2).</item>
/// </list>
/// <para>
/// This type is the DI seam that binds the manual sink's <see cref="HttpClient"/> (and its
/// <c>manual</c> metric label) to that engine; the fetch logic itself is shared, so the Commons
/// sink is guarded by exactly the same code path.
/// </para>
/// </summary>
internal sealed class SsrfSafeHttpClient
{
    private readonly HttpClient _httpClient;
    private const long MaxImageSizeBytes = 10 * 1024 * 1024; // 10MB (cover images)

    public SsrfSafeHttpClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <summary>
    /// Epic #3470 Slice 3a — downloads an admin-supplied cover image through the hardened fetch path
    /// under a 10MB streamed ceiling and the default total deadline. Returns the raw image bytes;
    /// the caller validates/re-encodes them.
    /// </summary>
    /// <exception cref="HardenedFetchException">A guard refused the fetch (scheme, port, redirect
    /// budget, size ceiling or deadline) — <see cref="HardenedFetchException.Reason"/> says which.</exception>
    /// <exception cref="HttpRequestException">The final hop answered a non-success status.</exception>
    public Task<byte[]> DownloadImageAsync(string url, CancellationToken ct) =>
        HardenedRedirectFetch.FetchAsync(
            _httpClient,
            url,
            MaxImageSizeBytes,
            MeepleAiMetrics.EgressSinks.Manual,
            HardenedRedirectFetch.DefaultDeadline,
            configureRequest: null,
            ct);
}
