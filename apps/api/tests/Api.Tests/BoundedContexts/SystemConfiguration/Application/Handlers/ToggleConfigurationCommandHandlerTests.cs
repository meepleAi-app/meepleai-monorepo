using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Moq;
using Xunit;
using SystemConfig = Api.BoundedContexts.SystemConfiguration.Domain.Entities.SystemConfiguration;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SystemConfiguration")]
public class ToggleConfigurationCommandHandlerTests
{
    private readonly Mock<IConfigurationRepository> _configRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly ToggleConfigurationCommandHandler _handler;

    public ToggleConfigurationCommandHandlerTests()
    {
        _handler = new ToggleConfigurationCommandHandler(
            _configRepoMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_ActivateConfig_ReturnsActivatedDto()
    {
        // Arrange
        var configId = Guid.NewGuid();
        var config = new SystemConfig(Guid.NewGuid(),
            new ConfigKey("feature.enabled"), "true", "boolean",
            Guid.NewGuid(), "Feature toggle", "Features");

        _configRepoMock.Setup(r => r.GetByIdAsync(configId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        var command = new ToggleConfigurationCommand(configId, true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsActive);
        _configRepoMock.Verify(r => r.UpdateAsync(config, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ConfigNotFound_ThrowsDomainException()
    {
        // Arrange
        _configRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemConfig?)null);

        var command = new ToggleConfigurationCommand(Guid.NewGuid(), false);

        // Act & Assert
        await Assert.ThrowsAsync<DomainException>(
            () => _handler.Handle(command, CancellationToken.None));
    }
}
