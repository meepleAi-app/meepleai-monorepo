using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services;

internal sealed class BggCoverDownloader : IBggCoverDownloader
{
    private readonly HttpClient _httpClient;
    private readonly IBggCoverUploadPipeline _uploadPipeline;
    private readonly ILogger<BggCoverDownloader> _logger;

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

        // SSRF guard (#2655 finding #10): only fetch HTTPS URLs that resolve to public IPs.
        // Fails closed — an invalid scheme or a private/reserved target aborts the download.
        try
        {
            SsrfSafeHttpClient.ValidateUrlScheme(remoteImageUrl);
            await SsrfSafeHttpClient.ValidateResolvedIpAsync(remoteImageUrl, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
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

            var imageBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
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
