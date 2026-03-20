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
public class RollbackConfigCommandHandlerTests
{
    private readonly Mock<IConfigurationRepository> _configRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly RollbackConfigCommandHandler _handler;

    public RollbackConfigCommandHandlerTests()
    {
        _handler = new RollbackConfigCommandHandler(
            _configRepoMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_ConfigNotFound_ReturnsNull()
    {
        // Arrange
        _configRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SystemConfig?)null);

        var command = new RollbackConfigCommand(Guid.NewGuid(), 1, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_ConfigExists_RollsBackAndReturnsDto()
    {
        // Arrange
        var configId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var config = new SystemConfig(Guid.NewGuid(), 
            new ConfigKey("app.timeout"), "30", "integer",
            userId, "Timeout setting", "Performance");

        // Update to create a previous value
        config.UpdateValue("60", userId);

        _configRepoMock.Setup(r => r.GetByIdAsync(configId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        var command = new RollbackConfigCommand(configId, 1, userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        _configRepoMock.Verify(r => r.UpdateAsync(config, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
