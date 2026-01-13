using Microsoft.Extensions.Diagnostics.HealthChecks;
using Api.Services;

namespace Api.Infrastructure;

internal class QdrantHealthCheck : IHealthCheck
{
    private readonly IQdrantService _qdrantService;
    private readonly ILogger<QdrantHealthCheck> _logger;

    public QdrantHealthCheck(IQdrantService qdrantService, ILogger<QdrantHealthCheck> logger)
    {
        _qdrantService = qdrantService;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Try to check if collection exists (lightweight operation)
            var collectionExists = await _qdrantService.CollectionExistsAsync(cancellationToken).ConfigureAwait(false);

            if (collectionExists)
            {
                return HealthCheckResult.Healthy("Qdrant collection is accessible");
            }
            else
            {
                return HealthCheckResult.Degraded("Qdrant is accessible but collection does not exist");
            }
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // HEALTH CHECK PATTERN: Must return Unhealthy status instead of throwing
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogError(ex, "Qdrant health check failed");
            return HealthCheckResult.Unhealthy("Qdrant is not accessible", ex);
        }
#pragma warning restore CA1031
    }
}