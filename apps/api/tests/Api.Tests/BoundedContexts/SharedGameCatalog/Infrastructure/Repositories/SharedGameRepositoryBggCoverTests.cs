using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;

/// <summary>
/// #3590 Slice B — la cover BGG ri-ospitata (layer L2.5) deve sopravvivere a un
/// load-modify-save dell'aggregato.
/// <para>
/// Regressione chiusa qui: <c>SharedGameRepository.Update()</c> fa
/// <c>MapToEntity(aggregato)</c> + <c>DbContext.Update(entity)</c> su grafo DETACHED, quindi
/// marca ogni colonna come Modified. Il mapper non scriveva <c>BggCoverR2Key</c> (né
/// <c>MapToDomain</c> lo leggeva), per cui ogni salvataggio emetteva
/// <c>SET bgg_cover_r2_key = NULL</c> — perdita silenziosa della cover.
/// </para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class SharedGameRepositoryBggCoverTests
{
    private const string BggKey = "bgg-covers/13/cover";

    private static SharedGameRepository Repo(Api.Infrastructure.MeepleAiDbContext db) =>
        new(db, Mock.Of<IDomainEventCollector>());

    private static SharedGameEntity NewEntity(Guid id, string? bggCoverR2Key) => new()
    {
        Id = id,
        Title = "Catan",
        Description = "desc",
        Status = 2, // Published
        CreatedBy = Guid.NewGuid(),
        CreatedAt = DateTime.UtcNow,
        ImageUrl = string.Empty,
        ThumbnailUrl = string.Empty,
        BggCoverR2Key = bggCoverR2Key,
    };

    [Fact]
    public async Task Update_PreservesBggCoverR2Key()
    {
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var id = Guid.NewGuid();
        db.SharedGames.Add(NewEntity(id, BggKey));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var repo = Repo(db);
        var game = await repo.GetByIdAsync(id, CancellationToken.None);
        game.Should().NotBeNull();
        repo.Update(game!);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var after = await db.SharedGames.AsNoTracking().FirstAsync(g => g.Id == id);
        after.BggCoverR2Key.Should().Be(BggKey,
            "un update dell'aggregato non deve cancellare la cover BGG ri-ospitata");
    }

    [Fact]
    public async Task GetByIdAsync_HydratesBggCoverR2KeyOntoTheAggregate()
    {
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var id = Guid.NewGuid();
        db.SharedGames.Add(NewEntity(id, BggKey));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var game = await Repo(db).GetByIdAsync(id, CancellationToken.None);

        game!.BggCoverR2Key.Should().Be(BggKey,
            "senza la lettura nel mapper l'aggregato non può preservare ciò che non conosce");
    }

    [Fact]
    public async Task SetBggCover_PersistsThroughUpdate()
    {
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var id = Guid.NewGuid();
        db.SharedGames.Add(NewEntity(id, bggCoverR2Key: null));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var repo = Repo(db);
        var game = await repo.GetByIdAsync(id, CancellationToken.None);
        game!.SetBggCover(BggKey);
        repo.Update(game);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var after = await db.SharedGames.AsNoTracking().FirstAsync(g => g.Id == id);
        after.BggCoverR2Key.Should().Be(BggKey);
    }

    [Fact]
    public void SetBggCover_EmptyKey_Throws()
    {
        var game = SharedGame.Create(
            "Catan", 1995, "desc", 3, 4, 90, 10, 2.5m, 7.8m,
            "https://example.com/c.jpg", "https://example.com/c-thumb.jpg", null, Guid.NewGuid());

        var act = () => game.SetBggCover("  ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SetBggCover_SameValue_IsIdempotent()
    {
        var game = SharedGame.Create(
            "Catan", 1995, "desc", 3, 4, 90, 10, 2.5m, 7.8m,
            "https://example.com/c.jpg", "https://example.com/c-thumb.jpg", null, Guid.NewGuid());

        game.SetBggCover(BggKey);
        game.SetBggCover(BggKey);

        game.BggCoverR2Key.Should().Be(BggKey);
    }
}
