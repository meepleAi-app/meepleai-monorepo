using Api.Helpers;
using Api.Infrastructure.Security;
using Azure;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;

namespace Api.Services.Pdf;

/// <summary>
/// <see cref="IBlobStorageService"/> su Azure Blob Storage (spec azure-local §4.1). Stesso layout di chiave di
/// <see cref="S3BlobStorageService"/> — <c>{category}/{resourceKey}/{fileId}_{fileName}</c> — e stesse semantiche:
/// risoluzione per prefisso, raw-key senza validazione, HEAD prima della firma, mai un'eccezione oltre il confine.
/// Il Content-Type di <see cref="StoreAsync"/> è sempre <c>application/pdf</c> come nel ramo S3 (difetto ereditato,
/// non introdotto: le altre categorie usano <see cref="StoreRawKeyAsync"/> con il tipo esplicito).
/// </summary>
internal sealed class AzureBlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient _serviceClient;
    private readonly BlobContainerClient _container;
    private readonly AzureBlobStorageOptions _options;
    private readonly ILogger<AzureBlobStorageService> _logger;
    private bool _containerEnsured;

    public AzureBlobStorageService(
        BlobServiceClient serviceClient,
        BlobContainerClient container,
        AzureBlobStorageOptions options,
        ILogger<AzureBlobStorageService> logger)
    {
        _serviceClient = serviceClient;
        _container = container;
        _options = options;
        _logger = logger;
    }

    internal AzureBlobStorageOptions Options => _options;

    public async Task<BlobStorageResult> StoreAsync(Stream stream, string fileName, BlobCategory category, string resourceKey, CancellationToken ct = default)
    {
        MemoryStream? buffer = null;
        try
        {
            PathSecurity.ValidateIdentifier(resourceKey, nameof(resourceKey));

            var (uploadStream, length, owned) = await BufferIfNeededAsync(stream, ct).ConfigureAwait(false);
            buffer = owned;

            var fileId = Guid.NewGuid().ToString("N");
            var key = GetKey(fileId, category, resourceKey, SanitizeFileName(fileName));

            await EnsureContainerAsync(ct).ConfigureAwait(false);
            await _container.GetBlobClient(key)
                .UploadAsync(uploadStream, new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = "application/pdf" } }, ct)
                .ConfigureAwait(false);

            _logger.LogInformation("Stored blob {Key} ({Size} bytes)", key, length);
            return new BlobStorageResult(true, fileId, key, length);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Azure Blob error storing file in {Category}/{ResourceKey}: {ErrorCode}", category, resourceKey, ex.ErrorCode);
            return new BlobStorageResult(false, null, null, 0, $"Azure Blob error: {ex.Message}");
        }
#pragma warning disable CA1031 // Confine del servizio: nessuna eccezione oltre (stesso pattern di S3BlobStorageService)
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error storing file in {Category}/{ResourceKey}", category, resourceKey);
            return new BlobStorageResult(false, null, null, 0, ex.Message);
        }
#pragma warning restore CA1031
        finally
        {
            if (buffer is not null)
            {
                await buffer.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    public async Task<Stream?> RetrieveAsync(string fileId, BlobCategory category, string resourceKey, CancellationToken ct = default)
    {
        try
        {
            PathSecurity.ValidateIdentifier(fileId, nameof(fileId));
            PathSecurity.ValidateIdentifier(resourceKey, nameof(resourceKey));

            var key = await TryResolveKeyAsync(fileId, category, resourceKey, ct).ConfigureAwait(false);
            if (key is null)
            {
                _logger.LogWarning("Blob not found for {FileId} in {Category}/{ResourceKey}", fileId, category, resourceKey);
                return null;
            }

            return await DownloadAsync(key, ct).ConfigureAwait(false);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving blob {FileId} for {Category}/{ResourceKey}", fileId, category, resourceKey);
            return null;
        }
#pragma warning restore CA1031
    }

    public async Task<bool> DeleteAsync(string fileId, BlobCategory category, string resourceKey, CancellationToken ct = default)
    {
        try
        {
            PathSecurity.ValidateIdentifier(fileId, nameof(fileId));
            PathSecurity.ValidateIdentifier(resourceKey, nameof(resourceKey));

            var matches = new List<string>();
            await foreach (var item in _container.GetBlobsAsync(BlobTraits.None, BlobStates.None, GetPrefix(fileId, category, resourceKey), ct).ConfigureAwait(false))
            {
                matches.Add(item.Name);
            }

            if (matches.Count == 0)
            {
                _logger.LogWarning("Blob not found for deletion: {FileId} in {Category}/{ResourceKey}", fileId, category, resourceKey);
                return false;
            }

            foreach (var name in matches)
            {
                await _container.DeleteBlobIfExistsAsync(name, cancellationToken: ct).ConfigureAwait(false);
                _logger.LogInformation("Deleted blob {Key}", name);
            }

            return true;
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error deleting blob {FileId} for {Category}/{ResourceKey}", fileId, category, resourceKey);
            return false;
        }
#pragma warning restore CA1031
    }

    public string GetStoragePath(string fileId, BlobCategory category, string resourceKey, string fileName)
    {
        PathSecurity.ValidateIdentifier(resourceKey, nameof(resourceKey));
        return GetKey(fileId, category, resourceKey, SanitizeFileName(fileName));
    }

    public async Task<bool> ExistsAsync(string fileId, BlobCategory category, string resourceKey, CancellationToken cancellationToken = default)
    {
        try
        {
            PathSecurity.ValidateIdentifier(fileId, nameof(fileId));
            PathSecurity.ValidateIdentifier(resourceKey, nameof(resourceKey));
            return await TryResolveKeyAsync(fileId, category, resourceKey, cancellationToken).ConfigureAwait(false) is not null;
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking blob existence {FileId} in {Category}/{ResourceKey}", fileId, category, resourceKey);
            return false;
        }
#pragma warning restore CA1031
    }

    public async Task<string?> GetPresignedDownloadUrlAsync(string fileId, BlobCategory category, string resourceKey, int? expirySeconds = null)
    {
        try
        {
            PathSecurity.ValidateIdentifier(fileId, nameof(fileId));
            PathSecurity.ValidateIdentifier(resourceKey, nameof(resourceKey));

            var key = await TryResolveKeyAsync(fileId, category, resourceKey, CancellationToken.None).ConfigureAwait(false);
            if (key is null)
            {
                _logger.LogWarning("Cannot sign URL: blob not found {FileId} in {Category}/{ResourceKey}", fileId, category, resourceKey);
                return null;
            }

            return await BuildReadSasAsync(key, expirySeconds ?? _options.SasExpirySeconds).ConfigureAwait(false);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error signing URL for {FileId} in {Category}/{ResourceKey}", fileId, category, resourceKey);
            return null;
        }
#pragma warning restore CA1031
    }

    public async Task<string?> GetPresignedUrlForRawKeyAsync(string rawKey, int? expirySeconds = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(rawKey))
            {
                return null;
            }

            // HEAD prima della firma: un URL firmato verso un blob assente renderebbe un'immagine rotta.
            var exists = await _container.GetBlobClient(rawKey).ExistsAsync(CancellationToken.None).ConfigureAwait(false);
            if (!exists.Value)
            {
                _logger.LogWarning("Cannot sign URL: raw key not found {Key}", rawKey);
                return null;
            }

            return await BuildReadSasAsync(rawKey, expirySeconds ?? _options.SasExpirySeconds).ConfigureAwait(false);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error signing URL for raw key {Key}", rawKey);
            return null;
        }
#pragma warning restore CA1031
    }

    public async Task<bool> DeleteRawKeyAsync(string rawKey, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(rawKey))
            {
                return false;
            }

            await _container.DeleteBlobIfExistsAsync(rawKey, cancellationToken: ct).ConfigureAwait(false);
            _logger.LogInformation("Deleted blob (raw key) {Key}", rawKey);
            return true;
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error deleting raw key {Key}", rawKey);
            return false;
        }
#pragma warning restore CA1031
    }

    public async Task<bool> StoreRawKeyAsync(string rawKey, Stream stream, string contentType, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawKey) || stream is null)
        {
            return false;
        }

        MemoryStream? buffer = null;
        try
        {
            var (uploadStream, _, owned) = await BufferIfNeededAsync(stream, ct).ConfigureAwait(false);
            buffer = owned;

            await EnsureContainerAsync(ct).ConfigureAwait(false);
            await _container.GetBlobClient(rawKey)
                .UploadAsync(uploadStream, new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } }, ct)
                .ConfigureAwait(false);

            _logger.LogInformation("Stored blob (raw key) {Key}", rawKey);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error storing raw key {Key}", rawKey);
            return false;
        }
#pragma warning restore CA1031
        finally
        {
            if (buffer is not null)
            {
                await buffer.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    public async Task<Stream?> RetrieveRawKeyAsync(string rawKey, CancellationToken ct = default)
    {
        try
        {
            return await DownloadAsync(rawKey, ct).ConfigureAwait(false);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving raw key {Key}", rawKey);
            return null;
        }
#pragma warning restore CA1031
    }

    // ---------------------------------------------------------------------------------------------

    private static string GetKey(string fileId, BlobCategory category, string resourceKey, string sanitizedFileName)
        => $"{category.ToS3Folder()}/{resourceKey}/{fileId}_{sanitizedFileName}";

    private static string GetPrefix(string fileId, BlobCategory category, string resourceKey)
        => $"{category.ToS3Folder()}/{resourceKey}/{fileId}_";

    private static string SanitizeFileName(string fileName)
        => StringHelper.SanitizeFilename(fileName, maxLength: 200, fallbackName: "file");

    private async Task<string?> TryResolveKeyAsync(string fileId, BlobCategory category, string resourceKey, CancellationToken ct)
    {
        var enumerator = _container
            .GetBlobsAsync(BlobTraits.None, BlobStates.None, GetPrefix(fileId, category, resourceKey), ct)
            .GetAsyncEnumerator(ct);
        await using (enumerator.ConfigureAwait(false))
        {
            return await enumerator.MoveNextAsync().ConfigureAwait(false) ? enumerator.Current.Name : null;
        }
    }

    private async Task<Stream?> DownloadAsync(string key, CancellationToken ct)
    {
        try
        {
            var response = await _container.GetBlobClient(key).DownloadStreamingAsync(cancellationToken: ct).ConfigureAwait(false);
            _logger.LogInformation("Retrieved blob {Key}", key);
            return response.Value.Content; // il chiamante dispone lo stream
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning(ex, "Blob not found {Key}", key);
            return null;
        }
    }

    private async Task EnsureContainerAsync(CancellationToken ct)
    {
        if (_containerEnsured)
        {
            return;
        }

        await _container.CreateIfNotExistsAsync(cancellationToken: ct).ConfigureAwait(false);
        _containerEnsured = true;
    }

    private static async Task<(Stream Stream, long Length, MemoryStream? Owned)> BufferIfNeededAsync(Stream stream, CancellationToken ct)
    {
        if (stream.CanSeek)
        {
            return (stream, stream.Length, null);
        }

        var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct).ConfigureAwait(false);
        buffer.Position = 0;
        return (buffer, buffer.Length, buffer);
    }

    /// <summary>
    /// Firma esplicita con <see cref="BlobSasBuilder"/> su container e chiave NOTI: niente <c>BlobUriBuilder</c>,
    /// che con un host come <c>azurite</c> (né IP né localhost) scambia l'account nel path per il container e
    /// produce una firma che Azurite rifiuta. L'URL è <c>{container.Uri}/{segmenti codificati}?{sas}</c>.
    /// </summary>
    private async Task<string> BuildReadSasAsync(string key, int expirySeconds)
    {
        var startsOn = DateTimeOffset.UtcNow.AddMinutes(-5);
        var expiresOn = DateTimeOffset.UtcNow.AddSeconds(expirySeconds);
        var sas = new BlobSasBuilder(BlobSasPermissions.Read, expiresOn)
        {
            BlobContainerName = _options.ContainerName,
            BlobName = key,
            Resource = "b",
            StartsOn = startsOn,
        };

        string query;
        if (!string.IsNullOrWhiteSpace(_options.AccountName) && !string.IsNullOrWhiteSpace(_options.AccountKey))
        {
            // Chiave condivisa (connection string: Azurite o account con chiave).
            query = sas.ToSasQueryParameters(new StorageSharedKeyCredential(_options.AccountName, _options.AccountKey)).ToString();
        }
        else
        {
            // Identità Entra (DefaultAzureCredential): SAS con chiave di delega utente.
            var delegationKey = await _serviceClient.GetUserDelegationKeyAsync(startsOn, expiresOn, CancellationToken.None).ConfigureAwait(false);
            query = sas.ToSasQueryParameters(delegationKey.Value, _serviceClient.AccountName).ToString();
        }

        var encodedKey = string.Join('/', key.Split('/').Select(Uri.EscapeDataString));
        var signed = new Uri($"{_container.Uri.AbsoluteUri.TrimEnd('/')}/{encodedKey}?{query}", UriKind.Absolute);

        _logger.LogInformation("Signed read URL for {Key} (expires in {Expiry}s)", key, expirySeconds);
        // AbsoluteUri, non ToString(): quest'ultimo decodifica i segmenti (%20 → spazio) e produce un URL non valido.
        return AlignWithPublicEndpoint(signed).AbsoluteUri;
    }

    private Uri AlignWithPublicEndpoint(Uri signed)
    {
        if (string.IsNullOrWhiteSpace(_options.PublicBlobEndpoint)
            || !Uri.TryCreate(_options.PublicBlobEndpoint, UriKind.Absolute, out var publicEndpoint))
        {
            return signed;
        }

        return new UriBuilder(signed) { Scheme = publicEndpoint.Scheme, Host = publicEndpoint.Host, Port = publicEndpoint.Port }.Uri;
    }
}
