// =============================================================================
// MeepleAI - RAG Plugin System Tests
// Issue #3414 - Plugin Contract & Interfaces
// =============================================================================

using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Plugins.Models;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Plugins;

public class PluginModelsTests
{
    #region PluginInput Tests

    [Fact]
    public void PluginInput_Create_WithPayload_SetsDefaults()
    {
        // Arrange
        var payload = JsonDocument.Parse("""{"query": "test"}""");

        // Act
        var input = PluginInput.Create(payload);

        // Assert
        input.ExecutionId.Should().NotBeEmpty();
        input.Payload.Should().NotBeNull();
        input.PipelineContext.Should().BeEmpty();
        input.Metadata.Should().BeEmpty();
        input.UserId.Should().BeNull();
        input.GameId.Should().BeNull();
    }

    [Fact]
    public void PluginInput_Create_WithContext_SetsAllProperties()
    {
        // Arrange
        var payload = JsonDocument.Parse("""{"query": "test"}""");
        var context = new Dictionary<string, JsonDocument>
        {
            ["node1"] = JsonDocument.Parse("""{"result": "value"}""")
        };
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        // Act
        var input = PluginInput.Create(payload, context, userId, gameId);

        // Assert
        input.ExecutionId.Should().NotBeEmpty();
        input.Payload.Should().NotBeNull();
        input.PipelineContext.Should().ContainKey("node1");
        input.UserId.Should().Be(userId);
        input.GameId.Should().Be(gameId);
    }

    #endregion

    #region PluginOutput Tests

    [Fact]
    public void PluginOutput_Successful_CreatesValidOutput()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var result = JsonDocument.Parse("""{"answer": "response"}""");
        var confidence = 0.95;

        // Act
        var output = PluginOutput.Successful(executionId, result, confidence);

        // Assert
        output.ExecutionId.Should().Be(executionId);
        output.Success.Should().BeTrue();
        output.Result.Should().NotBeNull();
        output.Confidence.Should().Be(confidence);
        output.ErrorMessage.Should().BeNull();
        output.ErrorCode.Should().BeNull();
    }

    [Fact]
    public void PluginOutput_Failed_CreatesErrorOutput()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var errorMessage = "Something went wrong";
        var errorCode = "ERR001";

        // Act
        var output = PluginOutput.Failed(executionId, errorMessage, errorCode);

        // Assert
        output.ExecutionId.Should().Be(executionId);
        output.Success.Should().BeFalse();
        output.Result.Should().BeNull();
        output.ErrorMessage.Should().Be(errorMessage);
        output.ErrorCode.Should().Be(errorCode);
    }

    #endregion

    #region PluginConfig Tests

    [Fact]
    public void PluginConfig_Default_HasExpectedValues()
    {
        // Act
        var config = PluginConfig.Default();

        // Assert
        config.Enabled.Should().BeTrue();
        config.TimeoutMs.Should().Be(30000);
        config.MaxRetries.Should().Be(3);
        config.RetryBackoffMs.Should().Be(1000);
        config.ExponentialBackoff.Should().BeTrue();
        config.Priority.Should().Be(100);
    }

    [Fact]
    public void PluginConfig_Create_WithCustomValues_SetsProperties()
    {
        // Act
        var config = PluginConfig.Create(
            enabled: false,
            timeoutMs: 5000,
            maxRetries: 5);

        // Assert
        config.Enabled.Should().BeFalse();
        config.TimeoutMs.Should().Be(5000);
        config.MaxRetries.Should().Be(5);
    }

    #endregion

    #region PluginMetadata Tests

    [Fact]
    public void PluginMetadata_Create_SetsRequiredProperties()
    {
        // Act
        var metadata = PluginMetadata.Create(
            "routing-intent-v1",
            "Intent Router",
            "1.0.0",
            PluginCategory.Routing,
            "Routes queries based on intent");

        // Assert
        metadata.Id.Should().Be("routing-intent-v1");
        metadata.Name.Should().Be("Intent Router");
        metadata.Version.Should().Be("1.0.0");
        metadata.Category.Should().Be(PluginCategory.Routing);
        metadata.Description.Should().Be("Routes queries based on intent");
        metadata.IsEnabled.Should().BeTrue();
        metadata.IsBuiltIn.Should().BeTrue();
    }

    #endregion

    #region HealthCheckResult Tests

    [Fact]
    public void HealthCheckResult_Healthy_CreatesHealthyStatus()
    {
        // Act
        var result = HealthCheckResult.Healthy("All systems operational");

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Message.Should().Be("All systems operational");
    }

    [Fact]
    public void HealthCheckResult_Degraded_CreatesDegradedStatus()
    {
        // Act
        var result = HealthCheckResult.Degraded("Slow response times");

        // Assert
        result.Status.Should().Be(HealthStatus.Degraded);
        result.Message.Should().Be("Slow response times");
    }

    [Fact]
    public void HealthCheckResult_Unhealthy_CreatesUnhealthyStatus()
    {
        // Act
        var result = HealthCheckResult.Unhealthy("Database connection failed");

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Message.Should().Be("Database connection failed");
    }

    [Fact]
    public void HealthCheckResult_Unknown_CreatesUnknownStatus()
    {
        // Act
        var result = HealthCheckResult.Unknown();

        // Assert
        result.Status.Should().Be(HealthStatus.Unknown);
        result.Message.Should().Contain("could not be determined");
    }

    #endregion

    #region ValidationResult Tests

    [Fact]
    public void ValidationResult_Success_IsValid()
    {
        // Act
        var result = ValidationResult.Success();

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void ValidationResult_SuccessWithWarnings_IsValidWithWarnings()
    {
        // Arrange
        var warning = new ValidationWarning { Message = "Consider using a shorter timeout" };

        // Act
        var result = ValidationResult.SuccessWithWarnings(warning);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().ContainSingle();
        result.Warnings[0].Message.Should().Be("Consider using a shorter timeout");
    }

    [Fact]
    public void ValidationResult_Failure_WithErrors_IsInvalid()
    {
        // Arrange
        var error = new ValidationError
        {
            Message = "Invalid value",
            PropertyPath = "timeout",
            Code = "INVALID_TIMEOUT",
            AttemptedValue = -1
        };

        // Act
        var result = ValidationResult.Failure(error);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle();
        result.Errors[0].Message.Should().Be("Invalid value");
        result.Errors[0].PropertyPath.Should().Be("timeout");
        result.Errors[0].Code.Should().Be("INVALID_TIMEOUT");
        result.Errors[0].AttemptedValue.Should().Be(-1);
    }

    [Fact]
    public void ValidationResult_Failure_WithMessages_CreatesErrors()
    {
        // Act
        var result = ValidationResult.Failure("Error 1", "Error 2");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(2);
        result.Errors[0].Message.Should().Be("Error 1");
        result.Errors[1].Message.Should().Be("Error 2");
    }

    [Fact]
    public void ValidationResult_Combine_MergesResults()
    {
        // Arrange
        var result1 = ValidationResult.Failure("Error 1");
        var result2 = ValidationResult.SuccessWithWarnings(new ValidationWarning { Message = "Warning 1" });
        var result3 = ValidationResult.Failure("Error 2");

        // Act
        var combined = ValidationResult.Combine(result1, result2, result3);

        // Assert
        combined.IsValid.Should().BeFalse();
        combined.Errors.Should().HaveCount(2);
        combined.Warnings.Should().ContainSingle();
    }

    [Fact]
    public void ValidationResult_Combine_AllSuccess_IsValid()
    {
        // Arrange
        var result1 = ValidationResult.Success();
        var result2 = ValidationResult.Success();

        // Act
        var combined = ValidationResult.Combine(result1, result2);

        // Assert
        combined.IsValid.Should().BeTrue();
        combined.Errors.Should().BeEmpty();
    }

    #endregion

    #region PluginCategory Tests

    [Theory]
    [InlineData(PluginCategory.Routing, 0)]
    [InlineData(PluginCategory.Cache, 1)]
    [InlineData(PluginCategory.Retrieval, 2)]
    [InlineData(PluginCategory.Evaluation, 3)]
    [InlineData(PluginCategory.Generation, 4)]
    [InlineData(PluginCategory.Validation, 5)]
    [InlineData(PluginCategory.Transform, 6)]
    [InlineData(PluginCategory.Filter, 7)]
    public void PluginCategory_HasExpectedValues(PluginCategory category, int expectedValue)
    {
        // Assert
        ((int)category).Should().Be(expectedValue);
    }

    #endregion

    #region HealthStatus Tests

    [Theory]
    [InlineData(HealthStatus.Healthy, 0)]
    [InlineData(HealthStatus.Degraded, 1)]
    [InlineData(HealthStatus.Unhealthy, 2)]
    [InlineData(HealthStatus.Unknown, 3)]
    public void HealthStatus_HasExpectedValues(HealthStatus status, int expectedValue)
    {
        // Assert
        ((int)status).Should().Be(expectedValue);
    }

    #endregion
}
