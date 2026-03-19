using Api.BoundedContexts.BusinessSimulations.Application.Queries;
using Api.BoundedContexts.BusinessSimulations.Domain.Entities;
using Api.BoundedContexts.BusinessSimulations.Domain.Enums;
using Api.BoundedContexts.BusinessSimulations.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.BusinessSimulations.Application.Queries;

/// <summary>
/// Unit tests for GetLedgerEntryByIdQueryHandler (Issue #3722)
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "BusinessSimulations")]
public sealed class GetLedgerEntryByIdQueryHandlerTests
{
    private readonly Mock<ILedgerEntryRepository> _repositoryMock;
    private readonly GetLedgerEntryByIdQueryHandler _handler;

    public GetLedgerEntryByIdQueryHandlerTests()
    {
        _repositoryMock = new Mock<ILedgerEntryRepository>();
        _handler = new GetLedgerEntryByIdQueryHandler(_repositoryMock.Object);
    }

    [Fact]
    public async Task Handle_ExistingEntry_ShouldReturnDto()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var entry = LedgerEntry.CreateManualEntry(
            DateTime.UtcNow.AddDays(-3),
            LedgerEntryType.Expense,
            LedgerCategory.Infrastructure,
            250m,
            userId,
            "USD",
            "Server costs Q1");

        _repositoryMock
            .Setup(r => r.GetByIdAsync(entry.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        var query = new GetLedgerEntryByIdQuery(entry.Id);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Id.Should().Be(entry.Id);
        result.Type.Should().Be(LedgerEntryType.Expense);
        result.Category.Should().Be(LedgerCategory.Infrastructure);
        result.Amount.Should().Be(250m);
        result.Currency.Should().Be("USD");
        result.Source.Should().Be(LedgerEntrySource.Manual);
        result.Description.Should().Be("Server costs Q1");
        result.CreatedByUserId.Should().Be(userId);
    }

    [Fact]
    public async Task Handle_AutoEntry_ShouldReturnDto()
    {
        // Arrange
        var entry = LedgerEntry.CreateAutoEntry(
            DateTime.UtcNow,
            LedgerEntryType.Expense,
            LedgerCategory.TokenUsage,
            0.05m,
            "USD",
            "Token usage: gpt-4o");

        _repositoryMock
            .Setup(r => r.GetByIdAsync(entry.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        var query = new GetLedgerEntryByIdQuery(entry.Id);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Source.Should().Be(LedgerEntrySource.Auto);
        result.CreatedByUserId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_NonExistentEntry_ShouldThrowNotFoundException()
    {
        // Arrange
        var id = Guid.NewGuid();
        _repositoryMock
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LedgerEntry?)null);

        var query = new GetLedgerEntryByIdQuery(id);

        // Act & Assert
        await FluentActions.Invoking(() => _handler.Handle(query, CancellationToken.None))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public void Constructor_WithNullRepository_ShouldThrow()
    {
        var act = () => new GetLedgerEntryByIdQueryHandler(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("repository");
    }
}
