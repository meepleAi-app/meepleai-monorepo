using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Application.Commands.CustomCover;
using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.EventHandlers;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Events;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.BoundedContexts.UserLibrary.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities.UserLibrary;
using Api.Middleware.Exceptions;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Covers;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.Integration.UserLibrary;

/// <summary>
/// La cover personalizzata lungo i percorsi veri (#4499): handler di upload e rimozione,
/// dettaglio del gioco con la sua cache, pulizia differita alla rimozione del gioco. Repository,
/// unità di lavoro e database sono veri; lo storage oggetti è un doppio con stato, così un test
/// può chiedere quali oggetti esistono dopo un'operazione.
///
/// <para>
/// Questi difetti c'erano prima di #4499, ma non si vedevano: la chiave della cover veniva azzerata
/// a ogni update, quindi nessun percorso arrivava mai a leggerla.
/// </para>
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "UserLibrary")]
[Trait("Issue", "4499")]
public sealed class UserLibraryCustomCoverFlowTests(UserLibraryMigratedDatabase database)
    : IClassFixture<UserLibraryMigratedDatabase>
{
    private static readonly byte[] FirstImage = [0x52, 0x49, 0x46, 0x46, 0x01];
    private static readonly byte[] SecondImage = [0x52, 0x49, 0x46, 0x46, 0x02];

    /// <summary>Storage oggetti finto, con stato: le chiavi scritte e non ancora cancellate.</summary>
    private sealed class FakeObjectStorage
    {
        public Dictionary<string, byte[]> Objects { get; } = new(StringComparer.Ordinal);

        /// <summary>La prossima scrittura fallisce come fallisce lo storage vero: restituisce false.</summary>
        public bool FailNextWrite { get; set; }

        public Mock<IBlobStorageService> Mock { get; } = new();

        public FakeObjectStorage()
        {
            Mock.Setup(s => s.StoreRawKeyAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns((string key, Stream content, string contentType, CancellationToken ct) =>
                {
                    if (FailNextWrite)
                    {
                        FailNextWrite = false;
                        return Task.FromResult(false);
                    }

                    using var copy = new MemoryStream();
                    content.CopyTo(copy);
                    Objects[key] = copy.ToArray();
                    return Task.FromResult(true);
                });
            Mock.Setup(s => s.DeleteRawKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns((string key, CancellationToken ct) => Task.FromResult(Objects.Remove(key)));
            Mock.Setup(s => s.GetPresignedUrlForRawKeyAsync(It.IsAny<string>(), It.IsAny<int?>()))
                .Returns((string key, int? expiry) =>
                    Task.FromResult<string?>(Objects.ContainsKey(key) ? $"https://storage.example.org/{key}" : null));
        }
    }

    private static UserLibraryRepository CreateRepository(MeepleAiDbContext context) =>
        new(context, new Mock<IDomainEventCollector>().Object);

    /// <summary>
    /// Gioco del catalogo nel dominio e nel database con lo stesso id (il dettaglio legge entrambi),
    /// utente, e la sua voce di libreria, senza cover.
    /// </summary>
    private async Task<(Guid UserId, SharedGame Game)> SeedEntryAsync()
    {
        var userId = Guid.NewGuid();
        var game = SharedGame.Create(
            title: "Gioco con cover personalizzata",
            yearPublished: 2019,
            description: "Gioco seminato per i test della cover personalizzata.",
            minPlayers: 1,
            maxPlayers: 5,
            playingTimeMinutes: 70,
            minAge: 10,
            complexityRating: 2.4m,
            averageRating: 8.1m,
            imageUrl: "https://unused.example.test/placeholder.jpg",
            thumbnailUrl: "https://unused.example.test/placeholder.jpg",
            rules: null,
            createdBy: userId,
            bggId: null);

        await using var context = database.CreateDbContext();
        await UserLibraryMigratedDatabase.SeedUserAndGameAsync(context, userId, game.Id);
        context.UserLibraryEntries.Add(new UserLibraryEntryEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SharedGameId = game.Id,
            AddedAt = DateTime.UtcNow,
            CurrentState = (int)GameStateType.Owned,
            StateChangedAt = DateTime.UtcNow,
        });
        await context.SaveChangesAsync();
        return (userId, game);
    }

    private async Task<CustomCoverUploadResult> UploadAsync(
        Guid userId, Guid gameId, byte[] image, FakeObjectStorage storage, HybridCache cache)
    {
        await using var context = database.CreateDbContext();
        var handler = new UploadCustomCoverCommandHandler(
            CreateRepository(context),
            storage.Mock.Object,
            new EfCoreUnitOfWork(context),
            cache,
            NullLogger<UploadCustomCoverCommandHandler>.Instance);

        using var content = new MemoryStream(image);
        return await handler.Handle(
            new UploadCustomCoverCommand(userId, gameId, content, content.Length, "image/webp"),
            TestContext.Current.CancellationToken);
    }

    private async Task RemoveAsync(Guid userId, Guid gameId, FakeObjectStorage storage, HybridCache cache)
    {
        await using var context = database.CreateDbContext();
        var handler = new RemoveCustomCoverCommandHandler(
            CreateRepository(context),
            storage.Mock.Object,
            new EfCoreUnitOfWork(context),
            cache,
            NullLogger<RemoveCustomCoverCommandHandler>.Instance);

        await handler.Handle(new RemoveCustomCoverCommand(userId, gameId), TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// <c>GET /library/games/{gameId}</c>: il dettaglio con il repository vero e la cache passata,
    /// cioè la stessa istanza che in produzione è un singleton condiviso fra le richieste.
    /// </summary>
    private async Task<GameDetailDto> GetDetailAsync(Guid userId, SharedGame game, FakeObjectStorage storage, HybridCache cache)
    {
        await using var context = database.CreateDbContext();

        var sharedGames = new Mock<ISharedGameRepository>();
        sharedGames.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);
        var labels = new Mock<IGameLabelRepository>();
        labels.Setup(r => r.GetLabelsForEntryAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<GameLabel>());
        var threads = new Mock<IChatThreadRepository>();
        threads.Setup(r => r.FindByUserIdAndGameIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ChatThread>());

        var handler = new GetGameDetailQueryHandler(
            CreateRepository(context),
            sharedGames.Object,
            labels.Object,
            threads.Object,
            Mock.Of<IRagAccessService>(),
            context,
            storage.Mock.Object,
            cache,
            NullLogger<GetGameDetailQueryHandler>.Instance);

        return await handler.Handle(new GetGameDetailQuery(userId, game.Id), TestContext.Current.CancellationToken);
    }

    private async Task<string?> ReadStoredKeyAsync(Guid userId, Guid gameId)
    {
        await using var verification = database.CreateDbContext();
        return await verification.UserLibraryEntries.AsNoTracking()
            .Where(e => e.UserId == userId && e.SharedGameId == gameId)
            .Select(e => e.CustomCoverR2Key)
            .SingleAsync();
    }

    [Fact]
    public async Task Upload_StoresTheKey_AndRemoveFindsIt()
    {
        var (userId, game) = await SeedEntryAsync();
        var storage = new FakeObjectStorage();
        var cache = TestDbContextFactory.CreateInMemoryHybridCache();
        var expected = CoverKeyBuilder.ForUser(userId, game.Id);

        var result = await UploadAsync(userId, game.Id, FirstImage, storage, cache);

        result.CoverR2Key.Should().Be(expected.DbKey);
        storage.Objects.Should().ContainKey(expected.PhysicalKey);
        (await ReadStoredKeyAsync(userId, game.Id)).Should().Be(
            expected.DbKey,
            "l'oggetto è stato scritto nello storage: la chiave deve arrivare al database, altrimenti "
            + "la libreria non lo mostra e nessuno può più rimuoverlo");

        // La rimozione legge la chiave dall'aggregato: se la lettura non la mappa, risponde 404.
        var remove = () => RemoveAsync(userId, game.Id, storage, cache);
        await remove.Should().NotThrowAsync<NotFoundException>("la cover appena caricata esiste: la rimozione deve trovarla");

        storage.Objects.Should().NotContainKey(expected.PhysicalKey);
        (await ReadStoredKeyAsync(userId, game.Id)).Should().BeNull("la rimozione azzera la chiave");
    }

    [Fact]
    public async Task Detail_AfterUpload_ShowsTheNewCover()
    {
        var (userId, game) = await SeedEntryAsync();
        var storage = new FakeObjectStorage();
        var cache = TestDbContextFactory.CreateInMemoryHybridCache();

        // Il dettaglio è aperto: è da lì che l'utente carica la cover.
        (await GetDetailAsync(userId, game, storage, cache)).CustomCoverR2Key.Should().BeNull();

        await UploadAsync(userId, game.Id, FirstImage, storage, cache);

        // Il frontend invalida la sua cache e rilegge: il server non deve rispondere con il DTO di prima.
        (await GetDetailAsync(userId, game, storage, cache)).CustomCoverR2Key.Should().Be(
            CoverKeyBuilder.ForUser(userId, game.Id).DbKey,
            "dopo l'upload il dettaglio deve offrire «Rimuovi»: un DTO in cache con la chiave null lo nasconde");
    }

    [Fact]
    public async Task Detail_AfterRemove_NoLongerShowsTheCover()
    {
        var (userId, game) = await SeedEntryAsync();
        var storage = new FakeObjectStorage();
        var cache = TestDbContextFactory.CreateInMemoryHybridCache();

        await UploadAsync(userId, game.Id, FirstImage, storage, cache);
        (await GetDetailAsync(userId, game, storage, cache)).CustomCoverR2Key.Should().NotBeNull();

        await RemoveAsync(userId, game.Id, storage, cache);

        (await GetDetailAsync(userId, game, storage, cache)).CustomCoverR2Key.Should().BeNull(
            "dopo la rimozione il dettaglio non deve più offrire «Rimuovi»: un secondo clic risponderebbe 404");
    }

    [Fact]
    public async Task ReplacingTheCover_WhenTheWriteFails_KeepsThePreviousOne()
    {
        var (userId, game) = await SeedEntryAsync();
        var storage = new FakeObjectStorage();
        var cache = TestDbContextFactory.CreateInMemoryHybridCache();
        var key = CoverKeyBuilder.ForUser(userId, game.Id);

        await UploadAsync(userId, game.Id, FirstImage, storage, cache);

        storage.FailNextWrite = true;
        var replace = () => UploadAsync(userId, game.Id, SecondImage, storage, cache);
        await replace.Should().ThrowAsync<ExternalServiceException>("la scrittura nello storage è fallita");

        // La chiave è deterministica: la cover vecchia e la nuova sono lo stesso oggetto. Cancellarla
        // prima di scrivere la perde quando la scrittura fallisce, mentre il database la indica ancora.
        storage.Objects.Should().ContainKey(key.PhysicalKey, "la cover precedente non va cancellata prima che la nuova sia scritta");
        storage.Objects[key.PhysicalKey].Should().Equal(FirstImage);
        (await ReadStoredKeyAsync(userId, game.Id)).Should().Be(key.DbKey);
    }

    [Fact]
    public async Task ReplacingTheCover_KeepsTheNewOne()
    {
        // GUARDIA: passa anche con il codice di prima. Diventa rossa se la cancellazione della
        // cover vecchia, spostata dopo la scrittura, colpisce l'oggetto appena scritto: con la
        // chiave deterministica vecchia e nuova sono lo stesso oggetto.
        var (userId, game) = await SeedEntryAsync();
        var storage = new FakeObjectStorage();
        var cache = TestDbContextFactory.CreateInMemoryHybridCache();
        var key = CoverKeyBuilder.ForUser(userId, game.Id);

        await UploadAsync(userId, game.Id, FirstImage, storage, cache);
        await UploadAsync(userId, game.Id, SecondImage, storage, cache);

        storage.Objects.Should().ContainKey(key.PhysicalKey);
        storage.Objects[key.PhysicalKey].Should().Equal(SecondImage, "la sostituzione riuscita lascia la cover nuova");
        (await ReadStoredKeyAsync(userId, game.Id)).Should().Be(key.DbKey);
    }

    [Fact]
    public async Task GameRemovedEvent_LeavesACoverThatAnEntryStillUses()
    {
        // Gioco rimosso, aggiunto di nuovo e con una cover nuova prima che l'outbox consegni
        // l'evento della rimozione. La chiave è deterministica, quindi è la stessa.
        var (userId, game) = await SeedEntryAsync();
        var storage = new FakeObjectStorage();
        var cache = TestDbContextFactory.CreateInMemoryHybridCache();
        var key = CoverKeyBuilder.ForUser(userId, game.Id);
        await UploadAsync(userId, game.Id, SecondImage, storage, cache);

        await HandleRemovalEventAsync(new GameRemovedFromLibraryEvent(Guid.NewGuid(), userId, game.Id, key.DbKey), storage);

        storage.Objects.Should().ContainKey(
            key.PhysicalKey,
            "la cover appartiene alla voce di adesso: la pulizia differita della voce rimossa non deve toccarla");
    }

    [Fact]
    public async Task GameRemovedEvent_DeletesACoverNoEntryUses()
    {
        // GUARDIA: passa anche senza la correzione. Diventa rossa se il controllo sulle voci impedisce
        // anche la pulizia legittima, quella di una cover che nessuno usa più.
        var (userId, game) = await SeedEntryAsync();
        var storage = new FakeObjectStorage();
        var key = CoverKeyBuilder.ForUser(userId, game.Id);
        storage.Objects[key.PhysicalKey] = FirstImage;

        await HandleRemovalEventAsync(new GameRemovedFromLibraryEvent(Guid.NewGuid(), userId, game.Id, key.DbKey), storage);

        storage.Objects.Should().NotContainKey(key.PhysicalKey, "nessuna voce usa più quella cover: l'oggetto va cancellato");
    }

    private async Task HandleRemovalEventAsync(GameRemovedFromLibraryEvent removal, FakeObjectStorage storage)
    {
        await using var context = database.CreateDbContext();
        var handler = new GameRemovedFromLibraryCustomCoverHandler(
            storage.Mock.Object,
            CreateRepository(context),
            NullLogger<GameRemovedFromLibraryCustomCoverHandler>.Instance);

        await handler.Handle(removal, TestContext.Current.CancellationToken);
    }
}
