using Api.BoundedContexts.Administration.Application.Queries.Usage;
using Api.SharedKernel.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Queries.Usage;

/// <summary>
/// Unit tests for GetUserUsageQueryHandler.
/// E2-2: Game Night Improvvisata - User Usage Endpoint.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
public sealed class GetUserUsageQueryHandlerTests
{
    private readonly Mock<ITierEnforcementService> _mockTierService;
    private readonly GetUserUsageQueryHandler _handler;

    public GetUserUsageQueryHandlerTests()
    {
        _mockTierService = new Mock<ITierEnforcementService>();
        _handler = new GetUserUsageQueryHandler(_mockTierService.Object);
    }

    [Fact]
    public async Task Handle_DelegatesToTierEnforcementService()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var expectedSnapshot = new UsageSnapshot(
            PrivateGames: 3, PrivateGamesMax: 10,
            PdfThisMonth: 5, PdfThisMonthMax: 20,
            AgentQueriesToday: 12, AgentQueriesTodayMax: 50,
            SessionQueries: 2, SessionQueriesMax: 5,
            Agents: 1, AgentsMax: 3,
            PhotosThisSession: 0, PhotosThisSessionMax: 10,
            SessionSaveEnabled: true,
            CatalogProposalsThisWeek: 1, CatalogProposalsThisWeekMax: 3
        );

        _mockTierService
            .Setup(s => s.GetUsageAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedSnapshot);

        var query = new GetUserUsageQuery(userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().Be(expectedSnapshot);
        _mockTierService.Verify(s => s.GetUsageAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithDifferentUserId_PassesCorrectId()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var emptySnapshot = new UsageSnapshot(
            PrivateGames: 0, PrivateGamesMax: 5,
            PdfThisMonth: 0, PdfThisMonthMax: 10,
            AgentQueriesToday: 0, AgentQueriesTodayMax: 25,
            SessionQueries: 0, SessionQueriesMax: 3,
            Agents: 0, AgentsMax: 1,
            PhotosThisSession: 0, PhotosThisSessionMax: 5,
            SessionSaveEnabled: false,
            CatalogProposalsThisWeek: 0, CatalogProposalsThisWeekMax: 1
        );

        _mockTierService
            .Setup(s => s.GetUsageAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(emptySnapshot);

        var query = new GetUserUsageQuery(userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.PrivateGames.Should().Be(0);
        result.SessionSaveEnabled.Should().BeFalse();
        _mockTierService.Verify(s => s.GetUsageAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void GetUserUsageQuery_WithValidUserId_CreatesQuery()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var query = new GetUserUsageQuery(userId);

        // Assert
        query.UserId.Should().Be(userId);
    }

    [Fact]
    public void Constructor_WithNullService_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new GetUserUsageQueryHandler(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("tierService");
    }
}
