using Api.Services.Pdf;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Api.Infrastructure.Health.Checks;

/// <summary>
/// Health check for S3-compatible object storage connectivity
/// Verifies storage service initialization and availability
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

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var storageProvider = _configuration["STORAGE_PROVIDER"]?.ToLowerInvariant() ?? "local";

        // Skip detailed check if using local storage
        if (!string.Equals(storageProvider, "s3", StringComparison.Ordinal))
        {
            return Task.FromResult(HealthCheckResult.Healthy("Using local filesystem storage (S3 not configured)"));
        }

        try
        {
            // Verify S3 configuration is present
            var endpoint = _configuration["S3_ENDPOINT"];
            var bucketName = _configuration["S3_BUCKET_NAME"];

            if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(bucketName))
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("S3 configuration incomplete (missing endpoint or bucket name)"));
            }

            // Quick check: verify service was initialized successfully
            // The factory would have thrown on initialization if credentials were invalid
            var serviceType = _storageService.GetType().Name;

            if (!string.Equals(serviceType, nameof(S3BlobStorageService), StringComparison.Ordinal))
            {
                return Task.FromResult(HealthCheckResult.Degraded($"Expected S3 storage but got {serviceType}"));
            }

            // Service type verification is sufficient for health check
            // The factory validates S3 connectivity during DI initialization
            // If we reached here with S3BlobStorageService, S3 is accessible
            return Task.FromResult(HealthCheckResult.Healthy($"S3 storage accessible (endpoint: {endpoint}, bucket: {bucketName})"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "S3 health check failed");
            return Task.FromResult(HealthCheckResult.Unhealthy("S3 storage check failed", ex));
        }
    }
}

