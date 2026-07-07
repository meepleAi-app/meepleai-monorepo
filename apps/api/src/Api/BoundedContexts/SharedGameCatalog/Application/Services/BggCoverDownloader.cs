using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;
using Api.Services.Pdf;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services;

internal sealed class BggCoverDownloader : IBggCoverDownloader
{
    private readonly HttpClient _httpClient;
    private readonly IBlobStorageService _blobStorageService;
    private readonly ILogger<BggCoverDownloader> _logger;

    public BggCoverDownloader(
        HttpClient httpClient,
        IBlobStorageService blobStorageService,
        ILogger<BggCoverDownloader> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _blobStorageService = blobStorageService ?? throw new ArgumentNullException(nameof(blobStorageService));
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
        // Reuses the SharedGameCatalog SsrfSafeHttpClient validators (same bounded context). Fails
        // closed — an invalid scheme or a private/reserved target aborts the download (returns null).
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

        var resourceKey = $"bgg-cover-{bggId}";

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

            var imageStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var _ = imageStream.ConfigureAwait(false);

            // Derive a safe filename from the URL path
            var fileName = $"cover-{bggId}{GetExtension(remoteImageUrl)}";

            var storageResult = await _blobStorageService
                .StoreAsync(imageStream, fileName, BlobCategory.GameImage, resourceKey, cancellationToken)
                .ConfigureAwait(false);

            if (!storageResult.Success)
            {
                _logger.LogWarning(
                    "BGG cover upload failed: BggId={BggId}, Error={Error}",
                    bggId, storageResult.ErrorMessage);
                return null;
            }

            _logger.LogInformation(
                "BGG cover uploaded successfully: BggId={BggId}, R2Key={Key}",
                bggId, resourceKey);
            return resourceKey;
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
