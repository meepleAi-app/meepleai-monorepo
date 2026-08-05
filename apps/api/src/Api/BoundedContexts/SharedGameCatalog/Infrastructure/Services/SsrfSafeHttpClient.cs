using System.Net;

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;

/// <summary>
/// Hardened egress client for the manual / arbitrary-URL download path (epic #3470 Slice 3
/// prerequisite, issue #3495 fix 5/N).
/// <para>
/// The IP boundary is the connect-pin (<see cref="Api.SharedKernel.Infrastructure.Http.SsrfPinnedConnect"/>,
/// applied to this client's <see cref="HttpClient"/> via <c>ConfigureSsrfPin(allowAutoRedirect: false)</c>):
/// every connection resolves the host once and dials a validated public IP, closing DNS-rebinding
/// and redirect-to-internal by construction. This class adds the remaining arbitrary-URL guards the
/// pin cannot express: HTTPS-only scheme re-validated on EVERY hop (blocks a redirect that downgrades
/// to <c>http/file/gopher</c> or points at a non-HTTP scheme — the pin only re-checks the IP), a
/// bounded manual redirect follow with loop detection, and a mid-stream byte ceiling.
/// </para>
/// <para>
/// Auto-redirect is DISABLED on the primary handler so 3xx responses surface here and are followed
/// manually through the scheme gate — with <c>AllowAutoRedirect=true</c> the handler would follow a
/// downgrade before this code could reject it.
/// </para>
/// </summary>
internal sealed class SsrfSafeHttpClient
{
    private readonly HttpClient _httpClient;
    private const long MaxImageSizeBytes = 10 * 1024 * 1024; // 10MB (cover images)
    private const int MaxRedirectHops = 5;

    public SsrfSafeHttpClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    // #3495 Slice E: the PDF variant of this fetch (DownloadPdfAsync + its 100MB ceiling) was dead
    // code — no production caller ever reached it, so it was an unreviewed second egress entry point
    // whose only exercise came from its own tests. Removed; the arbitrary-URL sink is images only.

    /// <summary>
    /// Epic #3470 Slice 3a — downloads an admin-supplied cover image through the hardened fetch path
    /// (HTTPS-only per hop, bounded redirect follow, IP-pinned connection) under a 10MB streamed
    /// ceiling. Returns the raw image bytes; the caller validates/re-encodes them.
    /// </summary>
    /// <exception cref="ArgumentException">The URL (or a redirect target) is not an absolute HTTPS URL.</exception>
    /// <exception cref="InvalidOperationException">Redirect loop / too many hops, or oversize.</exception>
    public Task<byte[]> DownloadImageAsync(string url, CancellationToken ct) =>
        FetchWithLimitAsync(url, MaxImageSizeBytes, ct);

    /// <summary>
    /// Hardened fetch shared by every arbitrary-URL sink on this client: validates the scheme on
    /// the initial URL and on every redirect hop (HTTPS-only), follows up to
    /// <see cref="MaxRedirectHops"/> redirects with loop detection, and reads the body under a
    /// streamed <paramref name="maxBytes"/> ceiling (aborts at limit+1, before buffering the whole
    /// payload). The connection-level IP validation is provided by the connect-pin. Returns the
    /// fully-read, within-limit bytes; the response and its content stream are disposed on exit.
    /// </summary>
    internal async Task<byte[]> FetchWithLimitAsync(string url, long maxBytes, CancellationToken ct)
    {
        var current = ParseHttpsAbsolute(url);
        var visited = new HashSet<string>(StringComparer.Ordinal);

        // The initial request plus up to MaxRedirectHops redirect follows.
        for (var hop = 0; hop <= MaxRedirectHops; hop++)
        {
            if (!visited.Add(current.AbsoluteUri))
                throw new InvalidOperationException("Redirect loop detected");

            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            using var response = await _httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);

            if (!IsRedirect(response.StatusCode))
            {
                response.EnsureSuccessStatusCode();

                // Reject early on an advertised over-cap length; Content-Length is spoofable/absent
                // (chunked), so the ceiling is ALSO enforced during the streamed read below.
                if (response.Content.Headers.ContentLength > maxBytes)
                    throw new InvalidOperationException($"Response exceeds the maximum size of {maxBytes / (1024 * 1024)}MB");

                return await ReadWithCeilingAsync(response, maxBytes, ct).ConfigureAwait(false);
            }

            var location = response.Headers.Location
                ?? throw new InvalidOperationException("Redirect response had no Location header");

            // Resolve relative → absolute against the current URL, then RE-VALIDATE the scheme:
            // an absolute http/file/gopher Location or a relative-to-non-http target is rejected
            // here (the pin only re-checks the IP, not the scheme).
            current = ParseHttpsAbsolute(new Uri(current, location).AbsoluteUri);
        }

        throw new InvalidOperationException($"Exceeded the maximum of {MaxRedirectHops} redirects");
    }

    private static async Task<byte[]> ReadWithCeilingAsync(HttpResponseMessage response, long maxBytes, CancellationToken ct)
    {
        using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var buffer = new MemoryStream();

        var chunk = new byte[81920];
        long total = 0;
        int read;
        while ((read = await stream.ReadAsync(chunk, ct).ConfigureAwait(false)) > 0)
        {
            total += read;
            if (total > maxBytes)
                throw new InvalidOperationException($"Response exceeds the maximum size of {maxBytes / (1024 * 1024)}MB");

            await buffer.WriteAsync(chunk.AsMemory(0, read), ct).ConfigureAwait(false);
        }

        return buffer.ToArray();
    }

    private static bool IsRedirect(HttpStatusCode code) => code is
        HttpStatusCode.MovedPermanently or HttpStatusCode.Found or HttpStatusCode.SeeOther
        or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect;

    private static Uri ParseHttpsAbsolute(string url)
    {
        ValidateUrlScheme(url);
        return new Uri(url, UriKind.Absolute);
    }

    /// <summary>
    /// Validates that the URL is absolute and uses the HTTPS scheme. Applied to the initial URL and
    /// re-applied to every redirect target.
    /// </summary>
    internal static void ValidateUrlScheme(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new ArgumentException("Invalid URL", nameof(url));

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Only HTTPS URLs are allowed", nameof(url));
    }
}
