using Api.BoundedContexts.EntityRelationships.Domain.Aggregates;
using Api.BoundedContexts.EntityRelationships.Domain.Enums;
using Api.BoundedContexts.EntityRelationships.Domain.Events;
using Api.Tests.Constants;
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

        Assert.NotEqual(Guid.Empty, link.Id);
        Assert.Equal(MeepleEntityType.Game, link.SourceEntityType);
        Assert.Equal(_sourceId, link.SourceEntityId);
        Assert.Equal(MeepleEntityType.Game, link.TargetEntityType);
        Assert.Equal(_targetId, link.TargetEntityId);
        Assert.Equal(EntityLinkType.ExpansionOf, link.LinkType);
        Assert.Equal(_ownerId, link.OwnerUserId);
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

        Assert.True(link.IsAdminApproved);
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

        Assert.False(link.IsAdminApproved);
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

        Assert.True(link.IsAdminApproved);
        Assert.True(link.IsBggImported);
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
        Assert.Single(events);
        var evt = Assert.IsType<EntityLinkCreatedEvent>(events[0]);
        Assert.Equal(link.Id, evt.EntityLinkId);
        Assert.Equal(EntityLinkType.CollaboratesWith, evt.LinkType);
        Assert.Equal(_ownerId, evt.OwnerUserId);
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

        Assert.Empty(events);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Create() validation — self-reference
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_SameSourceAndTarget_ThrowsArgumentException()
    {
        var sameId = Guid.NewGuid();

        var ex = Assert.Throws<ArgumentException>(() => EntityLink.Create(
            MeepleEntityType.Game, sameId,
            MeepleEntityType.Game, sameId,
            EntityLinkType.RelatedTo,
            EntityLinkScope.User, _ownerId));

        Assert.Contains("same", ex.Message, StringComparison.OrdinalIgnoreCase);
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

        Assert.NotNull(link);
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

        Assert.Equal(expected, link.IsBidirectional);
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

        Assert.False(link.IsAdminApproved);
        link.Approve();
        Assert.True(link.IsAdminApproved);
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
        Assert.Single(events);
        var evt = Assert.IsType<EntityLinkDeletedEvent>(events[0]);
        Assert.Equal(link.Id, evt.EntityLinkId);
        Assert.Equal(adminId, evt.DeletedByUserId);
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

        Assert.Equal(metadata, link.Metadata);
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

        Assert.Equal(newMetadata, link.Metadata);
    }
}
