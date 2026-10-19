using System.Globalization;
using Api.BoundedContexts.UserLibrary.Application.Commands.CustomCover;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.BoundedContexts.UserLibrary.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Infrastructure.Entities.UserLibrary;
using Api.Middleware.Exceptions;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Covers;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.Integration.UserLibrary;

/// <summary>
/// Un database migrato per l'intera classe di test (#4499): i test seminano righe con id propri e
/// leggono solo quelle, quindi non si vedono a vicenda. Si paga una migrazione invece di una per test.
/// </summary>
public sealed class UserLibraryRoundTripDatabase(SharedTestcontainersFixture shared) : IAsyncLifetime
{
    private readonly string _databaseName = $"userlib_roundtrip_{Guid.NewGuid():N}";

    public string ConnectionString { get; private set; } = null!;

    public MeepleAiDbContext CreateDbContext() => shared.CreateDbContext(ConnectionString);

    public async ValueTask InitializeAsync()
    {
        ConnectionString = await shared.CreateIsolatedDatabaseAsync(_databaseName);
        await using var context = CreateDbContext();
        await context.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync() => await shared.DropIsolatedDatabaseAsync(_databaseName);
}

/// <summary>
/// Round-trip di una voce di libreria attraverso <see cref="UserLibraryRepository"/> su Postgres
/// vero (#4499).
///
/// <para>
/// <c>UpdateAsync</c> fa <c>MapToPersistence</c> + <c>DbSet.Update</c> su un grafo <b>detached</b>:
/// EF marca come modificata ogni proprietà della classe, quindi una colonna che il mapper non
/// scrive viene salvata a <c>NULL</c>, e una che <c>MapToDomain</c> ricostruisce in modo diverso
/// viene riscritta col valore ricostruito. Il difetto non si vede leggendo una colonna alla volta:
/// serve una riga con <b>tutti</b> i campi valorizzati, la modifica di uno solo, e il confronto di
/// tutti gli altri.
/// </para>
/// <para>
/// L'elenco delle colonne viene dal modello EF, non da una lista scritta qui: una colonna aggiunta
/// domani entra nel confronto da sola, e se il seme non la valorizza il test lo dice.
/// </para>
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "UserLibrary")]
[Trait("Issue", "4499")]
public sealed class UserLibraryEntryRoundTripTests(UserLibraryRoundTripDatabase database)
    : IClassFixture<UserLibraryRoundTripDatabase>
{
    // Date nel passato, alla precisione di Postgres (microsecondi): un valore che l'update
    // riscrivesse con "adesso" non può coincidere per caso.
    private static readonly DateTime AddedAt = Utc("2024-11-02T18:04:05.123456");
    private static readonly DateTime CustomPdfUploadedAt = Utc("2025-01-10T08:15:00.654321");
    private static readonly DateTime OwnershipDeclaredAt = Utc("2025-02-01T12:00:00.000001");
    private static readonly DateTime StateChangedAt = Utc("2025-03-14T09:26:53.589793");
    private static readonly DateTime LastPlayed = Utc("2025-04-20T21:30:00.000000");

    private const string StoredAgentConfig = """{"LlmModel": "modello-ritirato", "Temperature": 0.7}""";

    private const string SeededTestRunId = "e2e-roundtrip4499-1760000000000";

    /// <summary>
    /// Colonne che il seme non può valorizzare, con il motivo. Ogni altra colonna del modello deve
    /// avere un valore diverso dal default, altrimenti il confronto non la proverebbe.
    /// </summary>
    private static readonly Dictionary<string, string> NotSeedable = new(StringComparer.Ordinal)
    {
        [nameof(UserLibraryEntryEntity.PrivateGameId)] =
            "vincolo XOR CK_UserLibraryEntry_GameSource: una voce del catalogo ha SharedGameId, non PrivateGameId",
    };

    private static DateTime Utc(string iso) =>
        DateTime.SpecifyKind(DateTime.Parse(iso, CultureInfo.InvariantCulture), DateTimeKind.Utc);

    private static UserLibraryRepository CreateRepository(MeepleAiDbContext context) =>
        new(context, new Mock<IDomainEventCollector>().Object);

    private sealed record Seeded(Guid UserId, Guid GameId, Guid EntryId, string CoverKey);

    /// <summary>
    /// Utente e gioco del catalogo (FK della voce, pitfall #2620), più un PDF privato per la FK di
    /// <c>PrivatePdfId</c>.
    /// </summary>
    private static async Task<(Guid UserId, Guid GameId, Guid PdfId)> SeedOwnerAndGameAsync(MeepleAiDbContext context)
    {
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();

        context.Users.Add(new UserEntity
        {
            Id = userId,
            Email = $"roundtrip-{userId:N}@meepleai.test",
            Tier = "free",
            Role = "user",
        });
        context.SharedGames.Add(new SharedGameEntity { Id = gameId, Title = "Gioco del round-trip" });
        context.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            SharedGameId = gameId,
            FileName = "regolamento-privato.pdf",
            FilePath = "/uploads/regolamento-privato.pdf",
            UploadedByUserId = userId,
        });
        await context.SaveChangesAsync();
        return (userId, gameId, pdfId);
    }

    /// <summary>Una voce con ogni colonna valorizzata, scritta direttamente: è il dato da preservare.</summary>
    private async Task<Seeded> SeedFullyPopulatedEntryAsync()
    {
        await using var context = database.CreateDbContext();
        var (userId, gameId, pdfId) = await SeedOwnerAndGameAsync(context);

        var coverKey = CoverKeyBuilder.ForUser(userId, gameId).DbKey;
        var entity = new UserLibraryEntryEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SharedGameId = gameId,
            AddedAt = AddedAt,
            Notes = "Note dell'utente, prima della modifica",
            IsFavorite = true,
            CustomPdfUrl = "https://cdn.example.org/regolamenti/regolamento.pdf",
            CustomPdfUploadedAt = CustomPdfUploadedAt,
            CustomPdfFileSizeBytes = 2_048_000,
            CustomPdfOriginalFileName = "regolamento.pdf",
            PrivatePdfId = pdfId,
            OwnershipDeclaredAt = OwnershipDeclaredAt,
            CurrentState = (int)GameStateType.InPrestito,
            StateChangedAt = StateChangedAt,
            StateNotes = "Prestato a Marco",
            TimesPlayed = 7,
            LastPlayed = LastPlayed,
            WinRate = 42.86m,
            AvgDuration = 95,
            CompetitiveSessions = 5,
            CustomCoverR2Key = coverKey,
            TestRunId = SeededTestRunId,
        };
        context.UserLibraryEntries.Add(entity);
        context.Entry(entity).Property(UserLibraryEntryEntity.RetiredCustomAgentConfigJson).CurrentValue = StoredAgentConfig;
        await context.SaveChangesAsync();

        return new Seeded(userId, gameId, entity.Id, coverKey);
    }

    /// <summary>
    /// Ogni colonna mappata della voce, letta da un contesto fresco. Le shadow property
    /// comprese: per questo la lettura è tracciata — esplicitamente, perché il contesto non traccia
    /// per default e un'entità staccata ha le shadow property al default — e passa da
    /// <c>Entry(...).Property</c>.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, object?>> ReadColumnsAsync(Guid entryId)
    {
        await using var context = database.CreateDbContext();
        var entity = await context.UserLibraryEntries.AsTracking().SingleAsync(e => e.Id == entryId);
        var entry = context.Entry(entity);
        return entry.Metadata.GetProperties()
            .ToDictionary(p => p.Name, p => entry.Property(p.Name).CurrentValue, StringComparer.Ordinal);
    }

    private static string Show(object? value) => value switch
    {
        null => "<null>",
        DateTime d => d.ToString("O", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => $"'{value}'",
    };

    [Fact]
    public async Task UpdateOfOneField_LeavesEveryOtherColumnAsStored()
    {
        var seeded = await SeedFullyPopulatedEntryAsync();
        var before = await ReadColumnsAsync(seeded.EntryId);

        // Il seme deve valorizzare ogni colonna, altrimenti "uguale prima e dopo" non prova nulla.
        var unseeded = before
            .Where(c => !NotSeedable.ContainsKey(c.Key) && IsDefault(c.Value))
            .Select(c => c.Key)
            .ToList();
        unseeded.Should().BeEmpty(
            "ogni colonna di user_library_entries va valorizzata nel seme di questo test, o aggiunta a "
            + "NotSeedable con il motivo: una colonna lasciata al default non viene controllata dal round-trip");

        var problems = new List<string>();

        await using (var context = database.CreateDbContext())
        {
            var repository = CreateRepository(context);
            var aggregate = await repository.GetByUserAndGameAsync(seeded.UserId, seeded.GameId);
            aggregate.Should().NotBeNull("il seme è una voce del catalogo, leggibile dal repository");

            // Lato lettura: chi legge e basta (dettaglio del gioco, stato del prestito, rimozione
            // della cover) vede solo l'aggregato, mai la colonna.
            if (aggregate!.CustomCoverR2Key != seeded.CoverKey)
            {
                problems.Add($"lettura CustomCoverR2Key: atteso {Show(seeded.CoverKey)}, l'aggregato porta {Show(aggregate.CustomCoverR2Key)}");
            }

            if (aggregate.CurrentState.ChangedAt != StateChangedAt)
            {
                problems.Add($"lettura CurrentState.ChangedAt: atteso {Show(StateChangedAt)}, l'aggregato porta {Show(aggregate.CurrentState.ChangedAt)}");
            }

            if (aggregate.TestRunId != SeededTestRunId)
            {
                problems.Add($"lettura TestRunId: atteso {Show(SeededTestRunId)}, l'aggregato porta {Show(aggregate.TestRunId)}");
            }

            // Un campo solo.
            aggregate.UpdateNotes(new LibraryNotes("Note dopo la modifica"));
            await repository.UpdateAsync(aggregate);
            await context.SaveChangesAsync();
        }

        var after = await ReadColumnsAsync(seeded.EntryId);

        foreach (var (column, valueBefore) in before)
        {
            if (column is nameof(UserLibraryEntryEntity.Notes) or nameof(UserLibraryEntryEntity.Xmin))
            {
                continue; // la colonna cambiata, e il token che cambia a ogni scrittura
            }

            var valueAfter = after[column];
            if (!Equals(valueBefore, valueAfter))
            {
                problems.Add($"scrittura {column}: prima {Show(valueBefore)}, dopo {Show(valueAfter)}");
            }
        }

        // Una stringa e non la collezione: BeEmpty su una collezione mostra solo il primo elemento,
        // e il punto del test è vedere TUTTI i campi persi in un colpo.
        string.Join(Environment.NewLine, problems).Should().BeEmpty(
            "l'update di Notes non deve toccare altre colonne né leggerle diversamente da come sono "
            + "salvate (#4499)");
        after[nameof(UserLibraryEntryEntity.Notes)].Should().Be("Note dopo la modifica", "la modifica deve arrivare al database");
    }

    [Fact]
    public async Task UploadCustomCover_StoresTheKey_AndRemoveFindsIt()
    {
        Guid userId;
        Guid gameId;
        await using (var context = database.CreateDbContext())
        {
            (userId, gameId, _) = await SeedOwnerAndGameAsync(context);
            context.UserLibraryEntries.Add(new UserLibraryEntryEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                SharedGameId = gameId,
                AddedAt = AddedAt,
                CurrentState = (int)GameStateType.Owned,
                StateChangedAt = StateChangedAt,
            });
            await context.SaveChangesAsync();
        }

        var expectedKey = CoverKeyBuilder.ForUser(userId, gameId);

        var storage = new Mock<IBlobStorageService>();
        storage
            .Setup(s => s.StoreRawKeyAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        storage
            .Setup(s => s.GetPresignedUrlForRawKeyAsync(It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync("https://storage.example.org/presigned");
        storage
            .Setup(s => s.DeleteRawKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Il percorso della rotta POST /library/{gameId}/cover: repository e unità di lavoro veri,
        // solo lo storage oggetti è finto.
        await using (var context = database.CreateDbContext())
        {
            var handler = new UploadCustomCoverCommandHandler(
                CreateRepository(context),
                storage.Object,
                new EfCoreUnitOfWork(context),
                NullLogger<UploadCustomCoverCommandHandler>.Instance);

            using var image = new MemoryStream([0x52, 0x49, 0x46, 0x46]);
            var result = await handler.Handle(
                new UploadCustomCoverCommand(userId, gameId, image, image.Length, "image/webp"),
                TestContext.Current.CancellationToken);

            result.CoverR2Key.Should().Be(expectedKey.DbKey);
        }

        storage.Verify(
            s => s.StoreRawKeyAsync(expectedKey.PhysicalKey, It.IsAny<Stream>(), "image/webp", It.IsAny<CancellationToken>()),
            Times.Once);

        await using (var verification = database.CreateDbContext())
        {
            var stored = await verification.UserLibraryEntries.AsNoTracking()
                .SingleAsync(e => e.UserId == userId && e.SharedGameId == gameId);
            stored.CustomCoverR2Key.Should().Be(
                expectedKey.DbKey,
                "l'oggetto è stato scritto nello storage: la chiave deve arrivare al database, altrimenti "
                + "la libreria non lo mostra e nessuno può più rimuoverlo (#4499)");
        }

        // La rimozione legge la chiave dall'aggregato: se la lettura non la mappa, risponde 404.
        await using (var context = database.CreateDbContext())
        {
            var handler = new RemoveCustomCoverCommandHandler(
                CreateRepository(context),
                storage.Object,
                new EfCoreUnitOfWork(context),
                NullLogger<RemoveCustomCoverCommandHandler>.Instance);

            var remove = () => handler.Handle(
                new RemoveCustomCoverCommand(userId, gameId),
                TestContext.Current.CancellationToken);

            await remove.Should().NotThrowAsync<NotFoundException>(
                "la cover appena caricata esiste: la rimozione deve trovarla");
        }

        storage.Verify(s => s.DeleteRawKeyAsync(expectedKey.PhysicalKey, It.IsAny<CancellationToken>()), Times.Once);

        await using (var verification = database.CreateDbContext())
        {
            var stored = await verification.UserLibraryEntries.AsNoTracking()
                .SingleAsync(e => e.UserId == userId && e.SharedGameId == gameId);
            stored.CustomCoverR2Key.Should().BeNull("la rimozione azzera la chiave");
        }
    }

    [Fact]
    public async Task ChangeState_StillStampsTheTimeOfTheChange()
    {
        var seeded = await SeedFullyPopulatedEntryAsync();

        var beforeChange = DateTime.UtcNow;
        await using (var context = database.CreateDbContext())
        {
            var repository = CreateRepository(context);
            var aggregate = await repository.GetByUserAndGameAsync(seeded.UserId, seeded.GameId);
            aggregate!.MarkAsOwned();
            await repository.UpdateAsync(aggregate);
            await context.SaveChangesAsync();
        }

        await using var verification = database.CreateDbContext();
        var stored = await verification.UserLibraryEntries.AsNoTracking().SingleAsync(e => e.Id == seeded.EntryId);

        // Conservare la data letta vale per l'update che lo stato non lo cambia; un cambio vero
        // resta datato al momento del cambio.
        stored.CurrentState.Should().Be((int)GameStateType.Owned);
        stored.StateChangedAt.Should().NotBeNull();
        stored.StateChangedAt!.Value.Should().BeOnOrAfter(beforeChange.AddSeconds(-1), "un cambio di stato vero porta la data del cambio");
    }

    private static bool IsDefault(object? value) =>
        value is null || (value.GetType().IsValueType && value.Equals(Activator.CreateInstance(value.GetType())));
}
