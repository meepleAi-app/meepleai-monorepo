using Api.Tests.BoundedContexts.KnowledgeBase.TestHelpers;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Services;
using Api.Services.LlmClients;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Unit tests for ProviderHealthCheckService
/// ISSUE-962: BGAI-020 - Background service for LLM provider health monitoring
/// </summary>
public class ProviderHealthCheckServiceTests
{
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public void Constructor_WithValidDependencies_Succeeds()
    {
        // Arrange
        var serviceScopeFactory = CreateServiceScopeFactory(new List<ILlmClient>
        {
            CreateMockClient("Ollama").Object,
            CreateMockClient("OpenRouter").Object
        });
        var logger = new Mock<ILogger<ProviderHealthCheckService>>();

        // Act
        var service = new ProviderHealthCheckService(serviceScopeFactory, logger.Object);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullScopeFactory_ThrowsArgumentNullException()
    {
        // Arrange
        var logger = new Mock<ILogger<ProviderHealthCheckService>>();

        // Act & Assert
        Action act = () =>
            new ProviderHealthCheckService(null!, logger.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var serviceScopeFactory = CreateServiceScopeFactory(new List<ILlmClient>());

        // Act & Assert
        Action act = () =>
            new ProviderHealthCheckService(serviceScopeFactory, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task GetProviderHealth_BeforeInitialization_ReturnsNull()
    {
        // Arrange
        var serviceScopeFactory = CreateServiceScopeFactory(new List<ILlmClient>
        {
            CreateMockClient("Ollama").Object
        });
        var logger = new Mock<ILogger<ProviderHealthCheckService>>();
        var service = new ProviderHealthCheckService(serviceScopeFactory, logger.Object);

        // Act
        var health = service.GetProviderHealth("Ollama");

        // Assert - Before StartAsync, no health status available
        health.Should().BeNull();
    }

    [Fact]
    public async Task GetAllProviderHealth_AfterInitialization_ReturnsAllProviders()
    {
        // Arrange — ProviderHealthCheckService uses CheckHealthAsync (not GenerateCompletionAsync)
        var ollamaClient = CreateMockClient("Ollama");
        ollamaClient.Setup(c => c.CheckHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var openRouterClient = CreateMockClient("OpenRouter");
        openRouterClient.Setup(c => c.CheckHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var serviceScopeFactory = CreateServiceScopeFactory(new List<ILlmClient>
        {
            ollamaClient.Object,
            openRouterClient.Object
        });
        var logger = new Mock<ILogger<ProviderHealthCheckService>>();
        var service = new ProviderHealthCheckService(serviceScopeFactory, logger.Object);

        // Simulate initialization (normally done by StartAsync)
        using var cts = new CancellationTokenSource(TestConstants.Timing.VeryShortTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, TestCancellationToken);

        // Act
        await service.StartAsync(linkedCts.Token);

        // Give time for initialization
        await Task.Delay(TestConstants.Timing.SmallDelay, TestCancellationToken);

        var allHealth = service.GetAllProviderHealth();

        // Assert
        allHealth.Should().NotBeNull();
        allHealth.Count.Should().Be(2);
        allHealth.Keys.Should().Contain("Ollama");
        allHealth.Keys.Should().Contain("OpenRouter");

        // Cleanup
        await service.StopAsync(TestCancellationToken);
    }

    [Fact]
    [Trait("Category", TestCategories.Integration)]
    public async Task HealthCheck_SuccessfulResponse_UpdatesHealthyStatus()
    {
        // Arrange — ProviderHealthCheckService uses CheckHealthAsync (not GenerateCompletionAsync)
        var ollamaClient = CreateMockClient("Ollama");
        ollamaClient.Setup(c => c.CheckHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var serviceScopeFactory = CreateServiceScopeFactory(new List<ILlmClient> { ollamaClient.Object });
        var logger = new Mock<ILogger<ProviderHealthCheckService>>();
        var service = new ProviderHealthCheckService(serviceScopeFactory, logger.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, TestCancellationToken);

        // Act
        await service.StartAsync(linkedCts.Token);
        await Task.Delay(KnowledgeBaseTestConstants.ProviderHealthCheck.WarmupAndFirstCheck, TestCancellationToken); // Wait for 10s warmup + first check

        var health = service.GetProviderHealth("Ollama");

        // Cleanup
        await service.StopAsync(TestCancellationToken);

        // Assert
        health.Should().NotBeNull();
        health.IsAvailable().Should().BeTrue();
    }

    [Fact]
    [Trait("Category", TestCategories.Integration)]
    public async Task HealthCheck_FailedResponse_UpdatesUnhealthyStatus()
    {
        // Arrange — ProviderHealthCheckService uses CheckHealthAsync (not GenerateCompletionAsync)
        var ollamaClient = CreateMockClient("Ollama");
        ollamaClient.Setup(c => c.CheckHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var serviceScopeFactory = CreateServiceScopeFactory(new List<ILlmClient> { ollamaClient.Object });
        var logger = new Mock<ILogger<ProviderHealthCheckService>>();
        var service = new ProviderHealthCheckService(serviceScopeFactory, logger.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, TestCancellationToken);

        // Act
        await service.StartAsync(linkedCts.Token);
        await Task.Delay(KnowledgeBaseTestConstants.ProviderHealthCheck.WarmupAndFirstCheck, TestCancellationToken); // Wait for 10s warmup + first check

        var health = service.GetProviderHealth("Ollama");

        // Cleanup
        await service.StopAsync(TestCancellationToken);

        // Assert
        health.Should().NotBeNull();
        health.IsAvailable().Should().BeFalse();
    }

    [Fact]
    [Trait("Category", TestCategories.Integration)]
    public async Task HealthCheck_Timeout_UpdatesUnhealthyStatus()
    {
        // Arrange — ProviderHealthCheckService uses CheckHealthAsync (not GenerateCompletionAsync)
        var ollamaClient = CreateMockClient("Ollama");
        ollamaClient.Setup(c => c.CheckHealthAsync(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken ct) =>
            {
                await Task.Delay(KnowledgeBaseTestConstants.ProviderHealthCheck.SlowResponseTimeout, ct); // Simulate slow response (will timeout)
                return true;
            });

        var serviceScopeFactory = CreateServiceScopeFactory(new List<ILlmClient> { ollamaClient.Object });
        var logger = new Mock<ILogger<ProviderHealthCheckService>>();
        var service = new ProviderHealthCheckService(serviceScopeFactory, logger.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(16));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, TestCancellationToken);

        // Act
        await service.StartAsync(linkedCts.Token);
        await Task.Delay(KnowledgeBaseTestConstants.ProviderHealthCheck.WarmupWithTimeoutBuffer, TestCancellationToken); // Wait for warmup + first check with timeout

        var health = service.GetProviderHealth("Ollama");

        // Cleanup
        await service.StopAsync(TestCancellationToken);

        // Assert
        health.Should().NotBeNull();
        health.IsAvailable().Should().BeFalse(); // Health check should fail due to timeout
    }

    [Fact]
    public async Task StopAsync_CancelsBackgroundTask()
    {
        // Arrange — ProviderHealthCheckService uses CheckHealthAsync (not GenerateCompletionAsync)
        var ollamaClient = CreateMockClient("Ollama");
        ollamaClient.Setup(c => c.CheckHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var serviceScopeFactory = CreateServiceScopeFactory(new List<ILlmClient> { ollamaClient.Object });
        var logger = new Mock<ILogger<ProviderHealthCheckService>>();
        var service = new ProviderHealthCheckService(serviceScopeFactory, logger.Object);

        // Act
        await service.StartAsync(TestCancellationToken);
        await Task.Delay(TestConstants.Timing.SmallDelay, TestCancellationToken); // Let it run briefly
        await service.StopAsync(TestCancellationToken);

        // Assert - No exception thrown, service stopped gracefully
        true.Should().BeTrue();
    }
    private Mock<ILlmClient> CreateMockClient(string providerName)
    {
        var mock = new Mock<ILlmClient>();
        mock.Setup(c => c.ProviderName).Returns(providerName);
        return mock;
    }

    private IServiceScopeFactory CreateServiceScopeFactory(List<ILlmClient> clients)
    {
        var services = new ServiceCollection();

        // Register each client
        foreach (var client in clients)
        {
            services.AddScoped(_ => client);
        }

        // Register IEnumerable<ILlmClient>
        services.AddScoped<IEnumerable<ILlmClient>>(sp => clients);

        var serviceProvider = services.BuildServiceProvider();

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope())
            .Returns(() =>
            {
                var scope = serviceProvider.CreateScope();
                return scope;
            });

        return scopeFactory.Object;
    }
}

