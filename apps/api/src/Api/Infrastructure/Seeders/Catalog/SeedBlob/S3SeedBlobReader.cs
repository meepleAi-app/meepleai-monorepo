using System.Net;
using Amazon.S3;

namespace Api.Infrastructure.Seeders.Catalog.SeedBlob;

/// <summary>
/// ISeedBlobReader backed by an S3-compatible client (AWS S3 or Cloudflare R2).
/// Uses the readonly credentials provided via SEED_BUCKET_* env vars.
/// </summary>
internal sealed class S3SeedBlobReader : ISeedBlobReader
{
    private readonly IAmazonS3 _client;
    private readonly string _bucket;

    public S3SeedBlobReader(IAmazonS3 client, string bucket)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _bucket = bucket ?? throw new ArgumentNullException(nameof(bucket));
    }

    public bool IsConfigured => true;

    public async Task<Stream> OpenReadAsync(string blobKey, CancellationToken ct)
    {
        var response = await _client
            .GetObjectAsync(_bucket, blobKey, ct)
            .ConfigureAwait(false);
        return response.ResponseStream;
    }

    public async Task<bool> ExistsAsync(string blobKey, CancellationToken ct)
    {
        try
        {
            await _client
                .GetObjectMetadataAsync(_bucket, blobKey, ct)
                .ConfigureAwait(false);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
        catch (AmazonS3Exception ex) when (
            ex.StatusCode == HttpStatusCode.Forbidden ||
            ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            // Credentials/policy issue — surface clearly so the operator knows
            // it is a configuration problem, not a missing blob.
            throw new InvalidOperationException(
                $"Seed bucket access denied for key '{blobKey}'. "
                + "Verify SEED_BUCKET_ACCESS_KEY / SEED_BUCKET_SECRET_KEY and bucket policy.",
                ex);
        }
    }
}
