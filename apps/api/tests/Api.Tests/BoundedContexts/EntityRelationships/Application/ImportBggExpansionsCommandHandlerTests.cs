using Api.BoundedContexts.EntityRelationships.Application.Commands;
using Api.BoundedContexts.EntityRelationships.Infrastructure.Services;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.EntityRelationships.Application;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "EntityRelationships")]
public class ImportBggExpansionsCommandHandlerTests
{
    private static readonly Guid _sharedGameId = Guid.NewGuid();
    private static readonly Guid _adminUserId = Guid.NewGuid();

    private readonly Mock<IBggExpansionImporter> _importerMock;
    private readonly ImportBggExpansionsCommandHandler _handler;

    public ImportBggExpansionsCommandHandlerTests()
    {
        _importerMock = new Mock<IBggExpansionImporter>();
        _handler = new ImportBggExpansionsCommandHandler(
            _importerMock.Object,
            new Mock<ILogger<ImportBggExpansionsCommandHandler>>().Object);
    }

    [Fact]
    public async Task Handle_ValidCommand_DelegatesToImporter()
    {
        // Arrange
        _importerMock
            .Setup(i => i.ImportExpansionsAsync(_sharedGameId, _adminUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        var command = new ImportBggExpansionsCommand(_sharedGameId, _adminUserId);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(5, result);
        _importerMock.Verify(
            i => i.ImportExpansionsAsync(_sharedGameId, _adminUserId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_NoLinksCreated_ReturnsZero()
    {
        // Arrange
        _importerMock
            .Setup(i => i.ImportExpansionsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var command = new ImportBggExpansionsCommand(_sharedGameId, _adminUserId);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task Handle_PassesCorrectGameIdAndAdminUserId()
    {
        // Arrange
        var expectedGameId = Guid.NewGuid();
        var expectedAdminId = Guid.NewGuid();

        _importerMock
            .Setup(i => i.ImportExpansionsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var command = new ImportBggExpansionsCommand(expectedGameId, expectedAdminId);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _importerMock.Verify(
            i => i.ImportExpansionsAsync(expectedGameId, expectedAdminId, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
