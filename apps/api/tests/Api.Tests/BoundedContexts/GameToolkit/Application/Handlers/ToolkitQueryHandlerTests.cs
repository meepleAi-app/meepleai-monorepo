using Api.BoundedContexts.GameToolkit.Application.Commands;
using Api.BoundedContexts.GameToolkit.Application.Queries;
using Api.BoundedContexts.GameToolkit.Application.Queries;
using Api.BoundedContexts.GameToolkit.Domain.Repositories;
using Api.Tests.Constants;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.GameToolkit.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameToolkit")]
public class ToolkitQueryHandlerTests
{
    private readonly Mock<IGameToolkitRepository> _repoMock;

    public ToolkitQueryHandlerTests()
    {
        _repoMock = new Mock<IGameToolkitRepository>();
    }

    // ========================================================================
    // GetToolkitQueryHandler
    // ========================================================================

    [Fact]
    public async Task GetToolkit_WhenExists_ReturnsDto()
    {
        var toolkit = CreateTestToolkit();
        _repoMock.Setup(r => r.GetByIdAsync(toolkit.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(toolkit);

        var handler = new GetToolkitQueryHandler(_repoMock.Object);
        var query = new GetToolkitQuery(toolkit.Id);

        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result.Id.Should().Be(toolkit.Id);
        result.Name.Should().Be("Test Toolkit");
    }

    [Fact]
    public async Task GetToolkit_WhenNotFound_ReturnsNull()
    {
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Api.BoundedContexts.GameToolkit.Domain.Entities.GameToolkit?)null);

        var handler = new GetToolkitQueryHandler(_repoMock.Object);
        var query = new GetToolkitQuery(Guid.NewGuid());

        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetToolkit_WithNullQuery_ThrowsArgumentNullException()
    {
        var handler = new GetToolkitQueryHandler(_repoMock.Object);

        var act = () =>
            handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    // ========================================================================
    // GetToolkitsByGameQueryHandler
    // ========================================================================

    [Fact]
    public async Task GetToolkitsByGame_ReturnsAllForGame()
    {
        var gameId = Guid.NewGuid();
        var toolkits = new List<Api.BoundedContexts.GameToolkit.Domain.Entities.GameToolkit>
        {
            CreateTestToolkit(gameId: gameId, name: "Toolkit 1"),
            CreateTestToolkit(gameId: gameId, name: "Toolkit 2"),
        };

        _repoMock.Setup(r => r.GetByGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(toolkits);

        var handler = new GetToolkitsByGameQueryHandler(_repoMock.Object);
        var query = new GetToolkitsByGameQuery(gameId);

        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        result.Count.Should().Be(2);
        result[0].Name.Should().Be("Toolkit 1");
        result[1].Name.Should().Be("Toolkit 2");
    }

    [Fact]
    public async Task GetToolkitsByGame_WhenNoToolkits_ReturnsEmptyList()
    {
        _repoMock.Setup(r => r.GetByGameIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Api.BoundedContexts.GameToolkit.Domain.Entities.GameToolkit>());

        var handler = new GetToolkitsByGameQueryHandler(_repoMock.Object);

        var result = await handler.Handle(new GetToolkitsByGameQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        result.Should().BeEmpty();
    }

    // ========================================================================
    // GetPublishedToolkitsQueryHandler
    // ========================================================================

    [Fact]
    public async Task GetPublishedToolkits_ReturnsOnlyPublished()
    {
        var published = CreateTestToolkit(name: "Published");
        published.Publish();

        _repoMock.Setup(r => r.GetPublishedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Api.BoundedContexts.GameToolkit.Domain.Entities.GameToolkit> { published });

        var handler = new GetPublishedToolkitsQueryHandler(_repoMock.Object);

        var result = await handler.Handle(new GetPublishedToolkitsQuery(), TestContext.Current.CancellationToken);

        result.Should().ContainSingle();
        result[0].IsPublished.Should().BeTrue();
    }

    [Fact]
    public async Task GetPublishedToolkits_WhenNone_ReturnsEmptyList()
    {
        _repoMock.Setup(r => r.GetPublishedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Api.BoundedContexts.GameToolkit.Domain.Entities.GameToolkit>());

        var handler = new GetPublishedToolkitsQueryHandler(_repoMock.Object);

        var result = await handler.Handle(new GetPublishedToolkitsQuery(), TestContext.Current.CancellationToken);

        result.Should().BeEmpty();
    }

    // ========================================================================
    // Helper
    // ========================================================================

    private static Api.BoundedContexts.GameToolkit.Domain.Entities.GameToolkit CreateTestToolkit(
        Guid? gameId = null, string name = "Test Toolkit")
    {
        return new Api.BoundedContexts.GameToolkit.Domain.Entities.GameToolkit(
            Guid.NewGuid(), gameId ?? Guid.NewGuid(), name, Guid.NewGuid());
    }
}
