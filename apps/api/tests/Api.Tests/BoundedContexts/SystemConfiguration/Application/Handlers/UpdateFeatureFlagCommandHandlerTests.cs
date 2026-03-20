using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.Services;
using Api.Tests.Constants;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SystemConfiguration")]
public class UpdateFeatureFlagCommandHandlerTests
{
    private readonly Mock<IFeatureFlagService> _featureFlagServiceMock = new();
    private readonly UpdateFeatureFlagCommandHandler _handler;

    public UpdateFeatureFlagCommandHandlerTests()
    {
        _handler = new UpdateFeatureFlagCommandHandler(_featureFlagServiceMock.Object);
    }

    [Fact]
    public async Task Handle_EnableGlobal_CallsEnableFeatureAsync()
    {
        // Arrange
        var command = new UpdateFeatureFlagCommand("Feature.Test", true, UserId: "admin");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _featureFlagServiceMock.Verify(
            s => s.EnableFeatureAsync("Feature.Test", null, "admin"), Times.Once);
    }

    [Fact]
    public async Task Handle_DisableGlobal_CallsDisableFeatureAsync()
    {
        // Arrange
        var command = new UpdateFeatureFlagCommand("Feature.Test", false, UserId: "admin");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _featureFlagServiceMock.Verify(
            s => s.DisableFeatureAsync("Feature.Test", null, "admin"), Times.Once);
    }

    [Fact]
    public async Task Handle_EnableForTier_CallsEnableFeatureForTierAsync()
    {
        // Arrange
        var tier = Api.SharedKernel.Domain.ValueObjects.UserTier.Parse("premium");
        var command = new UpdateFeatureFlagCommand("Feature.Premium", true, Tier: tier, UserId: "admin");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _featureFlagServiceMock.Verify(
            s => s.EnableFeatureForTierAsync("Feature.Premium", tier, "admin"), Times.Once);
    }

    [Fact]
    public async Task Handle_DisableForTier_CallsDisableFeatureForTierAsync()
    {
        // Arrange
        var tier = Api.SharedKernel.Domain.ValueObjects.UserTier.Parse("free");
        var command = new UpdateFeatureFlagCommand("Feature.Premium", false, Tier: tier, UserId: "admin");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _featureFlagServiceMock.Verify(
            s => s.DisableFeatureForTierAsync("Feature.Premium", tier, "admin"), Times.Once);
    }
}
