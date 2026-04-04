using Api.BoundedContexts.EntityRelationships.Application.Commands;
using Api.BoundedContexts.EntityRelationships.Domain.Aggregates;
using Api.BoundedContexts.EntityRelationships.Domain.Enums;
using Api.BoundedContexts.EntityRelationships.Domain.Exceptions;
using Api.BoundedContexts.EntityRelationships.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.EntityRelationships.Application;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "EntityRelationships")]
public class DeleteEntityLinkCommandHandlerTests
{
    private static readonly Guid _ownerId = Guid.NewGuid();
    private static readonly Guid _otherId = Guid.NewGuid();
    private static readonly Guid _sourceId = Guid.NewGuid();
    private static readonly Guid _targetId = Guid.NewGuid();

    private readonly Mock<IEntityLinkRepository> _repoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly DeleteEntityLinkCommandHandler _handler;

    public DeleteEntityLinkCommandHandlerTests()
    {
        _repoMock = new Mock<IEntityLinkRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _handler = new DeleteEntityLinkCommandHandler(
            _repoMock.Object,
            _unitOfWorkMock.Object,
            new Mock<ILogger<DeleteEntityLinkCommandHandler>>().Object);
    }

    private static EntityLink BuildLink(Guid? ownerId = null, bool isBggImported = false)
    {
        return EntityLink.Create(
            MeepleEntityType.Game, _sourceId,
            MeepleEntityType.Game, _targetId,
            EntityLinkType.ExpansionOf,
            EntityLinkScope.User,
            ownerId ?? _ownerId,
            isBggImported: isBggImported);
    }

    // ── Happy paths ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_OwnerDeletes_SoftDeletesLink()
    {
        var link = BuildLink(_ownerId);
        _repoMock.Setup(r => r.GetByIdAsync(link.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(link);

        var command = new DeleteEntityLinkCommand(link.Id, _ownerId, IsAdmin: false);
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        link.IsDeleted.Should().BeTrue();
        link.DeletedAt.Should().NotBeNull();
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AdminDeletesAnyLink_SoftDeletesLink()
    {
        var link = BuildLink(_otherId); // owned by someone else
        _repoMock.Setup(r => r.GetByIdAsync(link.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(link);

        var command = new DeleteEntityLinkCommand(link.Id, _ownerId, IsAdmin: true);
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        link.IsDeleted.Should().BeTrue();
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AdminDeletesBggImportedLink_Succeeds()
    {
        var link = BuildLink(_ownerId, isBggImported: true);
        _repoMock.Setup(r => r.GetByIdAsync(link.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(link);

        var command = new DeleteEntityLinkCommand(link.Id, _ownerId, IsAdmin: true);
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        link.IsDeleted.Should().BeTrue();
    }

    // ── Not found ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_LinkNotFound_ThrowsEntityLinkNotFoundException()
    {
        var missingId = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByIdAsync(missingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EntityLink?)null);

        var command = new DeleteEntityLinkCommand(missingId, _ownerId, IsAdmin: false);
        var act = () =>
            _handler.Handle(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<EntityLinkNotFoundException>();

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Authorization ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_UserDeletesOtherOwnersLink_ThrowsUnauthorized()
    {
        var link = BuildLink(_otherId); // owned by _otherId
        _repoMock.Setup(r => r.GetByIdAsync(link.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(link);

        var command = new DeleteEntityLinkCommand(link.Id, _ownerId, IsAdmin: false);
        var act2 = () =>
            _handler.Handle(command, TestContext.Current.CancellationToken);
        await act2.Should().ThrowAsync<UnauthorizedEntityLinkAccessException>();

        link.IsDeleted.Should().BeFalse();
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_UserDeletesBggImportedLink_ThrowsUnauthorized()
    {
        var link = BuildLink(_ownerId, isBggImported: true); // user owns but it's BGG-imported
        _repoMock.Setup(r => r.GetByIdAsync(link.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(link);

        var command = new DeleteEntityLinkCommand(link.Id, _ownerId, IsAdmin: false);
        var act3 = () =>
            _handler.Handle(command, TestContext.Current.CancellationToken);
        await act3.Should().ThrowAsync<UnauthorizedEntityLinkAccessException>();

        link.IsDeleted.Should().BeFalse();
    }

    // ── Null guard ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_NullCommand_ThrowsArgumentNullException()
    {
        var act4 = () =>
            _handler.Handle(null!, TestContext.Current.CancellationToken);
        await act4.Should().ThrowAsync<ArgumentNullException>();
    }
}
