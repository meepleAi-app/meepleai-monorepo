using Api.BoundedContexts.BusinessSimulations.Application.Handlers;
using Api.BoundedContexts.BusinessSimulations.Application.Queries;
using Api.BoundedContexts.BusinessSimulations.Domain.Entities;
using Api.BoundedContexts.BusinessSimulations.Domain.Enums;
using Api.BoundedContexts.BusinessSimulations.Domain.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.BusinessSimulations.Application.Queries;

/// <summary>
/// Unit tests for GetLedgerEntriesQueryHandler (Issue #3722)
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "BusinessSimulations")]
public sealed class GetLedgerEntriesQueryHandlerTests
{
    private readonly Mock<ILedgerEntryRepository> _repositoryMock;
    private readonly GetLedgerEntriesQueryHandler _handler;

    public GetLedgerEntriesQueryHandlerTests()
    {
        _repositoryMock = new Mock<ILedgerEntryRepository>();
        _handler = new GetLedgerEntriesQueryHandler(_repositoryMock.Object);
    }

    [Fact]
    public async Task Handle_WithNoFilters_ShouldReturnAllEntries()
    {
        // Arrange
        var entries = new List<LedgerEntry>
        {
            LedgerEntry.CreateManualEntry(DateTime.UtcNow.AddDays(-1), LedgerEntryType.Income, LedgerCategory.Subscription, 100m, Guid.NewGuid()),
            LedgerEntry.CreateAutoEntry(DateTime.UtcNow, LedgerEntryType.Expense, LedgerCategory.TokenUsage, 5m, "USD")
        };

        _repositoryMock
            .Setup(r => r.GetFilteredAsync(null, null, null, null, null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((entries.AsReadOnly(), 2));

        var query = new GetLedgerEntriesQuery(1, 20, null, null, null, null, null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Entries.Should().HaveCount(2);
        result.Total.Should().Be(2);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task Handle_WithTypeFilter_ShouldPassToRepository()
    {
        // Arrange
        var entries = new List<LedgerEntry>
        {
            LedgerEntry.CreateManualEntry(DateTime.UtcNow, LedgerEntryType.Income, LedgerCategory.Subscription, 50m, Guid.NewGuid())
        };

        _repositoryMock
            .Setup(r => r.GetFilteredAsync(LedgerEntryType.Income, null, null, null, null, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((entries.AsReadOnly(), 1));

        var query = new GetLedgerEntriesQuery(1, 10, LedgerEntryType.Income, null, null, null, null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Entries.Should().HaveCount(1);
        result.Entries[0].Type.Should().Be(LedgerEntryType.Income);
    }

    [Fact]
    public async Task Handle_WithDateRangeFilter_ShouldPassToRepository()
    {
        // Arrange
        var from = DateTime.UtcNow.AddDays(-30);
        var to = DateTime.UtcNow;
        _repositoryMock
            .Setup(r => r.GetFilteredAsync(null, null, null, from, to, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<LedgerEntry>().AsReadOnly(), 0));

        var query = new GetLedgerEntriesQuery(1, 20, null, null, null, from, to);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Entries.Should().BeEmpty();
        result.Total.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithSourceFilter_ShouldFilterBySource()
    {
        // Arrange
        var entries = new List<LedgerEntry>
        {
            LedgerEntry.CreateManualEntry(DateTime.UtcNow, LedgerEntryType.Expense, LedgerCategory.Other, 25m, Guid.NewGuid())
        };

        _repositoryMock
            .Setup(r => r.GetFilteredAsync(null, null, LedgerEntrySource.Manual, null, null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((entries.AsReadOnly(), 1));

        var query = new GetLedgerEntriesQuery(1, 20, null, null, LedgerEntrySource.Manual, null, null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Entries.Should().HaveCount(1);
        result.Entries[0].Source.Should().Be(LedgerEntrySource.Manual);
    }

    [Fact]
    public async Task Handle_ShouldMapDtoCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var entry = LedgerEntry.CreateManualEntry(
            DateTime.UtcNow.AddDays(-5),
            LedgerEntryType.Income,
            LedgerCategory.Subscription,
            99.99m,
            userId,
            "EUR",
            "Monthly sub");

        _repositoryMock
            .Setup(r => r.GetFilteredAsync(null, null, null, null, null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<LedgerEntry> { entry }.AsReadOnly(), 1));

        var query = new GetLedgerEntriesQuery(1, 20, null, null, null, null, null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        var dto = result.Entries[0];
        dto.Id.Should().Be(entry.Id);
        dto.Amount.Should().Be(99.99m);
        dto.Currency.Should().Be("EUR");
        dto.Description.Should().Be("Monthly sub");
        dto.CreatedByUserId.Should().Be(userId);
    }

    [Fact]
    public void Constructor_WithNullRepository_ShouldThrow()
    {
        var act = () => new GetLedgerEntriesQueryHandler(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("repository");
    }
}
