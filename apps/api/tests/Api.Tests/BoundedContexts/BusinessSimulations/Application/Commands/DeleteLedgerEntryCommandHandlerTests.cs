using Api.BoundedContexts.BusinessSimulations.Application.Commands;
using Api.BoundedContexts.BusinessSimulations.Domain.Entities;
using Api.BoundedContexts.BusinessSimulations.Domain.Enums;
using Api.BoundedContexts.BusinessSimulations.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.BusinessSimulations.Application.Commands;

/// <summary>
/// Unit tests for DeleteLedgerEntryCommandHandler (Issue #3722)
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "BusinessSimulations")]
public sealed class DeleteLedgerEntryCommandHandlerTests
{
    private readonly Mock<ILedgerEntryRepository> _repositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<DeleteLedgerEntryCommandHandler>> _loggerMock;
    private readonly DeleteLedgerEntryCommandHandler _handler;

    public DeleteLedgerEntryCommandHandlerTests()
    {
        _repositoryMock = new Mock<ILedgerEntryRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<DeleteLedgerEntryCommandHandler>>();
        _handler = new DeleteLedgerEntryCommandHandler(
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_ManualEntry_ShouldDeleteAndSave()
    {
        // Arrange
        var entry = LedgerEntry.CreateManualEntry(
            date: DateTime.UtcNow.AddDays(-1),
            type: LedgerEntryType.Expense,
            category: LedgerCategory.Marketing,
            amount: 500m,
            createdByUserId: Guid.NewGuid(),
            description: "Marketing campaign");

        _repositoryMock
            .Setup(r => r.GetByIdAsync(entry.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var command = new DeleteLedgerEntryCommand(entry.Id);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _repositoryMock.Verify(r => r.DeleteAsync(entry, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AutoEntry_ShouldThrowDomainException()
    {
        // Arrange
        var entry = LedgerEntry.CreateAutoEntry(
            date: DateTime.UtcNow,
            type: LedgerEntryType.Expense,
            category: LedgerCategory.TokenUsage,
            amount: 0.05m,
            currency: "USD",
            description: "Auto token usage");

        _repositoryMock
            .Setup(r => r.GetByIdAsync(entry.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        var command = new DeleteLedgerEntryCommand(entry.Id);

        // Act & Assert
        await FluentActions.Invoking(() => _handler.Handle(command, CancellationToken.None))
            .Should().ThrowAsync<DomainException>()
            .WithMessage("*manual*");
    }

    [Fact]
    public async Task Handle_EntryNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        var id = Guid.NewGuid();
        _repositoryMock
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LedgerEntry?)null);

        var command = new DeleteLedgerEntryCommand(id);

        // Act & Assert
        await FluentActions.Invoking(() => _handler.Handle(command, CancellationToken.None))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_AutoEntry_ShouldNotCallDelete()
    {
        // Arrange
        var entry = LedgerEntry.CreateAutoEntry(
            date: DateTime.UtcNow,
            type: LedgerEntryType.Income,
            category: LedgerCategory.Subscription,
            amount: 10m);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(entry.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        var command = new DeleteLedgerEntryCommand(entry.Id);

        // Act & Assert
        try { await _handler.Handle(command, CancellationToken.None); } catch { /* expected */ }

        _repositoryMock.Verify(r => r.DeleteAsync(It.IsAny<LedgerEntry>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Constructor_WithNullRepository_ShouldThrow()
    {
        var act = () => new DeleteLedgerEntryCommandHandler(
            null!, _unitOfWorkMock.Object, _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("repository");
    }

    [Fact]
    public void Constructor_WithNullUnitOfWork_ShouldThrow()
    {
        var act = () => new DeleteLedgerEntryCommandHandler(
            _repositoryMock.Object, null!, _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("unitOfWork");
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrow()
    {
        var act = () => new DeleteLedgerEntryCommandHandler(
            _repositoryMock.Object, _unitOfWorkMock.Object, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }
}
