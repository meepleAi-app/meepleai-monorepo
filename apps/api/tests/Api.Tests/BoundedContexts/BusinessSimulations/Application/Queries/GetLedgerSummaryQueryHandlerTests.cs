using Api.BoundedContexts.BusinessSimulations.Application.Queries;
using Api.BoundedContexts.BusinessSimulations.Domain.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.BusinessSimulations.Application.Queries;

/// <summary>
/// Unit tests for GetLedgerSummaryQueryHandler (Issue #3722)
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "BusinessSimulations")]
public sealed class GetLedgerSummaryQueryHandlerTests
{
    private readonly Mock<ILedgerEntryRepository> _repositoryMock;
    private readonly GetLedgerSummaryQueryHandler _handler;

    public GetLedgerSummaryQueryHandlerTests()
    {
        _repositoryMock = new Mock<ILedgerEntryRepository>();
        _handler = new GetLedgerSummaryQueryHandler(_repositoryMock.Object);
    }

    [Fact]
    public async Task Handle_WithData_ShouldReturnSummary()
    {
        // Arrange
        var from = DateTime.UtcNow.AddDays(-30);
        var to = DateTime.UtcNow;

        _repositoryMock
            .Setup(r => r.GetSummaryByDateRangeAsync(from, to, It.IsAny<CancellationToken>()))
            .ReturnsAsync((5000m, 1200m));

        var query = new GetLedgerSummaryQuery(from, to);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.TotalIncome.Should().Be(5000m);
        result.TotalExpense.Should().Be(1200m);
        result.NetBalance.Should().Be(3800m);
        result.From.Should().Be(from);
        result.To.Should().Be(to);
    }

    [Fact]
    public async Task Handle_WithNoData_ShouldReturnZeros()
    {
        // Arrange
        var from = DateTime.UtcNow.AddDays(-7);
        var to = DateTime.UtcNow;

        _repositoryMock
            .Setup(r => r.GetSummaryByDateRangeAsync(from, to, It.IsAny<CancellationToken>()))
            .ReturnsAsync((0m, 0m));

        var query = new GetLedgerSummaryQuery(from, to);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.TotalIncome.Should().Be(0m);
        result.TotalExpense.Should().Be(0m);
        result.NetBalance.Should().Be(0m);
    }

    [Fact]
    public async Task Handle_MoreExpenseThanIncome_ShouldReturnNegativeBalance()
    {
        // Arrange
        var from = DateTime.UtcNow.AddDays(-7);
        var to = DateTime.UtcNow;

        _repositoryMock
            .Setup(r => r.GetSummaryByDateRangeAsync(from, to, It.IsAny<CancellationToken>()))
            .ReturnsAsync((500m, 2000m));

        var query = new GetLedgerSummaryQuery(from, to);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.NetBalance.Should().Be(-1500m);
    }

    [Fact]
    public void Constructor_WithNullRepository_ShouldThrow()
    {
        var act = () => new GetLedgerSummaryQueryHandler(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("repository");
    }
}
