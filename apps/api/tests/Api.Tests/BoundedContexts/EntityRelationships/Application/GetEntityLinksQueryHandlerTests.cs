using Api.BoundedContexts.EntityRelationships.Application.Queries;
using Api.BoundedContexts.EntityRelationships.Domain.Aggregates;
using Api.BoundedContexts.EntityRelationships.Domain.Enums;
using Api.BoundedContexts.EntityRelationships.Domain.Repositories;
using Api.Tests.Constants;
using MediatR;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.EntityRelationships.Application;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "EntityRelationships")]
public class GetEntityLinksQueryHandlerTests
{
    private static readonly Guid _ownerId = Guid.NewGuid();
    private static readonly Guid _gameId = Guid.NewGuid();
    private static readonly Guid _targetId1 = Guid.NewGuid();
    private static readonly Guid _targetId2 = Guid.NewGuid();

    private readonly Mock<IEntityLinkRepository> _repoMock;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly GetEntityLinksQueryHandler _handler;

    public GetEntityLinksQueryHandlerTests()
    {
        _repoMock = new Mock<IEntityLinkRepository>();
        _mediatorMock = new Mock<IMediator>();
        _handler = new GetEntityLinksQueryHandler(_repoMock.Object, _mediatorMock.Object);
    }

    private static EntityLink BuildLink(
        Guid sourceId, Guid targetId,
        EntityLinkType linkType = EntityLinkType.ExpansionOf,
        EntityLinkScope scope = EntityLinkScope.User,
        Guid? ownerId = null)
    {
        return EntityLink.Create(
            MeepleEntityType.Game, sourceId,
            MeepleEntityType.Game, targetId,
            linkType, scope, ownerId ?? _ownerId);
    }

    // ── Happy paths ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ReturnsAllLinksFromRepo()
    {
        var link1 = BuildLink(_gameId, _targetId1);
        var link2 = BuildLink(_gameId, _targetId2);

        _repoMock
            .Setup(r => r.GetForEntityAsync(
                MeepleEntityType.Game, _gameId,
                null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([link1, link2]);

        var query = new GetEntityLinksQuery(MeepleEntityType.Game, _gameId);
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task Handle_WithRequestingUserId_PopulatesIsOwner()
    {
        var otherUserId = Guid.NewGuid();
        var myLink = BuildLink(_gameId, _targetId1, ownerId: _ownerId);
        var otherLink = BuildLink(_gameId, _targetId2, ownerId: otherUserId);

        _repoMock
            .Setup(r => r.GetForEntityAsync(
                MeepleEntityType.Game, _gameId,
                null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([myLink, otherLink]);

        var query = new GetEntityLinksQuery(MeepleEntityType.Game, _gameId, RequestingUserId: _ownerId);
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        var myDto = result.Single(d => d.OwnerUserId == _ownerId);
        var otherDto = result.Single(d => d.OwnerUserId == otherUserId);

        Assert.True(myDto.IsOwner, "Link owned by requesting user should have IsOwner=true");
        Assert.False(otherDto.IsOwner, "Link owned by another user should have IsOwner=false");
    }

    [Fact]
    public async Task Handle_WithoutRequestingUserId_IsOwnerDefaultsFalse()
    {
        var link = BuildLink(_gameId, _targetId1, ownerId: _ownerId);

        _repoMock
            .Setup(r => r.GetForEntityAsync(
                MeepleEntityType.Game, _gameId,
                null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([link]);

        var query = new GetEntityLinksQuery(MeepleEntityType.Game, _gameId);
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        Assert.False(result[0].IsOwner, "No requesting user — IsOwner should default false");
    }

    [Fact]
    public async Task Handle_PassesScopeFilter_ToRepository()
    {
        _repoMock
            .Setup(r => r.GetForEntityAsync(
                MeepleEntityType.Game, _gameId,
                EntityLinkScope.User, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var query = new GetEntityLinksQuery(MeepleEntityType.Game, _gameId, Scope: EntityLinkScope.User);
        await _handler.Handle(query, TestContext.Current.CancellationToken);

        _repoMock.Verify(r => r.GetForEntityAsync(
            MeepleEntityType.Game, _gameId,
            EntityLinkScope.User, null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_PassesLinkTypeFilter_ToRepository()
    {
        _repoMock
            .Setup(r => r.GetForEntityAsync(
                MeepleEntityType.Game, _gameId,
                null, EntityLinkType.ExpansionOf, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var query = new GetEntityLinksQuery(MeepleEntityType.Game, _gameId, LinkType: EntityLinkType.ExpansionOf);
        await _handler.Handle(query, TestContext.Current.CancellationToken);

        _repoMock.Verify(r => r.GetForEntityAsync(
            MeepleEntityType.Game, _gameId,
            null, EntityLinkType.ExpansionOf, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_PassesTargetEntityTypeFilter_ToRepository()
    {
        _repoMock
            .Setup(r => r.GetForEntityAsync(
                MeepleEntityType.Game, _gameId,
                null, null, MeepleEntityType.KbCard, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var query = new GetEntityLinksQuery(MeepleEntityType.Game, _gameId, TargetEntityType: MeepleEntityType.KbCard);
        await _handler.Handle(query, TestContext.Current.CancellationToken);

        _repoMock.Verify(r => r.GetForEntityAsync(
            MeepleEntityType.Game, _gameId,
            null, null, MeepleEntityType.KbCard, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_EmptyResult_ReturnsEmptyList()
    {
        _repoMock
            .Setup(r => r.GetForEntityAsync(
                It.IsAny<MeepleEntityType>(), It.IsAny<Guid>(),
                It.IsAny<EntityLinkScope?>(), It.IsAny<EntityLinkType?>(),
                It.IsAny<MeepleEntityType?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var query = new GetEntityLinksQuery(MeepleEntityType.Game, _gameId);
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    // ── Null guard ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_NullQuery_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _handler.Handle(null!, TestContext.Current.CancellationToken));
    }
}
