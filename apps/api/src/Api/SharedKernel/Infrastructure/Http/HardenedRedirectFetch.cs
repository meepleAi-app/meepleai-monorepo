using Api.Observability;

namespace Api.SharedKernel.Infrastructure.Http;

/// <summary>
/// Issue #3495 Slice D — the hardened redirect engine shared by every egress sink that follows
/// redirects or fetches a caller-supplied URL.
/// <para>
/// Division of responsibility: the connect-pin (<see cref="SsrfPinnedConnect"/>) owns the IP
/// boundary — it resolves once per connection and dials only a validated public address, so
/// DNS-rebinding and redirect-to-internal are closed by construction. This engine owns what the pin
/// cannot express, on the initial URL AND on every hop:
/// </para>
/// <list type="bullet">
///   <item>absolute <b>HTTPS-only</b> — a <c>302</c> to <c>http/file/gopher</c> is refused (C4/H2);</item>
///   <item><b>default port only</b> — a redirect to <c>:8080</c> / <c>:22</c> is port-probing (H2);</item>
///   <item>bounded hop count with loop detection;</item>
///   <item>a streamed byte ceiling, enforced during the read rather than after buffering;</item>
///   <item>a <b>total wall-clock deadline</b> across the whole exchange — a chain of individually
///   fast hops must not be able to hold a request open indefinitely (C4).</item>
/// </list>
/// <para>
/// Auto-redirect MUST be disabled on the client's handler (<c>ConfigureSsrfPin(allowAutoRedirect:
/// false)</c>) for these guards to see the 3xx at all: with auto-redirect on, the handler follows a
/// downgrade before this code can reject it.
/// </para>
/// </summary>
internal static class HardenedRedirectFetch
{
    /// <summary>Redirect follows allowed after the initial request.</summary>
    public const int MaxRedirectHops = 5;

    /// <summary>Default total budget for an arbitrary-URL fetch (#3495 C4).</summary>
    public static readonly TimeSpan DefaultDeadline = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Fetches <paramref name="url"/> under the guards described on the class, returning the fully
    /// read, within-ceiling bytes.
    /// </summary>
    /// <param name="client">Client whose handler carries the SSRF connect-pin with auto-redirect disabled.
    /// A relative <paramref name="url"/> is resolved against its <see cref="HttpClient.BaseAddress"/>.</param>
    /// <param name="url">Absolute HTTPS URL, or a relative path against the client's base address.</param>
    /// <param name="maxBytes">Byte ceiling for the response body.</param>
    /// <param name="sink">Bounded <c>MeepleAiMetrics.EgressSinks</c> constant for the block counters.</param>
    /// <param name="deadline">Total wall-clock budget for the whole exchange, hops included.</param>
    /// <param name="configureRequest">Applied to every hop's request (headers such as Accept).</param>
    /// <param name="ct">Caller cancellation. Distinct from the deadline: a caller cancel surfaces as
    /// <see cref="OperationCanceledException"/>, an expired deadline as
    /// <see cref="HardenedFetchException"/> with <see cref="HardenedFetchBlockReason.Timeout"/>.</param>
    /// <exception cref="HardenedFetchException">A guard refused the fetch.</exception>
    /// <exception cref="HttpRequestException">The final hop answered a non-success status.</exception>
    /// <exception cref="OperationCanceledException">The CALLER cancelled.</exception>
    public static async Task<byte[]> FetchAsync(
        HttpClient client,
        string url,
        long maxBytes,
        string sink,
        TimeSpan deadline,
        Action<HttpRequestMessage>? configureRequest,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(client);

        var current = ResolveInitialTarget(url, client.BaseAddress, sink);
        var visited = new HashSet<string>(StringComparer.Ordinal);

        // One budget for the whole exchange. Linked to the caller token so a real cancel still wins,
        // and disposed on exit so the timer never outlives the request.
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(deadline);

        try
        {
            // The initial request plus up to MaxRedirectHops redirect follows.
            for (var hop = 0; hop <= MaxRedirectHops; hop++)
            {
                if (!visited.Add(current.AbsoluteUri))
                {
                    throw Blocked(sink, HardenedFetchBlockReason.RedirectExhausted, "Redirect loop detected");
                }

                using var request = new HttpRequestMessage(HttpMethod.Get, current);
                configureRequest?.Invoke(request);

                using var response = await client
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, budget.Token)
                    .ConfigureAwait(false);

                if (!IsRedirect(response.StatusCode))
                {
                    response.EnsureSuccessStatusCode();

                    // Reject early on an advertised over-cap length; Content-Length is spoofable and
                    // absent on chunked bodies, so the ceiling is ALSO enforced during the read.
                    if (response.Content.Headers.ContentLength > maxBytes)
                    {
                        throw Blocked(sink, HardenedFetchBlockReason.SizeCap,
                            $"Response exceeds the maximum size of {maxBytes} bytes");
                    }

                    return await ReadWithCeilingAsync(response, maxBytes, sink, budget.Token).ConfigureAwait(false);
                }

                var location = response.Headers.Location
                    ?? throw Blocked(sink, HardenedFetchBlockReason.Scheme, "Redirect response had no Location header");

                // Resolve relative → absolute against the CURRENT hop, then re-run the full gate on
                // the target: the pin re-checks the IP of the next connection, nothing else.
                current = ResolveAndValidate(new Uri(current, location).AbsoluteUri, baseAddress: null, sink);
            }

            throw Blocked(sink, HardenedFetchBlockReason.RedirectExhausted,
                $"Exceeded the maximum of {MaxRedirectHops} redirects");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // The budget fired, not the caller: report a timeout rather than masquerading as a cancel.
            throw Blocked(sink, HardenedFetchBlockReason.Timeout,
                $"Egress fetch exceeded its {deadline.TotalSeconds:0.###}s budget");
        }
    }

    /// <summary>
    /// Resolves the FIRST target of an exchange.
    /// <para>
    /// Deliberate asymmetry: a <b>relative</b> path is our own configuration — the caller is a typed
    /// client whose <see cref="HttpClient.BaseAddress"/> we set in DI — so it is resolved and dialled
    /// without the scheme/port gate (the connect-pin still owns its IP). An <b>absolute</b> URL is
    /// caller input (the manual cover sink, a stored webhook) and gets the full gate. Every REDIRECT
    /// target is untrusted regardless of how the exchange started, so
    /// <see cref="ResolveAndValidate"/> runs on each hop.
    /// </para>
    /// <para>
    /// This is what lets a fixed-host client keep working against a local contract/test server on
    /// <c>http://localhost:PORT</c> while a hostile 302 to that same address is still refused.
    /// </para>
    /// </summary>
    private static Uri ResolveInitialTarget(string url, Uri? baseAddress, string sink)
    {
        if (!Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out var parsed))
        {
            throw Blocked(sink, HardenedFetchBlockReason.Scheme, "Invalid URL");
        }

        if (parsed.IsAbsoluteUri)
        {
            return ResolveAndValidate(url, baseAddress, sink);
        }

        if (baseAddress is null)
        {
            throw Blocked(sink, HardenedFetchBlockReason.Scheme,
                "Relative URL with no base address to resolve against");
        }

        return new Uri(baseAddress, parsed);
    }

    /// <summary>
    /// Validates that a URL is an absolute HTTPS URL on the default port, resolving it against
    /// <paramref name="baseAddress"/> when relative. Applied to every redirect hop, and available to
    /// callers that want the same fail-fast on a caller-supplied URL before starting work.
    /// </summary>
    /// <exception cref="HardenedFetchException">The URL is not absolute HTTPS, or uses a non-default port.</exception>
    public static Uri ResolveAndValidate(string url, Uri? baseAddress, string sink)
    {
        if (!Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out var parsed))
        {
            throw Blocked(sink, HardenedFetchBlockReason.Scheme, "Invalid URL");
        }

        if (!parsed.IsAbsoluteUri)
        {
            if (baseAddress is null)
            {
                throw Blocked(sink, HardenedFetchBlockReason.Scheme,
                    "Relative URL with no base address to resolve against");
            }

            parsed = new Uri(baseAddress, parsed);
        }

        if (!string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw Blocked(sink, HardenedFetchBlockReason.Scheme, "Only HTTPS URLs are allowed");
        }

        // Default port only. An explicit ":443" is still the default port (IsDefaultPort is true), so
        // this rejects exactly the port-probing shapes — :8080, :22, :6379 — and nothing legitimate.
        if (!parsed.IsDefaultPort)
        {
            throw Blocked(sink, HardenedFetchBlockReason.Port,
                "Only the default HTTPS port is allowed");
        }

        return parsed;
    }

    private static async Task<byte[]> ReadWithCeilingAsync(
        HttpResponseMessage response, long maxBytes, string sink, CancellationToken ct)
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
            {
                throw Blocked(sink, HardenedFetchBlockReason.SizeCap,
                    $"Response exceeds the maximum size of {maxBytes} bytes");
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), ct).ConfigureAwait(false);
        }

        return buffer.ToArray();
    }

    /// <summary>
    /// Counts the block on the bounded egress counters and builds the exception. Every refusal goes
    /// through here so "blocked" is never silent (#3495 M2).
    /// </summary>
    private static HardenedFetchException Blocked(string sink, HardenedFetchBlockReason reason, string message)
    {
        MeepleAiMetrics.RecordEgressBlocked(sink, ToMetricReason(reason));
        return new HardenedFetchException(reason, message);
    }

    private static string ToMetricReason(HardenedFetchBlockReason reason) => reason switch
    {
        HardenedFetchBlockReason.Scheme => MeepleAiMetrics.EgressBlockReasons.Scheme,
        HardenedFetchBlockReason.Port => MeepleAiMetrics.EgressBlockReasons.Port,
        HardenedFetchBlockReason.RedirectExhausted => MeepleAiMetrics.EgressBlockReasons.RedirectExhausted,
        HardenedFetchBlockReason.SizeCap => MeepleAiMetrics.EgressBlockReasons.SizeCap,
        HardenedFetchBlockReason.Timeout => MeepleAiMetrics.EgressBlockReasons.Timeout,
        _ => MeepleAiMetrics.EgressBlockReasons.Scheme,
    };

    private static bool IsRedirect(System.Net.HttpStatusCode code) => code is
        System.Net.HttpStatusCode.MovedPermanently or System.Net.HttpStatusCode.Found
        or System.Net.HttpStatusCode.SeeOther or System.Net.HttpStatusCode.TemporaryRedirect
        or System.Net.HttpStatusCode.PermanentRedirect;
}
