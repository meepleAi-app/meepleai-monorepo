using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.Models;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Tests for ExportStatsCommandHandler.
/// Tests stats export functionality with format validation and date range handling.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class ExportStatsCommandHandlerTests
{
    private readonly Mock<IAdminStatsService> _mockAdminStatsService;
    private readonly ExportStatsCommandHandler _handler;

    public ExportStatsCommandHandlerTests()
    {
        _mockAdminStatsService = new Mock<IAdminStatsService>();
        _handler = new ExportStatsCommandHandler(_mockAdminStatsService.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ReturnsExportedData()
    {
        // Arrange
        var command = new ExportStatsCommand(
            Format: "csv",
            FromDate: DateTime.UtcNow.AddDays(-7),
            ToDate: DateTime.UtcNow,
            GameId: null
        );

        var expectedExportData = "UserId,GameId,Queries,AvgLatency\n1,game-1,10,150\n";
        _mockAdminStatsService
            .Setup(s => s.ExportDashboardDataAsync(It.IsAny<ExportDataRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedExportData);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(expectedExportData);
        _mockAdminStatsService.Verify(
            s => s.ExportDashboardDataAsync(
                It.Is<ExportDataRequest>(r =>
                    r.Format == "csv" &&
                    r.FromDate == command.FromDate &&
                    r.ToDate == command.ToDate &&
                    r.GameId == null),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithGameIdFilter_PassesGameIdToService()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var command = new ExportStatsCommand(
            Format: "json",
            FromDate: DateTime.UtcNow.AddMonths(-1),
            ToDate: DateTime.UtcNow,
            GameId: gameId.ToString()
        );

        var expectedExportData = "{\"stats\":[]}";
        _mockAdminStatsService
            .Setup(s => s.ExportDashboardDataAsync(It.IsAny<ExportDataRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedExportData);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(expectedExportData);
        _mockAdminStatsService.Verify(
            s => s.ExportDashboardDataAsync(
                It.Is<ExportDataRequest>(r =>
                    r.Format == "json" &&
                    r.GameId == gameId.ToString()),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Arrange
        ExportStatsCommand? command = null;

        // Act & Assert
        var act = () =>
            _handler.Handle(command!, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentNullException>();

        _mockAdminStatsService.Verify(
            s => s.ExportDashboardDataAsync(It.IsAny<ExportDataRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
