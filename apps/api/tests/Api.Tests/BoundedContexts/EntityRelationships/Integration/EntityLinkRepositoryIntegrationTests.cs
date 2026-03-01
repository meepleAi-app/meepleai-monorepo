using Api.BoundedContexts.EntityRelationships.Domain.Aggregates;
using Api.BoundedContexts.EntityRelationships.Domain.Enums;
using Api.BoundedContexts.EntityRelationships.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.EntityRelationships.Integration;

/// <summary>
/// Integration tests for EntityLinkRepository using InMemory database.
/// Issue #5137: Validates repository queries used by user endpoints.
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "EntityRelationships")]
public sealed class EntityLinkRepositoryIntegrationTests : IAsyncLifetime
{
    private MeepleAiDbContext _dbContext = null!;
    private EntityLinkRepository _repository = null!;

    private readonly Guid _userId1 = Guid.NewGuid();
    private readonly Guid _userId2 = Guid.NewGuid();
    private readonly Guid _gameId1 = Guid.NewGuid();
    private readonly Guid _gameId2 = Guid.NewGuid();
    private readonly Guid _gameId3 = Guid.NewGuid();

    public async ValueTask InitializeAsync()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext($"EntityLinkRepoTest_{Guid.NewGuid()}");
        _repository = new EntityLinkRepository(_dbContext);
        await SeedTestDataAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.Database.EnsureDeletedAsync();
        await _dbContext.DisposeAsync();
    }

    private async Task SeedTestDataAsync()
    {
        // Game1 --expansion_of--> Game2 (user1, User scope, unidirectional)
        var link1 = EntityLink.Create(
            MeepleEntityType.Game, _gameId1,
            MeepleEntityType.Game, _gameId2,
            EntityLinkType.ExpansionOf,
            EntityLinkScope.User, _userId1);

        // Game1 --related_to--> Game3 (user1, User scope, bidirectional)
        var link2 = EntityLink.Create(
            MeepleEntityType.Game, _gameId1,
            MeepleEntityType.Game, _gameId3,
            EntityLinkType.RelatedTo,
            EntityLinkScope.User, _userId1);

        // Game2 --sequel_of--> Game3 (user2, Shared scope)
        var link3 = EntityLink.Create(
            MeepleEntityType.Game, _gameId2,
            MeepleEntityType.Game, _gameId3,
            EntityLinkType.SequelOf,
            EntityLinkScope.Shared, _userId2);

        await _dbContext.EntityLinks.AddRangeAsync(link1, link2, link3);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    // ── GetForEntityAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetForEntityAsync_AsSource_ReturnsLinks()
    {
        var results = await _repository.GetForEntityAsync(
            MeepleEntityType.Game, _gameId1, cancellationToken: TestContext.Current.CancellationToken);

        results.Should().HaveCount(2);
        results.Should().AllSatisfy(l => l.SourceEntityId.Should().Be(_gameId1));
    }

    [Fact]
    public async Task GetForEntityAsync_AsBidirectionalTarget_ReturnsLink()
    {
        // Game3 is a target of RelatedTo (bidirectional) from Game1
        var results = await _repository.GetForEntityAsync(
            MeepleEntityType.Game, _gameId3, cancellationToken: TestContext.Current.CancellationToken);

        // Game3 appears as target of: link2 (RelatedTo=bidirectional, User scope) + link3 (SequelOf=unidirectional, Shared)
        results.Should().Contain(l => l.IsBidirectional && l.TargetEntityId == _gameId3);
    }

    [Fact]
    public async Task GetForEntityAsync_FilterByScope_ReturnsOnlyScopedLinks()
    {
        var results = await _repository.GetForEntityAsync(
            MeepleEntityType.Game, _gameId1,
            scope: EntityLinkScope.User,
            cancellationToken: TestContext.Current.CancellationToken);

        results.Should().HaveCount(2);
        results.Should().AllSatisfy(l => l.Scope.Should().Be(EntityLinkScope.User));
    }

    [Fact]
    public async Task GetForEntityAsync_FilterByLinkType_ReturnsFilteredLinks()
    {
        var results = await _repository.GetForEntityAsync(
            MeepleEntityType.Game, _gameId1,
            linkType: EntityLinkType.ExpansionOf,
            cancellationToken: TestContext.Current.CancellationToken);

        results.Should().HaveCount(1);
        results[0].LinkType.Should().Be(EntityLinkType.ExpansionOf);
    }

    [Fact]
    public async Task GetForEntityAsync_UnknownEntity_ReturnsEmpty()
    {
        var results = await _repository.GetForEntityAsync(
            MeepleEntityType.Game, Guid.NewGuid(), cancellationToken: TestContext.Current.CancellationToken);

        results.Should().BeEmpty();
    }

    // ── GetCountForEntityAsync ───────────────────────────────────────────────

    [Fact]
    public async Task GetCountForEntityAsync_AsSource_CountsLinks()
    {
        var count = await _repository.GetCountForEntityAsync(
            MeepleEntityType.Game, _gameId1, TestContext.Current.CancellationToken);

        count.Should().Be(2);
    }

    [Fact]
    public async Task GetCountForEntityAsync_AsBidirectionalTarget_IncludesBidirectional()
    {
        // Game3 is target of RelatedTo (bidirectional) from Game1 — counts 1 bidirectional + link3 source Game2
        var count = await _repository.GetCountForEntityAsync(
            MeepleEntityType.Game, _gameId3, TestContext.Current.CancellationToken);

        // bidirectional RelatedTo(link2) + unidirectional SequelOf where Game2 is source (not Game3)
        // So Game3: link2 (bidirectional target ✅) + link3 (source=Game2, not Game3 ✅ but target=Game3 and unidirectional ❌)
        count.Should().Be(1); // only the bidirectional RelatedTo link
    }

    [Fact]
    public async Task GetCountForEntityAsync_UnknownEntity_ReturnsZero()
    {
        var count = await _repository.GetCountForEntityAsync(
            MeepleEntityType.Game, Guid.NewGuid(), TestContext.Current.CancellationToken);

        count.Should().Be(0);
    }

    // ── AddAsync + GetByIdAsync ──────────────────────────────────────────────

    [Fact]
    public async Task AddAsync_ThenGetById_ReturnsLink()
    {
        var link = EntityLink.Create(
            MeepleEntityType.Game, _gameId1,
            MeepleEntityType.Agent, Guid.NewGuid(),
            EntityLinkType.CollaboratesWith,
            EntityLinkScope.User, _userId1);

        await _repository.AddAsync(link, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var found = await _repository.GetByIdAsync(link.Id, TestContext.Current.CancellationToken);

        found.Should().NotBeNull();
        found!.Id.Should().Be(link.Id);
        found.LinkType.Should().Be(EntityLinkType.CollaboratesWith);
        found.IsBidirectional.Should().BeTrue();
    }

    // ── ExistsAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ExistsAsync_ExistingLink_ReturnsTrue()
    {
        var exists = await _repository.ExistsAsync(
            MeepleEntityType.Game, _gameId1,
            MeepleEntityType.Game, _gameId2,
            EntityLinkType.ExpansionOf,
            TestContext.Current.CancellationToken);

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_NonExistentLink_ReturnsFalse()
    {
        var exists = await _repository.ExistsAsync(
            MeepleEntityType.Game, _gameId1,
            MeepleEntityType.Game, _gameId2,
            EntityLinkType.SequelOf, // different link type
            TestContext.Current.CancellationToken);

        exists.Should().BeFalse();
    }

    // ── Soft-delete via Delete() ─────────────────────────────────────────────

    [Fact]
    public async Task SoftDelete_ExistingLink_ExcludedFromSubsequentQueries()
    {
        var links = await _repository.GetForEntityAsync(
            MeepleEntityType.Game, _gameId1, cancellationToken: TestContext.Current.CancellationToken);
        links.Should().HaveCount(2);

        // Soft-delete the first link via domain method
        var link = links[0];
        link.Delete(_userId1);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // HasQueryFilter(x => !x.IsDeleted) should exclude it
        var afterDelete = await _repository.GetForEntityAsync(
            MeepleEntityType.Game, _gameId1, cancellationToken: TestContext.Current.CancellationToken);

        afterDelete.Should().HaveCount(1);
    }

    [Fact]
    public async Task SoftDelete_Link_CountDecreases()
    {
        var links = await _repository.GetForEntityAsync(
            MeepleEntityType.Game, _gameId1, cancellationToken: TestContext.Current.CancellationToken);
        var link = links[0];
        link.Delete(_userId1);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var count = await _repository.GetCountForEntityAsync(
            MeepleEntityType.Game, _gameId1, TestContext.Current.CancellationToken);

        count.Should().Be(1);
    }
}
