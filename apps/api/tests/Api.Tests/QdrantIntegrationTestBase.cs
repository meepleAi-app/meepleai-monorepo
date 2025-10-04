using Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Testcontainers.Qdrant;
using Xunit;

namespace Api.Tests;

/// <summary>
/// Base class for integration tests using TestContainers with Qdrant (local) or service containers (CI)
/// </summary>
public abstract class QdrantIntegrationTestBase : IAsyncLifetime
{
    private readonly QdrantContainer? _qdrantContainer;
    private readonly bool _isRunningInCi;
    private string _qdrantUrl;

    protected QdrantService QdrantService { get; private set; } = null!;
    protected string QdrantUrl => _qdrantUrl;

    protected QdrantIntegrationTestBase()
    {
        // Detect CI environment
        _isRunningInCi = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")) ||
                         !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"));

        if (_isRunningInCi)
        {
            // In CI: Use service container from environment variable
            _qdrantUrl = Environment.GetEnvironmentVariable("QDRANT_URL") ?? "http://localhost:6333";
            _qdrantContainer = null;
        }
        else
        {
            // Local: Use TestContainers
            _qdrantContainer = new QdrantBuilder()
                .WithImage("qdrant/qdrant:v1.12.4")
                .WithCleanUp(true)
                .Build();
            _qdrantUrl = string.Empty; // Will be set after container starts
        }
    }

    public async Task InitializeAsync()
    {
        if (!_isRunningInCi && _qdrantContainer != null)
        {
            // Start Qdrant container (local development only)
            await _qdrantContainer.StartAsync();
            _qdrantUrl = $"http://{_qdrantContainer.Hostname}:{_qdrantContainer.GetMappedPublicPort(6333)}";
        }

        // Create QdrantService with Qdrant connection (works for both CI and local)
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["QDRANT_URL"]).Returns(_qdrantUrl);

        var loggerMock = new Mock<ILogger<QdrantService>>();
        QdrantService = new QdrantService(configMock.Object, loggerMock.Object);

        // Ensure collection exists for tests
        await QdrantService.EnsureCollectionExistsAsync();
    }

    public virtual async Task DisposeAsync()
    {
        if (_qdrantContainer != null)
        {
            // Stop and remove container (local development only)
            await _qdrantContainer.DisposeAsync();
        }
    }
}
