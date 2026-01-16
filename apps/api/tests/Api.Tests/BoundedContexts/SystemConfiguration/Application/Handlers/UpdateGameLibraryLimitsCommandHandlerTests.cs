using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Application.Handlers;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.Tests.Constants;
using MediatR;
using Moq;
using Xunit;
using SystemConfigurationEntity = Api.BoundedContexts.SystemConfiguration.Domain.Entities.SystemConfiguration;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Application.Handlers;

/// <summary>
/// Tests for UpdateGameLibraryLimitsCommandHandler.
/// Issue #2444: Admin UI - Configure Game Library Tier Limits
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class UpdateGameLibraryLimitsCommandHandlerTests
{
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<IConfigurationRepository> _mockConfigRepository;
    private readonly UpdateGameLibraryLimitsCommandHandler _handler;

    public UpdateGameLibraryLimitsCommandHandlerTests()
    {
        _mockMediator = new Mock<IMediator>();
        _mockConfigRepository = new Mock<IConfigurationRepository>();
        _handler = new UpdateGameLibraryLimitsCommandHandler(
            _mockMediator.Object,
            _mockConfigRepository.Object);
    }

    [Fact]
    public async Task Handle_WithValidLimits_ReturnsUpdatedDto()
    {
        // Arrange
        var adminUserId = Guid.NewGuid();
        var command = new UpdateGameLibraryLimitsCommand(
            FreeTierLimit: 10,
            NormalTierLimit: 30,
            PremiumTierLimit: 100,
            UpdatedByUserId: adminUserId
        );

        // Mock configuration repository to return null (will trigger creation)
        _mockConfigRepository
            .Setup(r => r.GetByKeyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemConfigurationEntity?)null);

        // Mock mediator to return success for Create commands
        _mockMediator
            .Setup(m => m.Send(It.IsAny<CreateConfigurationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConfigurationDto(
                Guid.NewGuid(), "key", "value", "int", null, "GameLibrary",
                true, false, "All", 1, DateTime.UtcNow, DateTime.UtcNow
            ));

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(10, result.FreeTierLimit);
        Assert.Equal(30, result.NormalTierLimit);
        Assert.Equal(100, result.PremiumTierLimit);
        Assert.Equal(adminUserId.ToString(), result.LastUpdatedByUserId);

        // Verify mediator was called 3 times (one for each tier)
        _mockMediator.Verify(
            m => m.Send(It.IsAny<CreateConfigurationCommand>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }
}
