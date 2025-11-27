using Xunit;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Collection definition for PDF pipeline integration tests.
/// Tests in this collection run sequentially to avoid Docker container port conflicts
/// and resource contention during PDF processing operations.
/// </summary>
[CollectionDefinition("PdfPipeline", DisableParallelization = true)]
public class PdfPipelineCollectionDefinition
{
}
