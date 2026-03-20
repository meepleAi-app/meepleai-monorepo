using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Api.Tests.Constants;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Tests for GetUserDashboardQueryHandler (Issue #2854).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
public class GetUserDashboardQueryHandlerTests
{
    private readonly Mock<IUserDashboardService> _mockUserDashboardService;
    private readonly Mock<ILogger<GetUserDashboardQueryHandler>> _mockLogger;
    private readonly GetUserDashboardQueryHandler _handler;

    public GetUserDashboardQueryHandlerTests()
    {
        _mockUserDashboardService = new Mock<IUserDashboardService>();
        _mockLogger = new Mock<ILogger<GetUserDashboardQueryHandler>>();
        _handler = new GetUserDashboardQueryHandler(_mockUserDashboardService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ShouldReturnDashboardData()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetUserDashboardQuery(userId);
        var expectedDashboard = new Api.BoundedContexts.Administration.Application.DTOs.UserDashboardDto(
            RecentGames: new List<Api.BoundedContexts.Administration.Application.DTOs.RecentGameDto>(),
            ActiveSessions: new List<Api.BoundedContexts.Administration.Application.DTOs.ActiveSessionDto>(),
            RecentChats: new List<Api.BoundedContexts.Administration.Application.DTOs.RecentChatDto>(),
            LibraryQuota: new Api.BoundedContexts.Administration.Application.DTOs.LibraryQuotaDto(0, 1000, 0)
        );

        _mockUserDashboardService
            .Setup(s => s.GetUserDashboardAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDashboard);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(expectedDashboard);
        _mockUserDashboardService.Verify(
            s => s.GetUserDashboardAsync(userId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldLogInformation()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetUserDashboardQuery(userId);
        var dashboard = new Api.BoundedContexts.Administration.Application.DTOs.UserDashboardDto(
            RecentGames: new List<Api.BoundedContexts.Administration.Application.DTOs.RecentGameDto>
            {
                new(Guid.NewGuid(), "Game 1", null, DateTime.UtcNow, false)
            },
            ActiveSessions: new List<Api.BoundedContexts.Administration.Application.DTOs.ActiveSessionDto>(),
            RecentChats: new List<Api.BoundedContexts.Administration.Application.DTOs.RecentChatDto>
            {
                new(Guid.NewGuid(), "Game 2", "Chat Title", "Last message", DateTime.UtcNow)
            },
            LibraryQuota: new Api.BoundedContexts.Administration.Application.DTOs.LibraryQuotaDto(5, 1000, 0)
        );

        _mockUserDashboardService
            .Setup(s => s.GetUserDashboardAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dashboard);

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Fetching dashboard data")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}