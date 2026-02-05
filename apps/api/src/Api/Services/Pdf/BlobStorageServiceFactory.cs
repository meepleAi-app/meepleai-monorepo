using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using System.Globalization;

namespace Api.Services.Pdf;

/// <summary>
/// Factory for creating blob storage service instances based on configuration
/// Supports local filesystem or S3-compatible object storage
/// </summary>
internal static class BlobStorageServiceFactory
{
    /// <summary>
    /// Creates a blob storage service based on the STORAGE_PROVIDER environment variable
    /// </summary>
    /// <param name="serviceProvider">Service provider for dependency resolution</param>
    /// <returns>IBlobStorageService implementation (local or S3)</returns>
    public static IBlobStorageService Create(IServiceProvider serviceProvider)
    {
        var config = serviceProvider.GetRequiredService<IConfiguration>();
        var storageProvider = config["STORAGE_PROVIDER"]?.ToLowerInvariant() ?? "local";

        return storageProvider switch
        {
            "s3" => CreateS3StorageService(serviceProvider, config),
            "local" => CreateLocalStorageService(serviceProvider, config),
            _ => CreateLocalStorageService(serviceProvider, config) // Fallback to local
        };
    }

    private static IBlobStorageService CreateS3StorageService(IServiceProvider serviceProvider, IConfiguration config)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<S3BlobStorageService>>();

        // Load S3 configuration from environment variables
        var options = new S3StorageOptions
        {
            Endpoint = config["S3_ENDPOINT"] ?? throw new InvalidOperationException("S3_ENDPOINT is required when STORAGE_PROVIDER=s3"),
            AccessKey = config["S3_ACCESS_KEY"] ?? throw new InvalidOperationException("S3_ACCESS_KEY is required when STORAGE_PROVIDER=s3"),
            SecretKey = config["S3_SECRET_KEY"] ?? throw new InvalidOperationException("S3_SECRET_KEY is required when STORAGE_PROVIDER=s3"),
            BucketName = config["S3_BUCKET_NAME"] ?? throw new InvalidOperationException("S3_BUCKET_NAME is required when STORAGE_PROVIDER=s3"),
            Region = config["S3_REGION"] ?? "auto",
            PresignedUrlExpirySeconds = int.TryParse(config["S3_PRESIGNED_URL_EXPIRY"], CultureInfo.InvariantCulture, out var expiry) ? expiry : 3600,
            EnableEncryption = !bool.TryParse(config["S3_DISABLE_ENCRYPTION"], out var disableEncryption) || !disableEncryption,
            ForcePathStyle = bool.TryParse(config["S3_FORCE_PATH_STYLE"], out var forcePathStyle) && forcePathStyle
        };

        // Create S3 client configuration
        var s3Config = new AmazonS3Config
        {
            ServiceURL = options.Endpoint,
            ForcePathStyle = options.ForcePathStyle,
            AuthenticationRegion = options.Region
        };

        // Handle region configuration
        if (!string.Equals(options.Region, "auto", StringComparison.Ordinal) && RegionEndpoint.GetBySystemName(options.Region) != null)
        {
            s3Config.RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region);
        }

        // Create S3 client with credentials
        var credentials = new BasicAWSCredentials(options.AccessKey, options.SecretKey);
        var s3Client = new AmazonS3Client(credentials, s3Config);

        logger.LogInformation(
            "Initialized S3 storage: endpoint={Endpoint}, bucket={Bucket}, region={Region}, encryption={Encryption}",
            options.Endpoint, options.BucketName, options.Region, options.EnableEncryption);

        return new S3BlobStorageService(s3Client, options, logger);
    }

    private static IBlobStorageService CreateLocalStorageService(IServiceProvider serviceProvider, IConfiguration config)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<BlobStorageService>>();
        return new BlobStorageService(config, logger);
    }
}
