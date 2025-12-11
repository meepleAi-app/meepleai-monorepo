using Api.BoundedContexts.KnowledgeBase.Domain.Services.LlmManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services.LlmManagement;

/// <summary>
/// ISSUE-1725: Tests for LlmModelOverrideService budget mode functionality
/// </summary>
public class LlmModelOverrideServiceTests
{
    private readonly Mock<ILogger<LlmModelOverrideService>> _loggerMock;
    private readonly IConfiguration _configuration;
    private readonly LlmModelOverrideService _sut;

    public LlmModelOverrideServiceTests()
    {
        _loggerMock = new Mock<ILogger<LlmModelOverrideService>>();

        // Empty configuration → use default mappings
        _configuration = new ConfigurationBuilder().Build();

        _sut = new LlmModelOverrideService(_configuration, _loggerMock.Object);
    }

    [Fact]
    public void IsInBudgetMode_InitialState_ReturnsFalse()
    {
        // Act
        var result = _sut.IsInBudgetMode();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void EnableBudgetMode_ValidReason_ActivatesBudgetMode()
    {
        // Arrange
        var reason = "Daily budget 95% exceeded";

        // Act
        _sut.EnableBudgetMode(reason);

        // Assert
        Assert.True(_sut.IsInBudgetMode());
        var status = _sut.GetBudgetModeStatus();
        Assert.Contains("ACTIVE", status, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(reason, status, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DisableBudgetMode_WhenActive_DeactivatesBudgetMode()
    {
        // Arrange
        _sut.EnableBudgetMode("Test reason");
        Assert.True(_sut.IsInBudgetMode());

        // Act
        _sut.DisableBudgetMode();

        // Assert
        Assert.False(_sut.IsInBudgetMode());
        var status = _sut.GetBudgetModeStatus();
        Assert.Contains("INACTIVE", status, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetOverrideModel_NotInBudgetMode_ReturnsOriginal()
    {
        // Arrange
        var originalModel = "openai/gpt-4o";

        // Act
        var result = _sut.GetOverrideModel(originalModel);

        // Assert
        Assert.Equal(originalModel, result);
    }

    [Fact]
    public void GetOverrideModel_InBudgetMode_WithMapping_ReturnsCheaperModel()
    {
        // Arrange
        _sut.EnableBudgetMode("Test budget constraint");
        var expensiveModel = "openai/gpt-4o";

        // Act
        var result = _sut.GetOverrideModel(expensiveModel);

        // Assert
        Assert.NotEqual(expensiveModel, result);
        Assert.Equal("openai/gpt-4o-mini", result); // Default mapping
    }

    [Fact]
    public void GetOverrideModel_InBudgetMode_NoMapping_ReturnsOriginal()
    {
        // Arrange
        _sut.EnableBudgetMode("Test budget constraint");
        var unknownModel = "unknown/custom-model";

        // Act
        var result = _sut.GetOverrideModel(unknownModel);

        // Assert
        Assert.Equal(unknownModel, result); // No mapping → return original
    }

    [Theory]
    [InlineData("openai/gpt-4o", "openai/gpt-4o-mini")]
    [InlineData("openai/gpt-4", "openai/gpt-3.5-turbo")]
    [InlineData("anthropic/claude-3-opus", "anthropic/claude-3.5-sonnet")]
    [InlineData("anthropic/claude-3.5-sonnet", "anthropic/claude-3.5-haiku")]
    [InlineData("openai/gpt-4o-mini", "meta-llama/llama-3.3-70b-instruct:free")]
    public void GetOverrideModel_InBudgetMode_DefaultMappings_DowngradesCorrectly(
        string expensiveModel,
        string expectedCheaper)
    {
        // Arrange
        _sut.EnableBudgetMode("Budget test");

        // Act
        var result = _sut.GetOverrideModel(expensiveModel);

        // Assert
        Assert.Equal(expectedCheaper, result);
    }

    [Fact]
    public void GetBudgetModeStatus_WhenInactive_ReturnsInactiveMessage()
    {
        // Act
        var status = _sut.GetBudgetModeStatus();

        // Assert
        Assert.Contains("INACTIVE", status, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetBudgetModeStatus_WhenActive_ReturnsActiveWithDetails()
    {
        // Arrange
        var reason = "Critical budget threshold";
        _sut.EnableBudgetMode(reason);

        // Act
        var status = _sut.GetBudgetModeStatus();

        // Assert
        Assert.Contains("ACTIVE", status, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(reason, status, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("min", status, StringComparison.OrdinalIgnoreCase); // Duration in minutes
    }

    [Fact]
    public void EnableBudgetMode_CalledTwice_UpdatesReasonAndTimestamp()
    {
        // Arrange
        _sut.EnableBudgetMode("First reason");
        var firstStatus = _sut.GetBudgetModeStatus();

        // Act
        Thread.Sleep(100); // Small delay to ensure different timestamp
        _sut.EnableBudgetMode("Second reason");
        var secondStatus = _sut.GetBudgetModeStatus();

        // Assert
        Assert.NotEqual(firstStatus, secondStatus);
        Assert.Contains("Second reason", secondStatus, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_WithCustomConfiguration_LoadsCustomMappings()
    {
        // Arrange
        var customConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LlmBudgetAlerts:ModelDowngradeMappings:custom/model-a"] = "custom/model-b"
            })
            .Build();

        var service = new LlmModelOverrideService(customConfig, _loggerMock.Object);
        service.EnableBudgetMode("Test");

        // Act
        var result = service.GetOverrideModel("custom/model-a");

        // Assert
        Assert.Equal("custom/model-b", result);
    }
}
