using Amazon.S3.Model;
using Api.Services.Pdf;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Api.Infrastructure.Health.Checks;

/// <summary>
/// Health check for S3-compatible object storage connectivity.
/// Performs real S3 API call (ListObjectsV2) to verify bucket accessibility.
/// </summary>
internal sealed class S3StorageHealthCheck : IHealthCheck
{
    private readonly IBlobStorageService _storageService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<S3StorageHealthCheck> _logger;

    public S3StorageHealthCheck(
        IBlobStorageService storageService,
        IConfiguration configuration,
        ILogger<S3StorageHealthCheck> logger)
    {
        _storageService = storageService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var storageProvider = _configuration["STORAGE_PROVIDER"]?.ToLowerInvariant() ?? "local";

        // Skip detailed check if using local storage
        if (!string.Equals(storageProvider, "s3", StringComparison.Ordinal))
        {
            return HealthCheckResult.Healthy("Using local filesystem storage (S3 not configured)");
        }

        try
        {
            // Verify the DI resolved the correct service type
            if (_storageService is not S3BlobStorageService s3Service)
            {
                var actualType = _storageService.GetType().Name;
                return HealthCheckResult.Degraded($"Expected S3 storage but got {actualType}");
            }

            // Real connectivity check: list objects with MaxKeys=1 to verify
            // credentials, endpoint, and bucket access with minimal overhead
            var listRequest = new ListObjectsV2Request
            {
                BucketName = s3Service.Options.BucketName,
                MaxKeys = 1
            };

            await s3Service.S3Client
                .ListObjectsV2Async(listRequest, cancellationToken)
                .ConfigureAwait(false);

            return HealthCheckResult.Healthy(
                $"S3 storage accessible (endpoint: {s3Service.Options.Endpoint}, bucket: {s3Service.Options.BucketName})");
        }
#pragma warning disable CA1031 // Service boundary: health check must never throw
        catch (Exception ex)
        {
            _logger.LogError(ex, "S3 health check failed");
            return HealthCheckResult.Unhealthy($"S3 storage unreachable: {ex.Message}", ex);
        }
#pragma warning restore CA1031
    }
}
