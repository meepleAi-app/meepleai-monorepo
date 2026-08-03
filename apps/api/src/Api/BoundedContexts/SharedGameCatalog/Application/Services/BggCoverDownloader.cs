using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services;

internal sealed class BggCoverDownloader : IBggCoverDownloader
{
    private readonly HttpClient _httpClient;
    private readonly IBggCoverUploadPipeline _uploadPipeline;
    private readonly ILogger<BggCoverDownloader> _logger;

    // #3495 (finding C5): stream-and-cap the image body to bound memory. The HttpClient-level
    // MaxResponseContentBufferSize belt is a no-op under ResponseHeadersRead, so the ceiling is
    // enforced here during the read.
    private const long MaxImageSizeBytes = 10 * 1024 * 1024;

    public BggCoverDownloader(
        HttpClient httpClient,
        IBggCoverUploadPipeline uploadPipeline,
        ILogger<BggCoverDownloader> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _uploadPipeline = uploadPipeline ?? throw new ArgumentNullException(nameof(uploadPipeline));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string?> DownloadAndUploadAsync(
        int bggId,
        string remoteImageUrl,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(remoteImageUrl))
        {
            return null;
        }

        // SSRF guard: reject non-HTTPS / malformed URLs up front (fail-fast). The public-IP
        // guarantee is enforced at connect time by the SSRF connect-pin on this client's handler
        // (ConfigureSsrfPin, #3495) — which, unlike a pre-connect DNS check, is not TOCTOU: every
        // connection (and each redirect hop) dials only a validated public address.
        try
        {
            SsrfSafeHttpClient.ValidateUrlScheme(remoteImageUrl);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(
                ex,
                "BGG cover download blocked by SSRF guard: BggId={BggId}, Url={Url}",
                bggId, remoteImageUrl);
            return null;
        }

        try
        {
            using var response = await _httpClient
                .GetAsync(remoteImageUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "BGG cover download failed: BggId={BggId}, Url={Url}, Status={Status}",
                    bggId, remoteImageUrl, response.StatusCode);
                return null;
            }

            // Reject early on an advertised over-cap length; Content-Length is spoofable/absent
            // (chunked), so the ceiling is ALSO enforced during the streamed read below.
            if (response.Content.Headers.ContentLength > MaxImageSizeBytes)
            {
                _logger.LogWarning("BGG cover exceeds size cap (Content-Length): BggId={BggId}", bggId);
                return null;
            }

            byte[] imageBytes;
            using (var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            using (var buffer = new MemoryStream())
            {
                var chunk = new byte[81920];
                long total = 0;
                int read;
                while ((read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    total += read;
                    if (total > MaxImageSizeBytes)
                    {
                        _logger.LogWarning(
                            "BGG cover exceeds {Cap}-byte cap mid-stream: BggId={BggId}", MaxImageSizeBytes, bggId);
                        return null;
                    }

                    await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                }

                imageBytes = buffer.ToArray();
            }

            if (imageBytes.Length == 0)
            {
                _logger.LogWarning("BGG cover download returned empty body: BggId={BggId}", bggId);
                return null;
            }

            var extension = GetExtension(remoteImageUrl);

            // Issue #2947: raw R2 PutObject to a deterministic key so
            // CoverUrlResolver can reconstruct it via GetPresignedUrlForRawKeyAsync.
            var r2Key = await _uploadPipeline
                .UploadAsync(bggId, imageBytes, extension, cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "BGG cover uploaded successfully: BggId={BggId}, R2Key={Key}",
                bggId, r2Key);
            return r2Key;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Unexpected error in BGG cover download/upload: BggId={BggId}", bggId);
            return null;
        }
    }

    private static string GetExtension(string url)
    {
        try
        {
            var uri = new Uri(url);
            var path = uri.AbsolutePath;
            var ext = Path.GetExtension(path);
            return string.IsNullOrEmpty(ext) || ext.Length > 5 ? ".jpg" : ext.ToLowerInvariant();
        }
        catch
        {
            return ".jpg";
        }
    }
}
