using System.Net;

namespace Api.SharedKernel.Infrastructure.Http;

/// <summary>
/// Seam over DNS resolution (issue #3495, finding H4). Injecting it lets the SSRF connect pin
/// resolve once and connect to that exact address in a unit-testable way, and lets tests inject
/// a resolver that returns public-then-private to prove the pin (not just the validation).
/// </summary>
internal interface IDnsResolver
{
    Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken ct);
}

/// <summary>Production resolver backed by the OS resolver.</summary>
internal sealed class SystemDnsResolver : IDnsResolver
{
    public async Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken ct)
        => await Dns.GetHostAddressesAsync(host, ct).ConfigureAwait(false);
}
