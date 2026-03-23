using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Domain.Entities;

/// <summary>
/// Tests for the FeatureFlag aggregate root.
/// Issue #3025: Backend 90% Coverage Target - Phase 3
/// </summary>
[Trait("Category", "Unit")]
public sealed class FeatureFlagTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidData_CreatesInstance()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "enable_dark_mode";

        // Act
        var featureFlag = new FeatureFlag(id, name);

        // Assert
        featureFlag.Id.Should().Be(id);
        featureFlag.Name.Should().Be(name);
        featureFlag.IsEnabled.Should().BeFalse();
        featureFlag.Description.Should().BeNull();
        featureFlag.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        featureFlag.UpdatedAt.Should().Be(featureFlag.CreatedAt);
    }

    [Fact]
    public void Constructor_WithEnabledFlag_SetsIsEnabled()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "feature_enabled";

        // Act
        var featureFlag = new FeatureFlag(id, name, isEnabled: true);

        // Assert
        featureFlag.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithDescription_SetsDescription()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "experimental_feature";
        var description = "Enables experimental AI features";

        // Act
        var featureFlag = new FeatureFlag(id, name, description: description);

        // Assert
        featureFlag.Description.Should().Be(description);
    }

    [Fact]
    public void Constructor_WithAllParameters_SetsAllProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "full_feature";
        var description = "Full feature description";

        // Act
        var featureFlag = new FeatureFlag(id, name, isEnabled: true, description: description);

        // Assert
        featureFlag.Id.Should().Be(id);
        featureFlag.Name.Should().Be(name);
        featureFlag.IsEnabled.Should().BeTrue();
        featureFlag.Description.Should().Be(description);
    }

    [Fact]
    public void Constructor_TrimsWhitespaceFromName()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "  feature_name  ";

        // Act
        var featureFlag = new FeatureFlag(id, name);

        // Assert
        featureFlag.Name.Should().Be("feature_name");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_WithEmptyOrNullName_ThrowsArgumentException(string? name)
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var action = () => new FeatureFlag(id, name!);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("Feature flag name cannot be empty*");
    }

    #endregion

    #region Enable Tests

    [Fact]
    public async Task Enable_WhenDisabled_SetsIsEnabledToTrue()
    {
        // Arrange
        var featureFlag = CreateTestFeatureFlag(isEnabled: false);
        var originalUpdatedAt = featureFlag.UpdatedAt;

        // Small delay to ensure different timestamp
        await Task.Delay(50);

        // Act
        featureFlag.Enable();

        // Assert
        featureFlag.IsEnabled.Should().BeTrue();
        featureFlag.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    [Fact]
    public void Enable_WhenAlreadyEnabled_IsIdempotent()
    {
        // Arrange
        var featureFlag = CreateTestFeatureFlag(isEnabled: true);
        var originalUpdatedAt = featureFlag.UpdatedAt;

        // Act
        featureFlag.Enable();

        // Assert
        featureFlag.IsEnabled.Should().BeTrue();
        featureFlag.UpdatedAt.Should().Be(originalUpdatedAt);
    }

    #endregion

    #region Disable Tests

    [Fact]
    public async Task Disable_WhenEnabled_SetsIsEnabledToFalse()
    {
        // Arrange
        var featureFlag = CreateTestFeatureFlag(isEnabled: true);
        var originalUpdatedAt = featureFlag.UpdatedAt;

        // Small delay to ensure different timestamp
        await Task.Delay(50);

        // Act
        featureFlag.Disable();

        // Assert
        featureFlag.IsEnabled.Should().BeFalse();
        featureFlag.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    [Fact]
    public void Disable_WhenAlreadyDisabled_IsIdempotent()
    {
        // Arrange
        var featureFlag = CreateTestFeatureFlag(isEnabled: false);
        var originalUpdatedAt = featureFlag.UpdatedAt;

        // Act
        featureFlag.Disable();

        // Assert
        featureFlag.IsEnabled.Should().BeFalse();
        featureFlag.UpdatedAt.Should().Be(originalUpdatedAt);
    }

    #endregion

    #region Toggle Tests

    [Fact]
    public async Task Toggle_WhenDisabled_EnablesFlag()
    {
        // Arrange
        var featureFlag = CreateTestFeatureFlag(isEnabled: false);
        var originalUpdatedAt = featureFlag.UpdatedAt;

        // Small delay to ensure different timestamp
        await Task.Delay(50);

        // Act
        featureFlag.Toggle();

        // Assert
        featureFlag.IsEnabled.Should().BeTrue();
        featureFlag.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    [Fact]
    public async Task Toggle_WhenEnabled_DisablesFlag()
    {
        // Arrange
        var featureFlag = CreateTestFeatureFlag(isEnabled: true);
        var originalUpdatedAt = featureFlag.UpdatedAt;

        // Small delay to ensure different timestamp
        await Task.Delay(50);

        // Act
        featureFlag.Toggle();

        // Assert
        featureFlag.IsEnabled.Should().BeFalse();
        featureFlag.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    [Fact]
    public void Toggle_CalledTwice_ReturnsToOriginalState()
    {
        // Arrange
        var featureFlag = CreateTestFeatureFlag(isEnabled: false);

        // Act
        featureFlag.Toggle();
        featureFlag.Toggle();

        // Assert
        featureFlag.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Toggle_AlwaysUpdatesTimestamp()
    {
        // Arrange
        var featureFlag = CreateTestFeatureFlag(isEnabled: false);
        var firstUpdatedAt = featureFlag.UpdatedAt;

        // Act - Toggle twice with delay
        await Task.Delay(50);
        featureFlag.Toggle();
        var secondUpdatedAt = featureFlag.UpdatedAt;

        await Task.Delay(50);
        featureFlag.Toggle();
        var thirdUpdatedAt = featureFlag.UpdatedAt;

        // Assert
        secondUpdatedAt.Should().BeAfter(firstUpdatedAt);
        thirdUpdatedAt.Should().BeAfter(secondUpdatedAt);
    }

    #endregion

    #region Helper Methods

    private static FeatureFlag CreateTestFeatureFlag(bool isEnabled = false, string? description = null)
    {
        return new FeatureFlag(
            Guid.NewGuid(),
            "test_feature",
            isEnabled,
            description);
    }

    #endregion
}
