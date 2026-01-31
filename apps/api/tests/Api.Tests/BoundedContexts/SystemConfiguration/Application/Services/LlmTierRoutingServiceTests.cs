using Api.BoundedContexts.SystemConfiguration.Application.Services;
using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.Enums;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Application.Services;

/// <summary>
/// Unit tests for LlmTierRoutingService.
/// Issue #2596: LLM tier routing with test/production model separation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class LlmTierRoutingServiceTests
{
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock;
    private readonly Mock<IServiceScope> _scopeMock;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<IAiModelConfigurationRepository> _repositoryMock;
    private readonly Mock<IHybridCacheService> _cacheMock;
    private readonly Mock<IHostEnvironment> _hostEnvironmentMock;
    private readonly Mock<ILogger<LlmTierRoutingService>> _loggerMock;
    private readonly LlmTierRoutingService _service;

    public LlmTierRoutingServiceTests()
    {
        _scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _scopeMock = new Mock<IServiceScope>();
        _serviceProviderMock = new Mock<IServiceProvider>();
        _repositoryMock = new Mock<IAiModelConfigurationRepository>();
        _cacheMock = new Mock<IHybridCacheService>();
        _hostEnvironmentMock = new Mock<IHostEnvironment>();
        _loggerMock = new Mock<ILogger<LlmTierRoutingService>>();

        // Setup service scope factory chain
        _scopeFactoryMock.Setup(f => f.CreateScope()).Returns(_scopeMock.Object);
        _scopeMock.Setup(s => s.ServiceProvider).Returns(_serviceProviderMock.Object);
        _serviceProviderMock
            .Setup(p => p.GetService(typeof(IAiModelConfigurationRepository)))
            .Returns(_repositoryMock.Object);

        _service = new LlmTierRoutingService(
            _scopeFactoryMock.Object,
            _cacheMock.Object,
            _hostEnvironmentMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task GetModelForTierAsync_ProductionEnvironment_UsesProductionEnvironmentType()
    {
        // Arrange
        var tier = LlmUserTier.Anonymous;
        var expectedModel = CreateTestModel("test-model", LlmEnvironmentType.Production);

        _hostEnvironmentMock.Setup(h => h.EnvironmentName).Returns("Production");

        _cacheMock.Setup(c => c.GetOrCreateAsync(
                It.Is<string>(k => k.Contains($"tier-model:{tier}:{LlmEnvironmentType.Production}")),
                It.IsAny<Func<CancellationToken, Task<AiModelConfiguration>>>(),
                It.IsAny<string[]>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedModel);

        // Act
        var result = await _service.GetModelForTierAsync(tier);

        // Assert
        result.Should().NotBeNull();
        result!.ModelId.Should().Be("test-model");
    }

    [Fact]
    public async Task GetModelForTierAsync_DevelopmentEnvironment_UsesTestEnvironmentType()
    {
        // Arrange
        var tier = LlmUserTier.User;
        var expectedModel = CreateTestModel("test-model", LlmEnvironmentType.Test);

        _hostEnvironmentMock.Setup(h => h.EnvironmentName).Returns("Development");

        _cacheMock.Setup(c => c.GetOrCreateAsync(
                It.Is<string>(k => k.Contains($"tier-model:{tier}:{LlmEnvironmentType.Test}")),
                It.IsAny<Func<CancellationToken, Task<AiModelConfiguration>>>(),
                It.IsAny<string[]>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedModel);

        // Act
        var result = await _service.GetModelForTierAsync(tier);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetTestModelForTierAsync_AlwaysUsesTestEnvironment()
    {
        // Arrange
        var tier = LlmUserTier.Admin;
        var expectedModel = CreateTestModel("test-admin-model", LlmEnvironmentType.Test);

        _cacheMock.Setup(c => c.GetOrCreateAsync(
                It.Is<string>(k => k.Contains($"tier-model:{tier}:{LlmEnvironmentType.Test}")),
                It.IsAny<Func<CancellationToken, Task<AiModelConfiguration>>>(),
                It.IsAny<string[]>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedModel);

        // Act
        var result = await _service.GetTestModelForTierAsync(tier);

        // Assert
        result.Should().NotBeNull();
        result!.ModelId.Should().Be("test-admin-model");
    }

    [Fact]
    public async Task GetAllTierRoutingsAsync_ReturnsAllTierConfigurations()
    {
        // Arrange
        var expectedRoutings = new List<TierRoutingInfo>
        {
            new() { Tier = LlmUserTier.Anonymous, ProductionModelId = "prod-anon", TestModelId = "test-anon" },
            new() { Tier = LlmUserTier.User, ProductionModelId = "prod-user", TestModelId = "test-user" }
        };

        // Use a callback to invoke the factory and return expected results
        _cacheMock.Setup(c => c.GetOrCreateAsync(
                It.Is<string>(k => k == "tier-routing:all"),
                It.IsAny<Func<CancellationToken, Task<List<TierRoutingInfo>>>>(),
                It.IsAny<string[]>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedRoutings);

        // Act
        var result = await _service.GetAllTierRoutingsAsync();

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task InvalidateCacheAsync_RemovesByTag()
    {
        // Arrange & Act
        await _service.InvalidateCacheAsync();

        // Assert
        _cacheMock.Verify(c => c.RemoveByTagAsync("tier-routing", It.IsAny<CancellationToken>()), Times.Once);
    }

    private static AiModelConfiguration CreateTestModel(string modelId, LlmEnvironmentType environment)
    {
        var model = AiModelConfiguration.Create(modelId, $"Test {modelId}", "OpenRouter", 1, ModelSettings.Default);
        model.SetTierRouting(LlmUserTier.Anonymous, environment, true);
        return model;
    }
}
