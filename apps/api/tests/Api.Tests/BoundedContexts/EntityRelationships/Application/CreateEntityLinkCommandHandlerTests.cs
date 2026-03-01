using Api.BoundedContexts.EntityRelationships.Application.Commands;
using Api.BoundedContexts.EntityRelationships.Application.Validators;
using Api.BoundedContexts.EntityRelationships.Domain.Aggregates;
using Api.BoundedContexts.EntityRelationships.Domain.Enums;
using Api.BoundedContexts.EntityRelationships.Domain.Exceptions;
using Api.BoundedContexts.EntityRelationships.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.EntityRelationships.Application;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "EntityRelationships")]
public class CreateEntityLinkCommandHandlerTests
{
    private static readonly Guid _ownerId = Guid.NewGuid();
    private static readonly Guid _sourceId = Guid.NewGuid();
    private static readonly Guid _targetId = Guid.NewGuid();

    private readonly Mock<IEntityLinkRepository> _repoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly CreateEntityLinkCommandHandler _handler;

    public CreateEntityLinkCommandHandlerTests()
    {
        _repoMock = new Mock<IEntityLinkRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _handler = new CreateEntityLinkCommandHandler(
            _repoMock.Object,
            _unitOfWorkMock.Object,
            new Mock<ILogger<CreateEntityLinkCommandHandler>>().Object);
    }

    // ── Happy paths ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_UserScope_ReturnsAutoApprovedDto()
    {
        // Arrange
        _repoMock
            .Setup(r => r.ExistsAsync(
                MeepleEntityType.Game, _sourceId,
                MeepleEntityType.Game, _targetId,
                EntityLinkType.ExpansionOf,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var command = new CreateEntityLinkCommand(
            MeepleEntityType.Game, _sourceId,
            MeepleEntityType.Game, _targetId,
            EntityLinkType.ExpansionOf,
            EntityLinkScope.User,
            _ownerId);

        // Act
        var dto = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEqual(Guid.Empty, dto.Id);
        Assert.True(dto.IsAdminApproved, "BR-04: user scope should be auto-approved");
        Assert.Equal(EntityLinkType.ExpansionOf, dto.LinkType);
        Assert.False(dto.IsBidirectional, "expansion_of is directed");
        _repoMock.Verify(r => r.AddAsync(It.IsAny<EntityLink>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SharedScope_RequiresApproval()
    {
        _repoMock
            .Setup(r => r.ExistsAsync(It.IsAny<MeepleEntityType>(), It.IsAny<Guid>(),
                It.IsAny<MeepleEntityType>(), It.IsAny<Guid>(), It.IsAny<EntityLinkType>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var command = new CreateEntityLinkCommand(
            MeepleEntityType.Game, _sourceId,
            MeepleEntityType.Game, _targetId,
            EntityLinkType.RelatedTo,
            EntityLinkScope.Shared,
            _ownerId);

        var dto = await _handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.False(dto.IsAdminApproved, "Shared scope requires explicit admin approval");
    }

    [Fact]
    public async Task Handle_BilateralLinkType_SetsBidirectional()
    {
        _repoMock
            .Setup(r => r.ExistsAsync(It.IsAny<MeepleEntityType>(), It.IsAny<Guid>(),
                It.IsAny<MeepleEntityType>(), It.IsAny<Guid>(), It.IsAny<EntityLinkType>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var command = new CreateEntityLinkCommand(
            MeepleEntityType.Game, _sourceId,
            MeepleEntityType.Game, _targetId,
            EntityLinkType.CompanionTo,
            EntityLinkScope.User,
            _ownerId);

        var dto = await _handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.True(dto.IsBidirectional, "companion_to is bilateral");
    }

    [Fact]
    public async Task Handle_BggImport_IsAutoApproved()
    {
        _repoMock
            .Setup(r => r.ExistsAsync(It.IsAny<MeepleEntityType>(), It.IsAny<Guid>(),
                It.IsAny<MeepleEntityType>(), It.IsAny<Guid>(), It.IsAny<EntityLinkType>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var command = new CreateEntityLinkCommand(
            MeepleEntityType.Game, _sourceId,
            MeepleEntityType.Game, _targetId,
            EntityLinkType.ExpansionOf,
            EntityLinkScope.Shared,
            _ownerId,
            IsBggImported: true);

        var dto = await _handler.Handle(command, TestContext.Current.CancellationToken);

        Assert.True(dto.IsAdminApproved, "BGG imported links are always auto-approved");
        Assert.True(dto.IsBggImported);
    }

    // ── BR-08: Duplicate check ─────────────────────────────────────────────────

    [Fact]
    public async Task Handle_DuplicateLink_ThrowsDuplicateEntityLinkException()
    {
        _repoMock
            .Setup(r => r.ExistsAsync(
                MeepleEntityType.Game, _sourceId,
                MeepleEntityType.Game, _targetId,
                EntityLinkType.ExpansionOf,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = new CreateEntityLinkCommand(
            MeepleEntityType.Game, _sourceId,
            MeepleEntityType.Game, _targetId,
            EntityLinkType.ExpansionOf,
            EntityLinkScope.User,
            _ownerId);

        await Assert.ThrowsAsync<DuplicateEntityLinkException>(() =>
            _handler.Handle(command, TestContext.Current.CancellationToken));

        _repoMock.Verify(r => r.AddAsync(It.IsAny<EntityLink>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Null guard ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_NullCommand_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _handler.Handle(null!, TestContext.Current.CancellationToken));
    }

    // ── Validator tests ───────────────────────────────────────────────────────

    [Fact]
    public void Validator_SameSourceAndTarget_IsInvalid()
    {
        var sameId = Guid.NewGuid();
        var command = new CreateEntityLinkCommand(
            MeepleEntityType.Game, sameId,
            MeepleEntityType.Game, sameId,
            EntityLinkType.ExpansionOf,
            EntityLinkScope.User,
            _ownerId);

        var validator = new CreateEntityLinkCommandValidator();
        var result = validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "TargetEntityId");
    }

    [Fact]
    public void Validator_EmptyOwnerUserId_IsInvalid()
    {
        var command = new CreateEntityLinkCommand(
            MeepleEntityType.Game, _sourceId,
            MeepleEntityType.Game, _targetId,
            EntityLinkType.ExpansionOf,
            EntityLinkScope.User,
            Guid.Empty);

        var validator = new CreateEntityLinkCommandValidator();
        var result = validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "OwnerUserId");
    }

    [Fact]
    public void Validator_ValidCommand_IsValid()
    {
        var command = new CreateEntityLinkCommand(
            MeepleEntityType.Game, _sourceId,
            MeepleEntityType.Game, _targetId,
            EntityLinkType.ExpansionOf,
            EntityLinkScope.User,
            _ownerId);

        var validator = new CreateEntityLinkCommandValidator();
        var result = validator.Validate(command);

        Assert.True(result.IsValid);
    }
}
