using Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Testcontainers.Qdrant;
using Xunit;

namespace Api.Tests;

/// <summary>
/// Base class for integration tests using TestContainers with Qdrant
/// </summary>
public abstract class QdrantIntegrationTestBase : IAsyncLifetime
{
    private readonly QdrantContainer _qdrantContainer;
    protected QdrantService QdrantService { get; private set; } = null!;
    protected string QdrantUrl => $"http://{_qdrantContainer.Hostname}:{_qdrantContainer.GetMappedPublicPort(6333)}";

    protected QdrantIntegrationTestBase()
    {
        _qdrantContainer = new QdrantBuilder()
            .WithImage("qdrant/qdrant:v1.12.4")
            .WithCleanUp(true)
            .Build();
    }

    public async Task InitializeAsync()
    {
        // Start Qdrant container
        await _qdrantContainer.StartAsync();

        // Create QdrantService with real Qdrant connection
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["QDRANT_URL"]).Returns(QdrantUrl);

        var loggerMock = new Mock<ILogger<QdrantService>>();
        QdrantService = new QdrantService(configMock.Object, loggerMock.Object);

        // Ensure collection exists for tests
        await QdrantService.EnsureCollectionExistsAsync();
    }

    public virtual async Task DisposeAsync()
    {
        // Stop and remove container
        await _qdrantContainer.DisposeAsync();
    }
}
