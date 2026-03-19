using Api.BoundedContexts.BusinessSimulations.Application.Commands;
using Api.BoundedContexts.BusinessSimulations.Application.Commands;
using Api.BoundedContexts.BusinessSimulations.Application.Queries;
using Api.BoundedContexts.BusinessSimulations.Domain.Entities;
using Api.BoundedContexts.BusinessSimulations.Domain.Enums;
using Api.BoundedContexts.BusinessSimulations.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.BusinessSimulations.Application.Handlers;

/// <summary>
/// Unit tests for CreateManualLedgerEntryCommandHandler (Issue #3722)
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "BusinessSimulations")]
public sealed class CreateManualLedgerEntryCommandHandlerTests
{
    private readonly Mock<ILedgerEntryRepository> _repositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<CreateManualLedgerEntryCommandHandler>> _loggerMock;
    private readonly CreateManualLedgerEntryCommandHandler _handler;

    public CreateManualLedgerEntryCommandHandlerTests()
    {
        _repositoryMock = new Mock<ILedgerEntryRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<CreateManualLedgerEntryCommandHandler>>();
        _handler = new CreateManualLedgerEntryCommandHandler(
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldCreateEntryAndReturnId()
    {
        // Arrange
        var userId = Guid.NewGuid();
        LedgerEntry? capturedEntry = null;
        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<LedgerEntry>(), It.IsAny<CancellationToken>()))
            .Callback<LedgerEntry, CancellationToken>((entry, _) => capturedEntry = entry)
            .Returns(Task.CompletedTask);
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var command = new CreateManualLedgerEntryCommand(
            DateTime.UtcNow.AddDays(-1),
            LedgerEntryType.Income,
            LedgerCategory.Subscription,
            99.99m,
            "EUR",
            "Monthly subscription",
            userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeEmpty();
        capturedEntry.Should().NotBeNull();
        capturedEntry!.Type.Should().Be(LedgerEntryType.Income);
        capturedEntry.Category.Should().Be(LedgerCategory.Subscription);
        capturedEntry.Amount.Amount.Should().Be(99.99m);
        capturedEntry.Amount.Currency.Should().Be("EUR");
        capturedEntry.Source.Should().Be(LedgerEntrySource.Manual);
        capturedEntry.Description.Should().Be("Monthly subscription");
        capturedEntry.CreatedByUserId.Should().Be(userId);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithExpenseType_ShouldCreateExpenseEntry()
    {
        // Arrange
        var userId = Guid.NewGuid();
        LedgerEntry? capturedEntry = null;
        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<LedgerEntry>(), It.IsAny<CancellationToken>()))
            .Callback<LedgerEntry, CancellationToken>((entry, _) => capturedEntry = entry)
            .Returns(Task.CompletedTask);
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var command = new CreateManualLedgerEntryCommand(
            DateTime.UtcNow,
            LedgerEntryType.Expense,
            LedgerCategory.Infrastructure,
            250.00m,
            "USD",
            "Server costs",
            userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeEmpty();
        capturedEntry!.Type.Should().Be(LedgerEntryType.Expense);
        capturedEntry.Category.Should().Be(LedgerCategory.Infrastructure);
        capturedEntry.Amount.Currency.Should().Be("USD");
    }

    [Fact]
    public async Task Handle_WithNullDescription_ShouldCreateEntry()
    {
        // Arrange
        var userId = Guid.NewGuid();
        LedgerEntry? capturedEntry = null;
        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<LedgerEntry>(), It.IsAny<CancellationToken>()))
            .Callback<LedgerEntry, CancellationToken>((entry, _) => capturedEntry = entry)
            .Returns(Task.CompletedTask);
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var command = new CreateManualLedgerEntryCommand(
            DateTime.UtcNow,
            LedgerEntryType.Income,
            LedgerCategory.TokenPurchase,
            50.00m,
            "EUR",
            null,
            userId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeEmpty();
        capturedEntry!.Description.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldCallRepositoryAddAndUnitOfWorkSave()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<LedgerEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var command = new CreateManualLedgerEntryCommand(
            DateTime.UtcNow,
            LedgerEntryType.Income,
            LedgerCategory.Other,
            10.00m,
            "EUR",
            "Test",
            userId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _repositoryMock.Verify(r => r.AddAsync(It.IsAny<LedgerEntry>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Constructor_WithNullRepository_ShouldThrow()
    {
        var act = () => new CreateManualLedgerEntryCommandHandler(
            null!, _unitOfWorkMock.Object, _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("repository");
    }

    [Fact]
    public void Constructor_WithNullUnitOfWork_ShouldThrow()
    {
        var act = () => new CreateManualLedgerEntryCommandHandler(
            _repositoryMock.Object, null!, _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("unitOfWork");
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrow()
    {
        var act = () => new CreateManualLedgerEntryCommandHandler(
            _repositoryMock.Object, _unitOfWorkMock.Object, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public async Task Handle_WithAllCategories_ShouldAcceptEach()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<LedgerEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        foreach (var category in Enum.GetValues<LedgerCategory>())
        {
            var command = new CreateManualLedgerEntryCommand(
                DateTime.UtcNow, LedgerEntryType.Income, category, 10m, "EUR", null, userId);

            // Act & Assert - should not throw
            var result = await _handler.Handle(command, CancellationToken.None);
            result.Should().NotBeEmpty();
        }
    }
}
