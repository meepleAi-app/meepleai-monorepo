// =============================================================================
// MeepleAI - RAG Plugin System Tests
// Issue #3417 - Plugin Registry Service
// =============================================================================

using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Base;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Contracts;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Registry;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Plugins.Registry;

public class PluginRegistryTests
{
    private readonly Mock<ILogger<PluginRegistry>> _loggerMock;
    private readonly IServiceProvider _serviceProvider;
    private readonly PluginRegistryOptions _options;

    public PluginRegistryTests()
    {
        _loggerMock = new Mock<ILogger<PluginRegistry>>();
        _options = new PluginRegistryOptions
        {
            // Scan the test assembly for test plugins
            AssembliesToScan = new List<string> { typeof(PluginRegistryTests).Assembly.GetName().Name! }
        };

        // Setup service provider with test plugins
        var services = new ServiceCollection();
        services.AddTransient<TestPlugin>();
        services.AddTransient<AnotherTestPlugin>();
        services.AddTransient<DisabledTestPlugin>();
        _serviceProvider = services.BuildServiceProvider();
    }

    private PluginRegistry CreateRegistry()
    {
        return new PluginRegistry(
            _serviceProvider,
            _loggerMock.Object,
            Options.Create(_options));
    }

    #region Discovery Tests

    [Fact]
    public void GetAllPlugins_ReturnsRegisteredPlugins()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var plugins = registry.GetAllPlugins();

        // Assert
        plugins.Should().NotBeEmpty();
        plugins.Should().Contain(p => p.Id == "test-plugin-v1");
    }

    [Fact]
    public void GetPluginsByCategory_FiltersCorrectly()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var transformPlugins = registry.GetPluginsByCategory(PluginCategory.Transform);

        // Assert
        transformPlugins.Should().NotBeEmpty();
        transformPlugins.Should().OnlyContain(p => p.Category == PluginCategory.Transform);
    }

    [Fact]
    public void GetPlugin_WithValidId_ReturnsMetadata()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var plugin = registry.GetPlugin("test-plugin-v1");

        // Assert
        plugin.Should().NotBeNull();
        plugin!.Id.Should().Be("test-plugin-v1");
        plugin.Name.Should().Be("Test Plugin");
        plugin.Category.Should().Be(PluginCategory.Transform);
    }

    [Fact]
    public void GetPlugin_WithInvalidId_ReturnsNull()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var plugin = registry.GetPlugin("nonexistent-plugin");

        // Assert
        plugin.Should().BeNull();
    }

    [Fact]
    public void GetPlugin_WithVersion_ReturnsSpecificVersion()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var plugin = registry.GetPlugin("test-plugin-v1", "1.0.0");

        // Assert
        plugin.Should().NotBeNull();
        plugin!.Version.Should().Be("1.0.0");
    }

    [Fact]
    public void IsRegistered_WithValidId_ReturnsTrue()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var isRegistered = registry.IsRegistered("test-plugin-v1");

        // Assert
        isRegistered.Should().BeTrue();
    }

    [Fact]
    public void IsRegistered_WithInvalidId_ReturnsFalse()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var isRegistered = registry.IsRegistered("nonexistent-plugin");

        // Assert
        isRegistered.Should().BeFalse();
    }

    #endregion

    #region Loading Tests

    [Fact]
    public void LoadPlugin_WithValidId_ReturnsPluginInstance()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var plugin = registry.LoadPlugin("test-plugin-v1");

        // Assert
        plugin.Should().NotBeNull();
        plugin.Should().BeOfType<TestPlugin>();
    }

    [Fact]
    public void LoadPlugin_WithInvalidId_ThrowsPluginNotFoundException()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var act = () => registry.LoadPlugin("nonexistent-plugin");

        // Assert
        act.Should().Throw<PluginNotFoundException>()
            .WithMessage("*nonexistent-plugin*not found*");
    }

    [Fact]
    public void LoadPlugin_WhenDisabled_ThrowsPluginDisabledException()
    {
        // Arrange
        var registry = CreateRegistry();
        registry.DisablePluginAsync("test-plugin-v1").GetAwaiter().GetResult();

        // Act
        var act = () => registry.LoadPlugin("test-plugin-v1");

        // Assert
        act.Should().Throw<PluginDisabledException>()
            .WithMessage("*test-plugin-v1*disabled*");
    }

    [Fact]
    public void TryLoadPlugin_WithValidId_ReturnsTrueAndPlugin()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var result = registry.TryLoadPlugin("test-plugin-v1", out var plugin);

        // Assert
        result.Should().BeTrue();
        plugin.Should().NotBeNull();
    }

    [Fact]
    public void TryLoadPlugin_WithInvalidId_ReturnsFalseAndNull()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var result = registry.TryLoadPlugin("nonexistent-plugin", out var plugin);

        // Assert
        result.Should().BeFalse();
        plugin.Should().BeNull();
    }

    #endregion

    #region Health Tests

    [Fact]
    public async Task GetHealthReportAsync_ReturnsReportForAllPlugins()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var report = await registry.GetHealthReportAsync();

        // Assert
        report.Should().NotBeNull();
        report.TotalPlugins.Should().BeGreaterThan(0);
        report.Plugins.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetHealthReportAsync_IncludesHealthCounts()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var report = await registry.GetHealthReportAsync();

        // Assert
        report.HealthyCount.Should().BeGreaterThanOrEqualTo(0);
        report.DegradedCount.Should().BeGreaterThanOrEqualTo(0);
        report.UnhealthyCount.Should().BeGreaterThanOrEqualTo(0);
        (report.HealthyCount + report.DegradedCount + report.UnhealthyCount + report.DisabledCount)
            .Should().Be(report.TotalPlugins);
    }

    [Fact]
    public async Task CheckPluginHealthAsync_WithValidPlugin_ReturnsHealthResult()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var result = await registry.CheckPluginHealthAsync("test-plugin-v1");

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckPluginHealthAsync_WithInvalidPlugin_ReturnsUnknown()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var result = await registry.CheckPluginHealthAsync("nonexistent-plugin");

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(HealthStatus.Unknown);
    }

    #endregion

    #region Management Tests

    [Fact]
    public async Task EnablePluginAsync_EnablesDisabledPlugin()
    {
        // Arrange
        var registry = CreateRegistry();
        await registry.DisablePluginAsync("test-plugin-v1");

        // Act
        await registry.EnablePluginAsync("test-plugin-v1");

        // Assert
        var act = () => registry.LoadPlugin("test-plugin-v1");
        act.Should().NotThrow();
    }

    [Fact]
    public async Task DisablePluginAsync_DisablesPlugin()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        await registry.DisablePluginAsync("test-plugin-v1");

        // Assert
        var act = () => registry.LoadPlugin("test-plugin-v1");
        act.Should().Throw<PluginDisabledException>();
    }

    [Fact]
    public async Task EnablePluginAsync_WithInvalidId_ThrowsPluginNotFoundException()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var act = async () => await registry.EnablePluginAsync("nonexistent-plugin");

        // Assert
        await act.Should().ThrowAsync<PluginNotFoundException>();
    }

    [Fact]
    public async Task DisablePluginAsync_WithInvalidId_ThrowsPluginNotFoundException()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var act = async () => await registry.DisablePluginAsync("nonexistent-plugin");

        // Assert
        await act.Should().ThrowAsync<PluginNotFoundException>();
    }

    [Fact]
    public async Task RefreshAsync_ClearsAndReloadsPlugins()
    {
        // Arrange
        var registry = CreateRegistry();
        var initialCount = registry.GetAllPlugins().Count;

        // Act
        await registry.RefreshAsync();

        // Assert
        var afterRefreshCount = registry.GetAllPlugins().Count;
        afterRefreshCount.Should().Be(initialCount);
    }

    #endregion

    #region Test Plugins

    [RagPlugin("test-plugin-v1", Category = PluginCategory.Transform, Name = "Test Plugin")]
    private sealed class TestPlugin : IRagPlugin
    {
        private static readonly JsonDocument EmptySchema = JsonDocument.Parse("{}");

        public string Id => "test-plugin-v1";
        public string Name => "Test Plugin";
        public string Version => "1.0.0";
        public PluginCategory Category => PluginCategory.Transform;

        public PluginMetadata Metadata => PluginMetadata.Create(
            "test-plugin-v1", "Test Plugin", "1.0.0", PluginCategory.Transform);

        public JsonDocument InputSchema => EmptySchema;
        public JsonDocument OutputSchema => EmptySchema;
        public JsonDocument ConfigSchema => EmptySchema;

        public Task<PluginOutput> ExecuteAsync(PluginInput input, PluginConfig? config = null, CancellationToken cancellationToken = default)
            => Task.FromResult(PluginOutput.Successful(input.ExecutionId, input.Payload));

        public Task<HealthCheckResult> HealthCheckAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(HealthCheckResult.Healthy());

        public ValidationResult ValidateConfig(PluginConfig config) => ValidationResult.Success();
        public ValidationResult ValidateInput(PluginInput input) => ValidationResult.Success();
    }

    [RagPlugin("another-plugin-v1", Category = PluginCategory.Retrieval, Name = "Another Plugin")]
    private sealed class AnotherTestPlugin : IRagPlugin
    {
        private static readonly JsonDocument EmptySchema = JsonDocument.Parse("{}");

        public string Id => "another-plugin-v1";
        public string Name => "Another Plugin";
        public string Version => "1.0.0";
        public PluginCategory Category => PluginCategory.Retrieval;

        public PluginMetadata Metadata => PluginMetadata.Create(
            "another-plugin-v1", "Another Plugin", "1.0.0", PluginCategory.Retrieval);

        public JsonDocument InputSchema => EmptySchema;
        public JsonDocument OutputSchema => EmptySchema;
        public JsonDocument ConfigSchema => EmptySchema;

        public Task<PluginOutput> ExecuteAsync(PluginInput input, PluginConfig? config = null, CancellationToken cancellationToken = default)
            => Task.FromResult(PluginOutput.Successful(input.ExecutionId, input.Payload));

        public Task<HealthCheckResult> HealthCheckAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(HealthCheckResult.Healthy());

        public ValidationResult ValidateConfig(PluginConfig config) => ValidationResult.Success();
        public ValidationResult ValidateInput(PluginInput input) => ValidationResult.Success();
    }

    [RagPlugin("disabled-plugin-v1", Category = PluginCategory.Transform, Name = "Disabled Plugin")]
    private sealed class DisabledTestPlugin : IRagPlugin
    {
        private static readonly JsonDocument EmptySchema = JsonDocument.Parse("{}");

        public string Id => "disabled-plugin-v1";
        public string Name => "Disabled Plugin";
        public string Version => "1.0.0";
        public PluginCategory Category => PluginCategory.Transform;

        public PluginMetadata Metadata => PluginMetadata.Create(
            "disabled-plugin-v1", "Disabled Plugin", "1.0.0", PluginCategory.Transform);

        public JsonDocument InputSchema => EmptySchema;
        public JsonDocument OutputSchema => EmptySchema;
        public JsonDocument ConfigSchema => EmptySchema;

        public Task<PluginOutput> ExecuteAsync(PluginInput input, PluginConfig? config = null, CancellationToken cancellationToken = default)
            => Task.FromResult(PluginOutput.Successful(input.ExecutionId, input.Payload));

        public Task<HealthCheckResult> HealthCheckAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(HealthCheckResult.Healthy());

        public ValidationResult ValidateConfig(PluginConfig config) => ValidationResult.Success();
        public ValidationResult ValidateInput(PluginInput input) => ValidationResult.Success();
    }

    #endregion
}
