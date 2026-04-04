using Api.BoundedContexts.EntityRelationships.Application.Queries;
using Api.BoundedContexts.EntityRelationships.Domain.Enums;
using Api.BoundedContexts.EntityRelationships.Domain.Repositories;
using Api.Tests.Constants;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.EntityRelationships.Application;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "EntityRelationships")]
public class GetEntityLinkCountQueryHandlerTests
{
    private static readonly Guid _gameId = Guid.NewGuid();

    private readonly Mock<IEntityLinkRepository> _repoMock;
    private readonly GetEntityLinkCountQueryHandler _handler;

    public GetEntityLinkCountQueryHandlerTests()
    {
        _repoMock = new Mock<IEntityLinkRepository>();
        _handler = new GetEntityLinkCountQueryHandler(_repoMock.Object);
    }

    // ── Happy paths ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ReturnsCountFromRepository()
    {
        _repoMock
            .Setup(r => r.GetCountForEntityAsync(
                MeepleEntityType.Game, _gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        var query = new GetEntityLinkCountQuery(MeepleEntityType.Game, _gameId);
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        result.Should().Be(5);
    }

    [Fact]
    public async Task Handle_ZeroLinks_ReturnsZero()
    {
        _repoMock
            .Setup(r => r.GetCountForEntityAsync(
                MeepleEntityType.Game, _gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var query = new GetEntityLinkCountQuery(MeepleEntityType.Game, _gameId);
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        result.Should().Be(0);
    }

    [Fact]
    public async Task Handle_PassesEntityTypeAndId_ToRepository()
    {
        _repoMock
            .Setup(r => r.GetCountForEntityAsync(
                MeepleEntityType.Agent, _gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var query = new GetEntityLinkCountQuery(MeepleEntityType.Agent, _gameId);
        await _handler.Handle(query, TestContext.Current.CancellationToken);

        _repoMock.Verify(r => r.GetCountForEntityAsync(
            MeepleEntityType.Agent, _gameId, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Null guard ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_NullQuery_ThrowsArgumentNullException()
    {
        var act = () =>
            _handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
