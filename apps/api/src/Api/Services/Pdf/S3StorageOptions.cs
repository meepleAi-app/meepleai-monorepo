namespace Api.Services.Pdf;

/// <summary>
/// Configuration options for S3-compatible object storage
/// Supports multiple providers: Cloudflare R2, AWS S3, Backblaze B2, MinIO, DigitalOcean Spaces
/// </summary>
internal sealed class S3StorageOptions
{
    /// <summary>
    /// S3-compatible endpoint URL (e.g., https://account-id.r2.cloudflarestorage.com for Cloudflare R2)
    /// </summary>
    public required string Endpoint { get; init; }

    /// <summary>
    /// S3 access key ID
    /// </summary>
    public required string AccessKey { get; init; }

    /// <summary>
    /// S3 secret access key
    /// </summary>
    public required string SecretKey { get; init; }

    /// <summary>
    /// S3 bucket name for storing files
    /// </summary>
    public required string BucketName { get; init; }

    /// <summary>
    /// AWS region (use "auto" for Cloudflare R2, specific region for AWS/others)
    /// </summary>
    public string Region { get; init; } = "auto";

    /// <summary>
    /// Pre-signed URL expiration time in seconds (default: 1 hour)
    /// </summary>
    public int PresignedUrlExpirySeconds { get; init; } = 3600;

    /// <summary>
    /// Enable server-side encryption (SSE-S3/AES256)
    /// </summary>
    public bool EnableEncryption { get; init; } = true;

    /// <summary>
    /// Force path-style URLs (required for MinIO and some S3-compatible services)
    /// </summary>
    public bool ForcePathStyle { get; init; }
}
