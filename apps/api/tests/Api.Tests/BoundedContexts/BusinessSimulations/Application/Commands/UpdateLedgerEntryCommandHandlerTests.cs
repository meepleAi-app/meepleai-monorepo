using Api.BoundedContexts.BusinessSimulations.Application.Commands;
using Api.BoundedContexts.BusinessSimulations.Application.Handlers;
using Api.BoundedContexts.BusinessSimulations.Domain.Entities;
using Api.BoundedContexts.BusinessSimulations.Domain.Enums;
using Api.BoundedContexts.BusinessSimulations.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.BusinessSimulations.Application.Commands;

/// <summary>
/// Unit tests for UpdateLedgerEntryCommandHandler (Issue #3722)
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "BusinessSimulations")]
public sealed class UpdateLedgerEntryCommandHandlerTests
{
    private readonly Mock<ILedgerEntryRepository> _repositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<UpdateLedgerEntryCommandHandler>> _loggerMock;
    private readonly UpdateLedgerEntryCommandHandler _handler;

    public UpdateLedgerEntryCommandHandlerTests()
    {
        _repositoryMock = new Mock<ILedgerEntryRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<UpdateLedgerEntryCommandHandler>>();
        _handler = new UpdateLedgerEntryCommandHandler(
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    private static LedgerEntry CreateTestEntry(Guid? id = null)
    {
        return LedgerEntry.CreateManualEntry(
            date: DateTime.UtcNow.AddDays(-1),
            type: LedgerEntryType.Income,
            category: LedgerCategory.Subscription,
            amount: 100m,
            createdByUserId: Guid.NewGuid(),
            currency: "EUR",
            description: "Original description");
    }

    [Fact]
    public async Task Handle_UpdateDescription_ShouldUpdateAndSave()
    {
        // Arrange
        var entry = CreateTestEntry();
        _repositoryMock
            .Setup(r => r.GetByIdAsync(entry.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var command = new UpdateLedgerEntryCommand(entry.Id, "Updated description", null, null);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        entry.Description.Should().Be("Updated description");
        entry.UpdatedAt.Should().NotBeNull();
        _repositoryMock.Verify(r => r.UpdateAsync(entry, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_UpdateCategory_ShouldUpdateAndSave()
    {
        // Arrange
        var entry = CreateTestEntry();
        _repositoryMock
            .Setup(r => r.GetByIdAsync(entry.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var command = new UpdateLedgerEntryCommand(entry.Id, null, LedgerCategory.Marketing, null);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        entry.Category.Should().Be(LedgerCategory.Marketing);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_UpdateMetadata_ShouldUpdateAndSave()
    {
        // Arrange
        var entry = CreateTestEntry();
        _repositoryMock
            .Setup(r => r.GetByIdAsync(entry.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var metadata = """{"invoice":"INV-001"}""";
        var command = new UpdateLedgerEntryCommand(entry.Id, null, null, metadata);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        entry.Metadata.Should().Be(metadata);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_UpdateAll_ShouldUpdateAllFields()
    {
        // Arrange
        var entry = CreateTestEntry();
        _repositoryMock
            .Setup(r => r.GetByIdAsync(entry.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var command = new UpdateLedgerEntryCommand(
            entry.Id, "New desc", LedgerCategory.Operational, """{"note":"test"}""");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        entry.Description.Should().Be("New desc");
        entry.Category.Should().Be(LedgerCategory.Operational);
        entry.Metadata.Should().Be("""{"note":"test"}""");
    }

    [Fact]
    public async Task Handle_EntryNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        var id = Guid.NewGuid();
        _repositoryMock
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LedgerEntry?)null);

        var command = new UpdateLedgerEntryCommand(id, "desc", null, null);

        // Act & Assert
        await FluentActions.Invoking(() => _handler.Handle(command, CancellationToken.None))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_NoFieldsToUpdate_ShouldStillCallSave()
    {
        // Arrange
        var entry = CreateTestEntry();
        _repositoryMock
            .Setup(r => r.GetByIdAsync(entry.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var command = new UpdateLedgerEntryCommand(entry.Id, null, null, null);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert - still calls save (no-op update is safe)
        _repositoryMock.Verify(r => r.UpdateAsync(entry, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Constructor_WithNullRepository_ShouldThrow()
    {
        var act = () => new UpdateLedgerEntryCommandHandler(
            null!, _unitOfWorkMock.Object, _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("repository");
    }

    [Fact]
    public void Constructor_WithNullUnitOfWork_ShouldThrow()
    {
        var act = () => new UpdateLedgerEntryCommandHandler(
            _repositoryMock.Object, null!, _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("unitOfWork");
    }
}
