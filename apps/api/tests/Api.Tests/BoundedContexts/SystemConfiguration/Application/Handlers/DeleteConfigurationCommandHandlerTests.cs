using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Moq;
using Xunit;
using SystemConfig = Api.BoundedContexts.SystemConfiguration.Domain.Entities.SystemConfiguration;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SystemConfiguration")]
public class DeleteConfigurationCommandHandlerTests
{
    private readonly Mock<IConfigurationRepository> _configRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly DeleteConfigurationCommandHandler _handler;

    public DeleteConfigurationCommandHandlerTests()
    {
        _handler = new DeleteConfigurationCommandHandler(
            _configRepoMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_ConfigExists_DeletesAndReturnsTrue()
    {
        // Arrange
        var configId = Guid.NewGuid();
        var config = new SystemConfig(Guid.NewGuid(),
            new ConfigKey("test.key"), "value", "string",
            Guid.NewGuid(), "Test", "General");

        _configRepoMock.Setup(r => r.GetByIdAsync(configId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        var command = new DeleteConfigurationCommand(configId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result);
        _configRepoMock.Verify(r => r.DeleteAsync(config, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ConfigNotFound_ReturnsFalse()
    {
        // Arrange
        _configRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemConfig?)null);

        var command = new DeleteConfigurationCommand(Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result);
        _configRepoMock.Verify(r => r.DeleteAsync(It.IsAny<SystemConfig>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
