using Api.BoundedContexts.EntityRelationships.Application.Commands;
using Api.BoundedContexts.EntityRelationships.Domain.Aggregates;
using Api.BoundedContexts.EntityRelationships.Domain.Enums;
using Api.BoundedContexts.EntityRelationships.Domain.Exceptions;
using Api.BoundedContexts.EntityRelationships.Domain.Repositories;
using Api.BoundedContexts.EntityRelationships.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.EntityRelationships;

/// <summary>
/// #4641: a PDF that two users upload is one file and two links. The second user's link was never written: the
/// duplicate check (<c>ExistsAsync</c>) and the unique index <c>uq_entity_links_source_target_type</c> looked at
/// the triple (source, target, type) and not at who owns the link, so the first user's row took the slot and the upload
/// of the second swallowed <c>DuplicateEntityLinkException</c> as "idempotent".
/// </summary>
/// <remarks>
/// <para>
/// Rule (2026-10-26): a <c>User</c>-scope link is personal, so it is unique per owner. A <c>Shared</c>-scope link
/// (curated, imported) belongs to nobody in particular, so it stays unique per triple. A personal link never blocks
/// publishing a shared one; a shared one already stands for everybody, so a user does not need a duplicate of it.
/// </para>
/// <para>
/// On Postgres: the filtered unique indexes are the point, and the in-memory provider does not enforce them.
/// </para>
/// </remarks>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "EntityRelationships")]
[Trait("Issue", "4641")]
public sealed class EntityLinkUniquenessPerOwnerIntegrationTests : IAsyncLifetime
{
    private const string PerTripleIndex = "uq_entity_links_source_target_type";
    private const string PerOwnerIndex = "uq_entity_links_user_source_target_type_owner";

    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext _db = null!;
    private ServiceProvider _serviceProvider = null!;
    private IMediator _mediator = null!;

    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Guid _pdfId = Guid.NewGuid();
    private readonly Guid _userA = Guid.NewGuid();
    private readonly Guid _userB = Guid.NewGuid();
    private readonly Guid _admin = Guid.NewGuid();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public EntityLinkUniquenessPerOwnerIntegrationTests(SharedTestcontainersFixture fixture) => _fixture = fixture;

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_linkowner_{Guid.NewGuid():N}";
        var conn = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(conn);
        services.AddScoped<IEntityLinkRepository, EntityLinkRepository>();
        _serviceProvider = services.BuildServiceProvider();
        _db = _serviceProvider.GetRequiredService<MeepleAiDbContext>();
        _mediator = _serviceProvider.GetRequiredService<IMediator>();
        await _db.Database.MigrateAsync(Ct);
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _serviceProvider.DisposeAsync();
        if (!string.IsNullOrEmpty(_databaseName))
        {
            try { await _fixture.DropIsolatedDatabaseAsync(_databaseName); } catch { /* ignore */ }
        }
    }

    private CreateEntityLinkCommand Create(EntityLinkScope scope, Guid owner) => new(
        MeepleEntityType.Game, _gameId, MeepleEntityType.KbCard, _pdfId,
        EntityLinkType.RelatedTo, scope, owner);

    private EntityLink Row(EntityLinkScope scope, Guid owner) => EntityLink.Create(
        MeepleEntityType.Game, _gameId, MeepleEntityType.KbCard, _pdfId,
        EntityLinkType.RelatedTo, scope, owner);

    private Task<int> LiveRowsAsync() => _db.EntityLinks.CountAsync(
        l => l.SourceEntityId == _gameId && l.TargetEntityId == _pdfId, Ct);

    // ─── Through the command: what the upload flows send ─────────

    /// <summary>The case of the issue: A has the link, B uploads the same file and has none.</summary>
    [Fact]
    public async Task TwoUsers_EachHoldTheirOwnUserLink_ToTheSamePdf()
    {
        await _mediator.Send(Create(EntityLinkScope.User, _userA), Ct);

        var second = await _mediator.Send(Create(EntityLinkScope.User, _userB), Ct);

        second.OwnerUserId.Should().Be(_userB);
        (await LiveRowsAsync()).Should().Be(2, "one file, two links: each user has a row of their own");
    }

    [Fact]
    public async Task TheSameUser_CannotHoldTheSameUserLinkTwice()
    {
        await _mediator.Send(Create(EntityLinkScope.User, _userA), Ct);

        var act = () => _mediator.Send(Create(EntityLinkScope.User, _userA), Ct);

        await act.Should().ThrowAsync<DuplicateEntityLinkException>();
        (await LiveRowsAsync()).Should().Be(1);
    }

    [Fact]
    public async Task TwoSharedLinks_OnTheSameTriple_StillConflict_WhoeverOwnsThem()
    {
        await _mediator.Send(Create(EntityLinkScope.Shared, _admin), Ct);

        var act = () => _mediator.Send(Create(EntityLinkScope.Shared, _userA), Ct);

        await act.Should().ThrowAsync<DuplicateEntityLinkException>();
        (await LiveRowsAsync()).Should().Be(1);
    }

    /// <summary>Publishing a shared link must not depend on whether somebody happens to hold a personal copy.</summary>
    [Fact]
    public async Task APersonalLink_DoesNotBlockPublishingASharedOne()
    {
        await _mediator.Send(Create(EntityLinkScope.User, _userA), Ct);

        var shared = await _mediator.Send(Create(EntityLinkScope.Shared, _admin), Ct);

        shared.Scope.Should().Be(EntityLinkScope.Shared);
        (await LiveRowsAsync()).Should().Be(2);
    }

    /// <summary>The same person can hold a personal link and publish the shared one: each scope has its own index.</summary>
    [Fact]
    public async Task APersonalLink_DoesNotBlockTheSharedOneOfTheSamePerson()
    {
        await _mediator.Send(Create(EntityLinkScope.User, _admin), Ct);

        var shared = await _mediator.Send(Create(EntityLinkScope.Shared, _admin), Ct);

        shared.Scope.Should().Be(EntityLinkScope.Shared);
        (await LiveRowsAsync()).Should().Be(2);
    }

    /// <summary>A shared link already stands for everybody: a personal duplicate of it is not needed, as before.</summary>
    [Fact]
    public async Task ASharedLink_StillBlocksThePersonalDuplicateOfAUser()
    {
        await _mediator.Send(Create(EntityLinkScope.Shared, _admin), Ct);

        var act = () => _mediator.Send(Create(EntityLinkScope.User, _userA), Ct);

        await act.Should().ThrowAsync<DuplicateEntityLinkException>();
        (await LiveRowsAsync()).Should().Be(1);
    }

    // ─── Straight at the table: the indexes, not the handler ─────

    [Fact]
    public async Task TheDatabase_AcceptsTwoUserLinks_OfDifferentOwners()
    {
        _db.EntityLinks.Add(Row(EntityLinkScope.User, _userA));
        _db.EntityLinks.Add(Row(EntityLinkScope.User, _userB));

        var act = () => _db.SaveChangesAsync(Ct);

        await act.Should().NotThrowAsync();
        (await LiveRowsAsync()).Should().Be(2);
    }

    [Fact]
    public async Task TheDatabase_RefusesTwoUserLinks_OfTheSameOwner()
    {
        _db.EntityLinks.Add(Row(EntityLinkScope.User, _userA));
        _db.EntityLinks.Add(Row(EntityLinkScope.User, _userA));

        var act = () => _db.SaveChangesAsync(Ct);

        var thrown = await act.Should().ThrowAsync<DbUpdateException>();
        var pg = thrown.Which.InnerException.Should().BeOfType<PostgresException>().Subject;
        pg.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
        pg.ConstraintName.Should().Be(PerOwnerIndex);
    }

    [Fact]
    public async Task TheDatabase_RefusesTwoSharedLinks_OnTheSameTriple_EvenForDifferentOwners()
    {
        _db.EntityLinks.Add(Row(EntityLinkScope.Shared, _admin));
        _db.EntityLinks.Add(Row(EntityLinkScope.Shared, _userA));

        var act = () => _db.SaveChangesAsync(Ct);

        var thrown = await act.Should().ThrowAsync<DbUpdateException>();
        var pg = thrown.Which.InnerException.Should().BeOfType<PostgresException>().Subject;
        pg.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
        pg.ConstraintName.Should().Be(PerTripleIndex);
    }

    [Fact]
    public async Task ADeletedLink_DoesNotHoldTheSlot()
    {
        var first = Row(EntityLinkScope.User, _userA);
        first.Delete();
        _db.EntityLinks.Add(first);
        await _db.SaveChangesAsync(Ct);
        _db.ChangeTracker.Clear();

        var again = await _mediator.Send(Create(EntityLinkScope.User, _userA), Ct);

        again.Id.Should().NotBe(first.Id);
        (await LiveRowsAsync()).Should().Be(1);
    }
}
