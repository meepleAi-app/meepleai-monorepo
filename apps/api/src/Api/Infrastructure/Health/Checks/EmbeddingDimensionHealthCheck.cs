using Api.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Api.Infrastructure.Health.Checks;

/// <summary>
/// Health check that validates the configured embedding provider dimensions
/// match the pgvector_embeddings table schema (vector(768)).
/// Prevents silent RAG failures from dimension mismatches.
/// </summary>
public class EmbeddingDimensionHealthCheck : IHealthCheck
{
    /// <summary>
    /// The vector dimension defined in the pgvector_embeddings table schema.
    /// Must match PgVectorEmbeddingEntityConfiguration: HasColumnType("vector(768)").
    /// </summary>
    internal const int ExpectedSchemaDimensions = 768;

    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<EmbeddingDimensionHealthCheck> _logger;

    // Constructor is internal because IEmbeddingService is internal (CS0051).
    // ActivatorUtilities resolves internal constructors within the same assembly.
    internal EmbeddingDimensionHealthCheck(
        IEmbeddingService embeddingService,
        ILogger<EmbeddingDimensionHealthCheck> logger)
    {
        _embeddingService = embeddingService;
        _logger = logger;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var providerDimensions = _embeddingService.GetEmbeddingDimensions();

            if (providerDimensions == ExpectedSchemaDimensions)
            {
                return Task.FromResult(HealthCheckResult.Healthy(
                    $"Embedding dimensions match: provider={providerDimensions}, schema={ExpectedSchemaDimensions}"));
            }

            _logger.LogError(
                "CRITICAL: Embedding dimension mismatch! Provider outputs {ProviderDims}-dim vectors " +
                "but pgvector_embeddings schema expects {SchemaDims}-dim. " +
                "RAG indexing and search will fail. Change Embedding:Provider config or run a schema migration.",
                providerDimensions, ExpectedSchemaDimensions);

            return Task.FromResult(HealthCheckResult.Unhealthy(
                $"Embedding dimension mismatch: provider={providerDimensions}, schema expects {ExpectedSchemaDimensions}. " +
                $"Change Embedding:Provider in config or run a schema migration to vector({providerDimensions})."));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not verify embedding dimensions — service may be unavailable");
            return Task.FromResult(HealthCheckResult.Degraded(
                "Cannot verify embedding dimensions — embedding service unavailable", ex));
        }
    }
}
