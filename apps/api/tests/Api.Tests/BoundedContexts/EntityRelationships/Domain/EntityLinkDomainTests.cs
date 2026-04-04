using Api.BoundedContexts.EntityRelationships.Domain.Aggregates;
using Api.BoundedContexts.EntityRelationships.Domain.Enums;
using Api.BoundedContexts.EntityRelationships.Domain.Events;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.EntityRelationships.Domain;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "EntityRelationships")]
public class EntityLinkDomainTests
{
    private static readonly Guid _ownerId = Guid.NewGuid();
    private static readonly Guid _sourceId = Guid.NewGuid();
    private static readonly Guid _targetId = Guid.NewGuid();

    // ──────────────────────────────────────────────────────────────────────────
    // Create() happy paths
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_WithValidParameters_ReturnsEntityLink()
    {
        var link = EntityLink.Create(
            MeepleEntityType.Game, _sourceId,
            MeepleEntityType.Game, _targetId,
            EntityLinkType.ExpansionOf,
            EntityLinkScope.User, _ownerId);

        link.Id.Should().NotBe(Guid.Empty);
        link.SourceEntityType.Should().Be(MeepleEntityType.Game);
        link.SourceEntityId.Should().Be(_sourceId);
        link.TargetEntityType.Should().Be(MeepleEntityType.Game);
        link.TargetEntityId.Should().Be(_targetId);
        link.LinkType.Should().Be(EntityLinkType.ExpansionOf);
        link.OwnerUserId.Should().Be(_ownerId);
    }

    [Fact]
    public void Create_UserScope_IsAutoApproved()
    {
        // BR-04: user-scope links are auto-approved
        var link = EntityLink.Create(
            MeepleEntityType.Game, _sourceId,
            MeepleEntityType.Game, _targetId,
            EntityLinkType.RelatedTo,
            EntityLinkScope.User, _ownerId);

        link.IsAdminApproved.Should().BeTrue();
    }

    [Fact]
    public void Create_SharedScope_IsNotAutoApproved()
    {
        // BR-05: shared-scope links require admin approval
        var link = EntityLink.Create(
            MeepleEntityType.Game, _sourceId,
            MeepleEntityType.Game, _targetId,
            EntityLinkType.RelatedTo,
            EntityLinkScope.Shared, _ownerId);

        link.IsAdminApproved.Should().BeFalse();
    }

    [Fact]
    public void Create_BggImported_IsAlwaysApproved()
    {
        var link = EntityLink.Create(
            MeepleEntityType.Game, _sourceId,
            MeepleEntityType.Game, _targetId,
            EntityLinkType.ExpansionOf,
            EntityLinkScope.Shared, _ownerId,
            isBggImported: true);

        link.IsAdminApproved.Should().BeTrue();
        link.IsBggImported.Should().BeTrue();
    }

    [Fact]
    public void Create_RaisesEntityLinkCreatedEvent()
    {
        var link = EntityLink.Create(
            MeepleEntityType.Game, _sourceId,
            MeepleEntityType.Agent, _targetId,
            EntityLinkType.CollaboratesWith,
            EntityLinkScope.User, _ownerId);

        var events = link.PopDomainEvents();
        events.Should().ContainSingle();
        events[0].Should().BeOfType<EntityLinkCreatedEvent>();
        var evt = (EntityLinkCreatedEvent)events[0];
        evt.EntityLinkId.Should().Be(link.Id);
        evt.LinkType.Should().Be(EntityLinkType.CollaboratesWith);
        evt.OwnerUserId.Should().Be(_ownerId);
    }

    [Fact]
    public void PopDomainEvents_ClearsEvents()
    {
        var link = EntityLink.Create(
            MeepleEntityType.Game, _sourceId,
            MeepleEntityType.Game, _targetId,
            EntityLinkType.ExpansionOf,
            EntityLinkScope.User, _ownerId);

        link.PopDomainEvents(); // first call clears
        var events = link.PopDomainEvents(); // second call should be empty

        events.Should().BeEmpty();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Create() validation — self-reference
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_SameSourceAndTarget_ThrowsArgumentException()
    {
        var sameId = Guid.NewGuid();

        var act = () => EntityLink.Create(
            MeepleEntityType.Game, sameId,
            MeepleEntityType.Game, sameId,
            EntityLinkType.RelatedTo,
            EntityLinkScope.User, _ownerId);
        var ex = act.Should().Throw<ArgumentException>().Which;

        ex.Message.Should().ContainEquivalentOf("same");
    }

    [Fact]
    public void Create_DifferentTypeSameId_DoesNotThrow()
    {
        // Different entity type with same GUID is allowed (different entities)
        var sameId = Guid.NewGuid();

        var link = EntityLink.Create(
            MeepleEntityType.Game, sameId,
            MeepleEntityType.Agent, sameId, // different type
            EntityLinkType.RelatedTo,
            EntityLinkScope.User, _ownerId);

        link.Should().NotBeNull();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // IsBidirectional derivation
    // ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(EntityLinkType.CompanionTo, true)]
    [InlineData(EntityLinkType.RelatedTo, true)]
    [InlineData(EntityLinkType.CollaboratesWith, true)]
    [InlineData(EntityLinkType.ExpansionOf, false)]
    [InlineData(EntityLinkType.SequelOf, false)]
    [InlineData(EntityLinkType.Reimplements, false)]
    [InlineData(EntityLinkType.PartOf, false)]
    [InlineData(EntityLinkType.SpecializedBy, false)]
    public void Create_IsBidirectional_DerivedFromLinkType(EntityLinkType linkType, bool expected)
    {
        var link = EntityLink.Create(
            MeepleEntityType.Game, _sourceId,
            MeepleEntityType.Game, _targetId,
            linkType, EntityLinkScope.User, _ownerId);

        link.IsBidirectional.Should().Be(expected);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Approve()
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Approve_SharedLink_SetsIsAdminApproved()
    {
        var link = EntityLink.Create(
            MeepleEntityType.Game, _sourceId,
            MeepleEntityType.Game, _targetId,
            EntityLinkType.ExpansionOf,
            EntityLinkScope.Shared, _ownerId);

        link.IsAdminApproved.Should().BeFalse();
        link.Approve();
        link.IsAdminApproved.Should().BeTrue();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Delete()
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Delete_RaisesEntityLinkDeletedEvent()
    {
        var link = EntityLink.Create(
            MeepleEntityType.Game, _sourceId,
            MeepleEntityType.Game, _targetId,
            EntityLinkType.ExpansionOf,
            EntityLinkScope.User, _ownerId);

        link.PopDomainEvents(); // clear creation event
        var adminId = Guid.NewGuid();
        link.Delete(adminId);

        var events = link.PopDomainEvents();
        events.Should().ContainSingle();
        events[0].Should().BeOfType<EntityLinkDeletedEvent>();
        var evt = (EntityLinkDeletedEvent)events[0];
        evt.EntityLinkId.Should().Be(link.Id);
        evt.DeletedByUserId.Should().Be(adminId);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Metadata
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_WithMetadata_StoresMetadata()
    {
        var metadata = "{\"notes\":\"Seafarers requires base game\",\"bgg_id\":\"197406\"}";
        var link = EntityLink.Create(
            MeepleEntityType.Game, _sourceId,
            MeepleEntityType.Game, _targetId,
            EntityLinkType.ExpansionOf,
            EntityLinkScope.User, _ownerId,
            metadata: metadata);

        link.Metadata.Should().Be(metadata);
    }

    [Fact]
    public void UpdateMetadata_ChangesMetadata()
    {
        var link = EntityLink.Create(
            MeepleEntityType.Game, _sourceId,
            MeepleEntityType.Game, _targetId,
            EntityLinkType.RelatedTo,
            EntityLinkScope.User, _ownerId);

        var newMetadata = "{\"notes\":\"updated\"}";
        link.UpdateMetadata(newMetadata);

        link.Metadata.Should().Be(newMetadata);
    }

    // ── KbCard entity type (Issue #5184) ──────────────────────────────────────

    [Fact]
    public void MeepleEntityType_KbCard_HasValue9()
    {
        ((int)MeepleEntityType.KbCard).Should().Be(9);
    }

    [Fact]
    public void Create_KbCardToGame_CreatesLink()
    {
        var pdfId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        var link = EntityLink.Create(
            MeepleEntityType.KbCard, pdfId,
            MeepleEntityType.Game, gameId,
            EntityLinkType.PartOf,
            EntityLinkScope.User, _ownerId);

        link.SourceEntityType.Should().Be(MeepleEntityType.KbCard);
        link.SourceEntityId.Should().Be(pdfId);
        link.TargetEntityType.Should().Be(MeepleEntityType.Game);
        link.TargetEntityId.Should().Be(gameId);
        link.LinkType.Should().Be(EntityLinkType.PartOf);
    }
}
