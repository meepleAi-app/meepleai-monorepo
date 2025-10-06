using System;
using System.Collections.Generic;
using Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Testcontainers.Qdrant;
using Xunit;
using Xunit.Sdk;

namespace Api.Tests;

/// <summary>
/// Base class for integration tests using TestContainers with Qdrant (local) or service containers (CI)
/// </summary>
public abstract class QdrantIntegrationTestBase : IAsyncLifetime
{
    private readonly QdrantContainer? _qdrantContainer;
    private readonly bool _isRunningInCi;
    private readonly string? _configuredQdrantUrl;
    private string? _skipReason;
    private string _qdrantUrl;
    private bool _containerStarted;

    protected QdrantService QdrantService { get; private set; } = null!;
    protected string QdrantUrl => _qdrantUrl;

    protected QdrantIntegrationTestBase()
    {
        // Detect CI environment
        _isRunningInCi = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")) ||
                         !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"));

        _configuredQdrantUrl = Environment.GetEnvironmentVariable("QDRANT_URL");

        if (!string.IsNullOrWhiteSpace(_configuredQdrantUrl))
        {
            _qdrantUrl = _configuredQdrantUrl;
            _qdrantContainer = null;
            return;
        }

        if (_isRunningInCi)
        {
            _qdrantUrl = "http://localhost:6333";
            _qdrantContainer = null;
            return;
        }

        // Local environment without explicit endpoint: use Testcontainers
        _qdrantContainer = new QdrantBuilder()
            .WithImage("qdrant/qdrant:v1.12.4")
            .WithCleanUp(true)
            .Build();
        _qdrantUrl = string.Empty; // Will be set after container starts
    }

    public async Task InitializeAsync()
    {
        if (_skipReason is not null)
        {
            throw new SkipException(_skipReason);
        }

        if (_qdrantContainer != null)
        {
            try
            {
                // Start Qdrant container (local development only)
                await _qdrantContainer.StartAsync();
                _containerStarted = true;
                _qdrantUrl = $"http://{_qdrantContainer.Hostname}:{_qdrantContainer.GetMappedPublicPort(6333)}";
            }
            catch (Exception ex) when (string.IsNullOrWhiteSpace(_configuredQdrantUrl))
            {
                _skipReason =
                    $"Qdrant integration tests skipped: failed to start local Testcontainers instance and no QDRANT_URL was provided. {ex.Message}";
                throw new SkipException(_skipReason);
            }
        }

        if (string.IsNullOrWhiteSpace(_qdrantUrl))
        {
            _skipReason =
                "Qdrant integration tests skipped: no QDRANT_URL configured and Docker/Testcontainers is unavailable.";
            throw new SkipException(_skipReason);
        }

        // Create QdrantService with Qdrant connection (works for both CI and local)
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["QDRANT_URL"] = _qdrantUrl
            })
            .Build();

        var adapterLogger = new Mock<ILogger<QdrantClientAdapter>>();
        var clientAdapter = new QdrantClientAdapter(configuration, adapterLogger.Object);

        var loggerMock = new Mock<ILogger<QdrantService>>();
        QdrantService = new QdrantService(clientAdapter, loggerMock.Object);

        // Ensure collection exists for tests
        await QdrantService.EnsureCollectionExistsAsync();
    }

    public virtual async Task DisposeAsync()
    {
        if (_containerStarted && _qdrantContainer != null)
        {
            // Stop and remove container (local development only)
            await _qdrantContainer.DisposeAsync();
        }
    }
}
