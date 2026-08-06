using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Api.SharedKernel.Infrastructure.Http;

/// <summary>
/// Applies the SSRF connect-pin (<see cref="SsrfPinnedConnect"/>) to a typed
/// <see cref="HttpClient"/> egress so that EVERY connection — the initial request AND every
/// ≤5 auto-redirect hop — resolves the host once, fails closed if any resolved address is
/// private/reserved (per <see cref="SsrfPolicy"/>), and dials the validated public IP directly.
/// DNS-rebinding and redirect-to-internal are closed by construction.
/// <para>
/// Shared by all outbound sinks (BGG cover download, Slack webhook) so the pin configuration
/// lives in exactly one place — the drift-sensitive security wiring is DRY (issue #3495 fix 3/N).
/// </para>
/// </summary>
internal static class SsrfPinnedHttpClientBuilderExtensions
{
    /// <summary>
    /// Configures the client's primary handler as a <see cref="SocketsHttpHandler"/> whose
    /// <see cref="SocketsHttpHandler.ConnectCallback"/> pins to the validated address. Requires an
    /// <see cref="IDnsResolver"/> to be registered in the same container.
    /// <para>
    /// <paramref name="allowAutoRedirect"/> defaults to <see langword="true"/> for fixed-host sinks
    /// (BGG/Wikidata/Commons/Slack), where every ≤5 auto-redirect hop is re-pinned at connect. The
    /// arbitrary-URL manual sink (issue #3495 fix 5/N) passes <see langword="false"/> so it can follow
    /// redirects itself through an HTTPS-only scheme gate the connect-pin cannot express.
    /// </para>
    /// </summary>
    /// <param name="builder">The typed-client HTTP builder whose primary handler is being pinned.</param>
    /// <param name="sink">A bounded <c>MeepleAiMetrics.EgressSinks</c> constant identifying this client
    /// (a compile-time value, never a host/IP) — the label on the egress blocked/allowed counters (#3495 M2).</param>
    /// <param name="allowAutoRedirect">See the summary — <see langword="false"/> only for the arbitrary-URL manual sink.</param>
    /// <param name="configureHandler">Optional per-sink tuning of the pinned handler (connection pooling,
    /// concurrency, HTTP/2). Runs BEFORE the pin fields are written, so a caller can never weaken the
    /// SSRF guard: <see cref="SocketsHttpHandler.ConnectCallback"/>, <paramref name="allowAutoRedirect"/>
    /// and the redirect cap are re-applied afterwards and always win (#3495 Slice E / finding C3 —
    /// share the callback, not one handler, so each sink keeps its own pool and budgets).</param>
    /// <param name="allowedHostSuffixes">Defence in depth for FIXED-HOST sinks (#3495 M3): every
    /// connection — initial request and each redirect hop — must target one of these registrable
    /// domains (exact or sub-domain). Omit it ONLY for the arbitrary-URL manual sink, whose purpose is
    /// to fetch a host chosen at call time.</param>
    public static IHttpClientBuilder ConfigureSsrfPin(
        this IHttpClientBuilder builder,
        string sink,
        bool allowAutoRedirect = true,
        Action<SocketsHttpHandler>? configureHandler = null,
        IReadOnlyCollection<string>? allowedHostSuffixes = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // The pin needs a resolver; registering it here means a new pinned sink cannot fail at
        // runtime because its context forgot the TryAdd (contexts registering their own stub/impl
        // BEFORE this call still win — TryAdd never overwrites).
        builder.Services.TryAddSingleton<IDnsResolver, SystemDnsResolver>();

        return builder.ConfigurePrimaryHttpMessageHandler(sp =>
        {
            var handler = new SocketsHttpHandler();
            configureHandler?.Invoke(handler);

            handler.ConnectCallback = SsrfPinnedConnect.Create(
                sp.GetRequiredService<IDnsResolver>(), sink, allowedHostSuffixes);
            handler.AllowAutoRedirect = allowAutoRedirect;
            handler.MaxAutomaticRedirections = 5;
            return handler;
        });
    }
}
