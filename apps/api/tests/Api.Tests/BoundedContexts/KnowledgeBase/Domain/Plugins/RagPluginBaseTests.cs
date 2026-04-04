// =============================================================================
// MeepleAI - RAG Plugin System Tests
// Issue #3414 - Plugin Contract & Interfaces
// =============================================================================

using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Base;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Plugins;

public class RagPluginBaseTests
{
    private readonly ILogger _logger;

    public RagPluginBaseTests()
    {
        _logger = NullLogger.Instance;
    }

    #region ExecuteAsync Tests

    [Fact]
    public async Task ExecuteAsync_WithValidInput_ReturnsSuccessfulOutput()
    {
        // Arrange
        var plugin = new TestPlugin(_logger);
        var input = CreateValidInput();

        // Act
        var result = await plugin.ExecuteAsync(input);

        // Assert
        result.Success.Should().BeTrue();
        result.ExecutionId.Should().Be(input.ExecutionId);
        result.Metrics.DurationMs.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidInput_ReturnsValidationError()
    {
        // Arrange
        var plugin = new TestPlugin(_logger);
        var input = new PluginInput
        {
            ExecutionId = Guid.Empty, // Invalid
            Payload = JsonDocument.Parse("{}")
        };

        // Act
        var result = await plugin.ExecuteAsync(input);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("VALIDATION_ERROR");
        result.ErrorMessage.Should().Contain("ExecutionId");
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidConfig_ReturnsConfigError()
    {
        // Arrange
        var plugin = new TestPlugin(_logger);
        var input = CreateValidInput();
        var config = new PluginConfig
        {
            TimeoutMs = -1, // Invalid
            MaxRetries = 3
        };

        // Act
        var result = await plugin.ExecuteAsync(input, config);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("CONFIG_ERROR");
        result.ErrorMessage.Should().Contain("Timeout");
    }

    [Fact]
    public async Task ExecuteAsync_WithTimeout_ReturnsTimeoutError()
    {
        // Arrange
        var plugin = new SlowPlugin(_logger);
        var input = CreateValidInput();
        var config = new PluginConfig { TimeoutMs = 100 }; // Very short timeout

        // Act
        var result = await plugin.ExecuteAsync(input, config);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("TIMEOUT");
    }

    [Fact]
    public async Task ExecuteAsync_WithCancellation_ThrowsOperationCancelled()
    {
        // Arrange
        var plugin = new SlowPlugin(_logger);
        var input = CreateValidInput();
        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = plugin.ExecuteAsync(input, null, cts.Token);
        await Task.Delay(50);
        await cts.CancelAsync();

        // Assert - TaskCanceledException derives from OperationCanceledException
        await ((Func<Task>)(() => executeTask)).Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ExecuteAsync_WithException_ReturnsExecutionError()
    {
        // Arrange
        var plugin = new FailingPlugin(_logger);
        var input = CreateValidInput();

        // Act
        var result = await plugin.ExecuteAsync(input);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("EXECUTION_ERROR");
        result.ErrorMessage.Should().Be("Test exception");
    }

    #endregion

    #region HealthCheckAsync Tests

    [Fact]
    public async Task HealthCheckAsync_Default_ReturnsHealthy()
    {
        // Arrange
        var plugin = new TestPlugin(_logger);

        // Act
        var result = await plugin.HealthCheckAsync();

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.CheckDurationMs.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task HealthCheckAsync_WithCustomCheck_ReturnsCustomStatus()
    {
        // Arrange
        var plugin = new DegradedPlugin(_logger);

        // Act
        var result = await plugin.HealthCheckAsync();

        // Assert
        result.Status.Should().Be(HealthStatus.Degraded);
        result.Message.Should().Be("Running slow");
    }

    [Fact]
    public async Task HealthCheckAsync_WithException_ReturnsUnhealthy()
    {
        // Arrange
        var plugin = new HealthCheckFailingPlugin(_logger);

        // Act
        var result = await plugin.HealthCheckAsync();

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Message.Should().Contain("Health check failed");
    }

    #endregion

    #region ValidateConfig Tests

    [Fact]
    public void ValidateConfig_WithValidConfig_ReturnsSuccess()
    {
        // Arrange
        var plugin = new TestPlugin(_logger);
        var config = PluginConfig.Default();

        // Act
        var result = plugin.ValidateConfig(config);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateConfig_WithNegativeTimeout_ReturnsError()
    {
        // Arrange
        var plugin = new TestPlugin(_logger);
        var config = new PluginConfig { TimeoutMs = -1 };

        // Act
        var result = plugin.ValidateConfig(config);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyPath == "TimeoutMs");
    }

    [Fact]
    public void ValidateConfig_WithNegativeRetries_ReturnsError()
    {
        // Arrange
        var plugin = new TestPlugin(_logger);
        var config = new PluginConfig { TimeoutMs = 1000, MaxRetries = -1 };

        // Act
        var result = plugin.ValidateConfig(config);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyPath == "MaxRetries");
    }

    [Fact]
    public void ValidateConfig_WithNegativeBackoff_ReturnsError()
    {
        // Arrange
        var plugin = new TestPlugin(_logger);
        var config = new PluginConfig { TimeoutMs = 1000, RetryBackoffMs = -1 };

        // Act
        var result = plugin.ValidateConfig(config);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyPath == "RetryBackoffMs");
    }

    [Fact]
    public void ValidateConfig_WithCustomValidation_IncludesCustomErrors()
    {
        // Arrange
        var plugin = new CustomValidationPlugin(_logger);
        var config = PluginConfig.Default();

        // Act
        var result = plugin.ValidateConfig(config);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == "CUSTOM_ERROR");
    }

    #endregion

    #region ValidateInput Tests

    [Fact]
    public void ValidateInput_WithValidInput_ReturnsSuccess()
    {
        // Arrange
        var plugin = new TestPlugin(_logger);
        var input = CreateValidInput();

        // Act
        var result = plugin.ValidateInput(input);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateInput_WithEmptyExecutionId_ReturnsError()
    {
        // Arrange
        var plugin = new TestPlugin(_logger);
        var input = new PluginInput
        {
            ExecutionId = Guid.Empty,
            Payload = JsonDocument.Parse("{}")
        };

        // Act
        var result = plugin.ValidateInput(input);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == "MISSING_EXECUTION_ID");
    }

    #endregion

    #region Schema Tests

    [Fact]
    public void InputSchema_ReturnsValidJsonDocument()
    {
        // Arrange
        var plugin = new TestPlugin(_logger);

        // Act
        var schema = plugin.InputSchema;

        // Assert
        schema.Should().NotBeNull();
        schema.RootElement.GetProperty("$schema").GetString()
            .Should().Contain("json-schema.org");
    }

    [Fact]
    public void OutputSchema_ReturnsValidJsonDocument()
    {
        // Arrange
        var plugin = new TestPlugin(_logger);

        // Act
        var schema = plugin.OutputSchema;

        // Assert
        schema.Should().NotBeNull();
        schema.RootElement.GetProperty("$schema").GetString()
            .Should().Contain("json-schema.org");
    }

    [Fact]
    public void ConfigSchema_ReturnsValidJsonDocument()
    {
        // Arrange
        var plugin = new TestPlugin(_logger);

        // Act
        var schema = plugin.ConfigSchema;

        // Assert
        schema.Should().NotBeNull();
        schema.RootElement.GetProperty("$schema").GetString()
            .Should().Contain("json-schema.org");
    }

    #endregion

    #region Metadata Tests

    [Fact]
    public void Metadata_ReturnsCorrectValues()
    {
        // Arrange
        var plugin = new TestPlugin(_logger);

        // Act
        var metadata = plugin.Metadata;

        // Assert
        metadata.Id.Should().Be("test-plugin-v1");
        metadata.Name.Should().Be("Test Plugin");
        metadata.Version.Should().Be("1.0.0");
        metadata.Category.Should().Be(PluginCategory.Transform);
    }

    #endregion

    #region Helper Methods

    private static PluginInput CreateValidInput()
    {
        return new PluginInput
        {
            ExecutionId = Guid.NewGuid(),
            Payload = JsonDocument.Parse("""{"test": "data"}""")
        };
    }

    #endregion

    #region Test Plugin Implementations

    private class TestPlugin : RagPluginBase
    {
        public override string Id => "test-plugin-v1";
        public override string Name => "Test Plugin";
        public override string Version => "1.0.0";
        public override PluginCategory Category => PluginCategory.Transform;

        public TestPlugin(ILogger logger) : base(logger) { }

        protected override Task<PluginOutput> ExecuteCoreAsync(
            PluginInput input,
            PluginConfig config,
            CancellationToken cancellationToken)
        {
            var result = JsonDocument.Parse("""{"processed": true}""");
            return Task.FromResult(PluginOutput.Successful(input.ExecutionId, result));
        }

        protected override JsonDocument CreateInputSchema() =>
            CreateBasicSchema("object", "Test input schema");

        protected override JsonDocument CreateOutputSchema() =>
            CreateBasicSchema("object", "Test output schema");

        protected override JsonDocument CreateConfigSchema() =>
            CreateBasicSchema("object", "Test config schema");
    }

    private class SlowPlugin : TestPlugin
    {
        public SlowPlugin(ILogger logger) : base(logger) { }

        protected override async Task<PluginOutput> ExecuteCoreAsync(
            PluginInput input,
            PluginConfig config,
            CancellationToken cancellationToken)
        {
            await Task.Delay(5000, cancellationToken);
            var result = JsonDocument.Parse("""{"processed": true}""");
            return PluginOutput.Successful(input.ExecutionId, result);
        }
    }

    private class FailingPlugin : TestPlugin
    {
        public FailingPlugin(ILogger logger) : base(logger) { }

        protected override Task<PluginOutput> ExecuteCoreAsync(
            PluginInput input,
            PluginConfig config,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Test exception");
        }
    }

    private class DegradedPlugin : TestPlugin
    {
        public DegradedPlugin(ILogger logger) : base(logger) { }

        protected override Task<HealthCheckResult> PerformHealthCheckAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(HealthCheckResult.Degraded("Running slow"));
        }
    }

    private class HealthCheckFailingPlugin : TestPlugin
    {
        public HealthCheckFailingPlugin(ILogger logger) : base(logger) { }

        protected override Task<HealthCheckResult> PerformHealthCheckAsync(CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Health check exception");
        }
    }

    private class CustomValidationPlugin : TestPlugin
    {
        public CustomValidationPlugin(ILogger logger) : base(logger) { }

        protected override ValidationResult ValidateConfigCore(PluginConfig config)
        {
            return ValidationResult.Failure(new ValidationError
            {
                Message = "Custom validation failed",
                Code = "CUSTOM_ERROR"
            });
        }
    }

    #endregion
}
