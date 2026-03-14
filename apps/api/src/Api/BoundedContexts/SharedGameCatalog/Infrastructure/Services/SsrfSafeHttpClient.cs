using System.Net;
using System.Net.Sockets;

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;

/// <summary>
/// HTTP client wrapper that validates URLs against SSRF attacks before downloading.
/// Blocks private/reserved IP ranges, non-HTTPS schemes, and oversized responses.
/// </summary>
internal sealed class SsrfSafeHttpClient
{
    private readonly HttpClient _httpClient;
    private const long MaxPdfSizeBytes = 100 * 1024 * 1024; // 100MB

    public SsrfSafeHttpClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <summary>
    /// Downloads a PDF from the given URL after validating it is safe (HTTPS, public IP, valid PDF).
    /// </summary>
    /// <param name="url">The HTTPS URL to download the PDF from.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A seekable MemoryStream containing the PDF content.</returns>
    /// <exception cref="ArgumentException">If the URL is invalid or not HTTPS.</exception>
    /// <exception cref="InvalidOperationException">If the URL resolves to a private IP, the file is too large, or content is not a valid PDF.</exception>
    public async Task<Stream> DownloadPdfAsync(string url, CancellationToken ct)
    {
        ValidateUrlScheme(url);
        await ValidateResolvedIpAsync(url, ct).ConfigureAwait(false);

        var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        // Validate size from Content-Length header if available
        if (response.Content.Headers.ContentLength > MaxPdfSizeBytes)
            throw new InvalidOperationException($"PDF exceeds maximum size of {MaxPdfSizeBytes / (1024 * 1024)}MB");

        var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);

        // Validate PDF magic bytes (%PDF)
        var buffer = new byte[4];
        var bytesRead = await stream.ReadAsync(buffer, ct).ConfigureAwait(false);
        if (bytesRead < 4 || buffer[0] != 0x25 || buffer[1] != 0x50 || buffer[2] != 0x44 || buffer[3] != 0x46)
            throw new InvalidOperationException("Downloaded content is not a valid PDF file");

        // Return a new stream that includes the magic bytes we already read
        var memStream = new MemoryStream();
        await memStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct).ConfigureAwait(false);
        await stream.CopyToAsync(memStream, ct).ConfigureAwait(false);

        if (memStream.Length > MaxPdfSizeBytes)
            throw new InvalidOperationException($"PDF exceeds maximum size of {MaxPdfSizeBytes / (1024 * 1024)}MB");

        memStream.Position = 0;
        return memStream;
    }

    /// <summary>
    /// Validates that the URL uses the HTTPS scheme.
    /// </summary>
    internal static void ValidateUrlScheme(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new ArgumentException("Invalid URL", nameof(url));

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Only HTTPS URLs are allowed", nameof(url));
    }

    /// <summary>
    /// Validates that the URL does not resolve to a private or reserved IP address.
    /// </summary>
    internal static async Task ValidateResolvedIpAsync(string url, CancellationToken ct)
    {
        var uri = new Uri(url);
        var addresses = await Dns.GetHostAddressesAsync(uri.Host, ct).ConfigureAwait(false);

        foreach (var ip in addresses)
        {
            if (IsPrivateOrReserved(ip))
                throw new InvalidOperationException($"URL resolves to blocked IP range: {ip}");
        }
    }

    /// <summary>
    /// Checks whether an IP address belongs to a private or reserved range (RFC 1918, link-local, loopback, etc.).
    /// </summary>
    internal static bool IsPrivateOrReserved(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip)) return true;

        // IPv4-mapped IPv6 addresses (e.g. ::ffff:10.0.0.1) must be checked as IPv4
        if (ip.IsIPv4MappedToIPv6)
            return IsPrivateOrReserved(ip.MapToIPv4());

        var bytes = ip.GetAddressBytes();

        return ip.AddressFamily switch
        {
            AddressFamily.InterNetwork => bytes[0] switch
            {
                10 => true,                                           // 10.0.0.0/8
                172 => bytes[1] >= 16 && bytes[1] <= 31,             // 172.16.0.0/12
                192 => bytes[1] == 168,                               // 192.168.0.0/16
                169 => bytes[1] == 254,                               // 169.254.0.0/16 (link-local/AWS metadata)
                0 => true,                                            // 0.0.0.0/8
                _ => false
            },
            AddressFamily.InterNetworkV6 => ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.Equals(IPAddress.IPv6Loopback),
            _ => true // Block unknown address families
        };
    }
}
