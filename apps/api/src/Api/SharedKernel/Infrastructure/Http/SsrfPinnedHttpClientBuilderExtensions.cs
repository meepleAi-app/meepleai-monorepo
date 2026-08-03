using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;

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
    public static IHttpClientBuilder ConfigureSsrfPin(this IHttpClientBuilder builder, bool allowAutoRedirect = true)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.ConfigurePrimaryHttpMessageHandler(sp => new SocketsHttpHandler
        {
            ConnectCallback = SsrfPinnedConnect.Create(sp.GetRequiredService<IDnsResolver>()),
            AllowAutoRedirect = allowAutoRedirect,
            MaxAutomaticRedirections = 5,
        });
    }
}
