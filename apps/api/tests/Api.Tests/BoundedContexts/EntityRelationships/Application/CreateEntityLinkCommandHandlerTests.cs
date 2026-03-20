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
using FluentAssertions;
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
        dto.Id.Should().NotBe(Guid.Empty);
        dto.IsAdminApproved.Should().BeTrue("BR-04: user scope should be auto-approved");
        dto.LinkType.Should().Be(EntityLinkType.ExpansionOf);
        dto.IsBidirectional.Should().BeFalse("expansion_of is directed");
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

        dto.IsAdminApproved.Should().BeFalse("Shared scope requires explicit admin approval");
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

        dto.IsBidirectional.Should().BeTrue("companion_to is bilateral");
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

        dto.IsAdminApproved.Should().BeTrue("BGG imported links are always auto-approved");
        dto.IsBggImported.Should().BeTrue();
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

        var act = () =>
            _handler.Handle(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<DuplicateEntityLinkException>();

        _repoMock.Verify(r => r.AddAsync(It.IsAny<EntityLink>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Null guard ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_NullCommand_ThrowsArgumentNullException()
    {
        var act2 = () =>
            _handler.Handle(null!, TestContext.Current.CancellationToken);
        await act2.Should().ThrowAsync<ArgumentNullException>();
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

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "TargetEntityId");
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

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "OwnerUserId");
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

        result.IsValid.Should().BeTrue();
    }
}
