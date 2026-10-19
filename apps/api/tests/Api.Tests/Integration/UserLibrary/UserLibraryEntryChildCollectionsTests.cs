using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.BoundedContexts.UserLibrary.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities.UserLibrary;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using static Api.Tests.Integration.UserLibrary.UserLibraryChildRows;

namespace Api.Tests.Integration.UserLibrary;

/// <summary>
/// Le collezioni figlie di una voce di libreria attraverso <see cref="UserLibraryRepository"/> su
/// Postgres vero (#4505): sessioni, checklist, etichette assegnate. Per ognuna l'aggiunta, la
/// modifica (le etichette non ne hanno: un'assegnazione non cambia) e la rimozione, più un update
/// che non le tocca.
///
/// <para>
/// <c>UpdateAsync</c> persiste un aggregato staccato. Prima riattaccava tutto il grafo con
/// <c>DbSet.Update</c>: un figlio nuovo (con l'id già assegnato) diventava un UPDATE di una riga che
/// non esiste, un figlio tolto non veniva cancellato, e ogni figlio letto veniva riscritto per
/// intero, con le date che la lettura ricostruiva ad "adesso" e il marcatore E2E della sessione,
/// che il dominio non porta, a <c>NULL</c>.
/// </para>
/// <para>
/// Una lettura può non caricare una collezione (<c>GetByUserAndGameAsync</c> carica solo le
/// etichette): per l'aggregato quella collezione è vuota, ma nel database no. Il repository
/// cancella solo ciò che ha letto e il dominio ha tolto, mai ciò che non ha letto.
/// </para>
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "UserLibrary")]
[Trait("Issue", "4505")]
public sealed class UserLibraryEntryChildCollectionsTests(UserLibraryMigratedDatabase database)
    : IClassFixture<UserLibraryMigratedDatabase>
{
    // Date nel passato, alla precisione di Postgres (microsecondi): una data riscritta con
    // "adesso" non può coincidere per caso.
    private static readonly DateTime FirstPlayedAt = Utc("2025-05-01T20:00:00.123456");
    private static readonly DateTime SecondPlayedAt = Utc("2025-05-08T21:30:00.234567");
    private static readonly DateTime CreatedAt = Utc("2025-04-01T08:00:00.345678");
    private static readonly DateTime UpdatedAt = Utc("2025-04-02T09:00:00.456789");
    private static readonly DateTime AssignedAt = Utc("2025-03-03T10:00:00.567891");

    private const string SessionTestRunId = "e2e-children4505-1760000000000";

    /// <summary>
    /// Colonne figlie che il seme non può valorizzare, con il motivo. Ogni altra colonna deve avere
    /// un valore diverso dal default, altrimenti il confronto non la proverebbe.
    /// </summary>
    private static readonly Dictionary<string, string> NotSeedable = new(StringComparer.Ordinal)
    {
        [nameof(UserGameChecklistEntity.IsCompleted)] =
            "booleano: false è un valore legittimo, e il seme ha una voce completata e due no",
        [nameof(UserGameSessionEntity.DidWin)] =
            "booleano: false è un valore legittimo, e il seme ha una partita persa e una vinta",
        [nameof(UserGameChecklistEntity.DisplayOrder)] =
            "la prima voce ha ordine 0, come la numera il dominio; le altre due hanno 1 e 2",
    };

    private static UserLibraryRepository CreateRepository(MeepleAiDbContext context) =>
        new(context, new Mock<IDomainEventCollector>().Object);

    private sealed record Seeded(
        Guid UserId, Guid GameId, Guid EntryId,
        Guid LostSessionId, Guid WonSessionId,
        Guid BoardItemId, Guid CompletedItemId, Guid LastItemId,
        Guid FirstLabelId, Guid SecondLabelId, Guid ThirdLabelId);

    /// <summary>
    /// Una voce con due partite (statistiche coerenti), tre voci di checklist (una completata) e due
    /// etichette assegnate su tre dell'utente. Ogni colonna figlia valorizzata.
    /// </summary>
    private async Task<Seeded> SeedAsync()
    {
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        await using var context = database.CreateDbContext();
        await UserLibraryMigratedDatabase.SeedUserAndGameAsync(context, userId, gameId);

        var entry = new UserLibraryEntryEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SharedGameId = gameId,
            AddedAt = Utc("2025-01-01T09:00:00.000000"),
            CurrentState = (int)GameStateType.Owned,
            StateChangedAt = Utc("2025-01-01T09:00:00.000000"),
            TimesPlayed = 2,
            LastPlayed = SecondPlayedAt,
            WinRate = 50m,
            AvgDuration = 90,
            CompetitiveSessions = 2,
        };
        context.UserLibraryEntries.Add(entry);

        var lost = Session(entry.Id, FirstPlayedAt, 60, didWin: false, "Anna, Bruno", "Prima partita", SessionTestRunId);
        var won = Session(entry.Id, SecondPlayedAt, 120, didWin: true, "Anna, Carla", "Rivincita", "e2e-children4505-1760000000001");
        context.Set<UserGameSessionEntity>().AddRange(lost, won);

        var board = Item(entry.Id, "Disponi il tabellone", 0, isCompleted: false, "Al centro del tavolo");
        var completed = Item(entry.Id, "Mescola le carte", 1, isCompleted: true, "Due mazzi separati");
        var last = Item(entry.Id, "Distribuisci cinque carte", 2, isCompleted: false, "Una a testa, a giro");
        context.Set<UserGameChecklistEntity>().AddRange(board, completed, last);

        var first = new GameLabelEntity { Id = Guid.NewGuid(), Name = "Da rigiocare", Color = "#3366ff", UserId = userId };
        var second = new GameLabelEntity { Id = Guid.NewGuid(), Name = "Con la famiglia", Color = "#ff9900", UserId = userId };
        var third = new GameLabelEntity { Id = Guid.NewGuid(), Name = "Serata lunga", Color = "#00aa55", UserId = userId };
        context.GameLabels.AddRange(first, second, third);
        context.UserGameLabels.AddRange(Assignment(entry.Id, first.Id), Assignment(entry.Id, second.Id));

        await context.SaveChangesAsync();
        return new Seeded(userId, gameId, entry.Id, lost.Id, won.Id, board.Id, completed.Id, last.Id, first.Id, second.Id, third.Id);
    }

    private static UserGameSessionEntity Session(
        Guid entryId, DateTime playedAt, int minutes, bool didWin, string players, string notes, string testRunId) => new()
        {
            Id = Guid.NewGuid(),
            UserLibraryEntryId = entryId,
            PlayedAt = playedAt,
            DurationMinutes = minutes,
            DidWin = didWin,
            Players = players,
            Notes = notes,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt,
            TestRunId = testRunId,
        };

    private static UserGameChecklistEntity Item(
        Guid entryId, string description, int order, bool isCompleted, string additionalInfo) => new()
        {
            Id = Guid.NewGuid(),
            UserLibraryEntryId = entryId,
            Description = description,
            DisplayOrder = order,
            IsCompleted = isCompleted,
            AdditionalInfo = additionalInfo,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt,
        };

    private static UserGameLabelEntity Assignment(Guid entryId, Guid labelId) => new()
    {
        Id = Guid.NewGuid(),
        UserLibraryEntryId = entryId,
        LabelId = labelId,
        AssignedAt = AssignedAt,
    };

    private sealed record ChildRows(
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, object?>> Sessions,
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, object?>> Checklist,
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, object?>> Labels);

    private async Task<ChildRows> ReadChildrenAsync(Guid entryId) => new(
        await ReadAsync<UserGameSessionEntity>(database, entryId),
        await ReadAsync<UserGameChecklistEntity>(database, entryId),
        await ReadAsync<UserGameLabelEntity>(database, entryId));

    /// <summary>
    /// Legge la voce con il caricatore indicato, applica <paramref name="change"/> all'aggregato e lo
    /// salva con <c>UpdateAsync</c>, in un contesto suo, come fa un handler.
    /// </summary>
    private async Task UpdateAsync(Seeded seeded, Loader loader, Action<UserLibraryEntry> change)
    {
        await using var context = database.CreateDbContext();
        var repository = CreateRepository(context);
        var aggregate = loader switch
        {
            Loader.WithStats => await repository.GetUserGameWithStatsAsync(seeded.UserId, seeded.GameId),
            Loader.ByUserAndGame => await repository.GetByUserAndGameAsync(seeded.UserId, seeded.GameId),
            _ => throw new ArgumentOutOfRangeException(nameof(loader)),
        };
        aggregate.Should().NotBeNull();

        change(aggregate!);
        await repository.UpdateAsync(aggregate!);
        await context.SaveChangesAsync();
    }

    public enum Loader
    {
        /// <summary><c>GetUserGameWithStatsAsync</c>: sessioni, checklist ed etichette.</summary>
        WithStats,

        /// <summary><c>GetByUserAndGameAsync</c>: le sole etichette (è la lettura degli handler).</summary>
        ByUserAndGame,
    }

    private static string Lines(IEnumerable<string> problems) => string.Join(Environment.NewLine, problems);

    // ─── Un update che i figli non li tocca ────────────────────────────────────────────────

    [Theory]
    [InlineData(Loader.WithStats)]
    [InlineData(Loader.ByUserAndGame)]
    public async Task UpdateOfTheRootOnly_LeavesEveryChildRowAsStored(Loader loader)
    {
        var seeded = await SeedAsync();
        var before = await ReadChildrenAsync(seeded.EntryId);

        // Il seme deve valorizzare ogni colonna, altrimenti "uguale prima e dopo" non prova nulla.
        var unseeded = before.Sessions.Values.Concat(before.Checklist.Values).Concat(before.Labels.Values)
            .SelectMany(ColumnsLeftAtDefault)
            .Where(column => !NotSeedable.ContainsKey(column))
            .Distinct()
            .ToList();
        unseeded.Should().BeEmpty(
            "ogni colonna figlia va valorizzata nel seme, o aggiunta a NotSeedable con il motivo");

        await UpdateAsync(seeded, loader, entry => entry.UpdateNotes(new LibraryNotes("Una nota, e basta")));

        var after = await ReadChildrenAsync(seeded.EntryId);
        Lines(Differences(before.Sessions, after.Sessions)
                .Concat(Differences(before.Checklist, after.Checklist))
                .Concat(Differences(before.Labels, after.Labels)))
            .Should().BeEmpty(
                "un update della sola riga radice non deve cancellare, riscrivere o aggiungere figli, "
                + "né quelli letti né quelli che la lettura non ha caricato");
    }

    // ─── Sessioni ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(Loader.WithStats)]
    [InlineData(Loader.ByUserAndGame)]
    public async Task NewSession_IsInserted(Loader loader)
    {
        var seeded = await SeedAsync();
        var before = await ReadChildrenAsync(seeded.EntryId);
        var newSessionId = Guid.Empty;

        await UpdateAsync(seeded, loader, entry =>
            newSessionId = entry.RecordGameSession(Utc("2025-06-01T19:00:00.000001"), 45, didWin: null, "Anna", "Partita veloce").Id);

        var after = await ReadChildrenAsync(seeded.EntryId);
        after.Sessions.Keys.Should().BeEquivalentTo([seeded.LostSessionId, seeded.WonSessionId, newSessionId]);
        after.Sessions[newSessionId][nameof(UserGameSessionEntity.DurationMinutes)].Should().Be(45);
        after.Sessions[newSessionId][nameof(UserGameSessionEntity.Notes)].Should().Be("Partita veloce");
        Lines(Differences(before.Sessions, after.Sessions.Where(s => s.Key != newSessionId).ToDictionary(s => s.Key, s => s.Value)))
            .Should().BeEmpty("le partite già salvate restano come sono");
    }

    [Fact]
    public async Task ChangedSession_WritesOnlyWhatChanged()
    {
        var seeded = await SeedAsync();
        var before = await ReadChildrenAsync(seeded.EntryId);
        var beforeChange = DateTime.UtcNow;

        await UpdateAsync(seeded, Loader.WithStats, entry =>
            entry.Sessions.Single(s => s.Id == seeded.LostSessionId).UpdateNotes("Persa per un punto"));

        var after = await ReadChildrenAsync(seeded.EntryId);
        var changed = after.Sessions[seeded.LostSessionId];
        changed[nameof(UserGameSessionEntity.Notes)].Should().Be("Persa per un punto");
        ((DateTime?)changed[nameof(UserGameSessionEntity.UpdatedAt)]).Should().BeOnOrAfter(beforeChange.AddSeconds(-1));

        // Il resto della riga cambiata (marcatore E2E e data di creazione compresi) e le altre righe
        // restano come sono.
        Lines(Differences(before.Sessions, after.Sessions,
                (seeded.LostSessionId, nameof(UserGameSessionEntity.Notes)),
                (seeded.LostSessionId, nameof(UserGameSessionEntity.UpdatedAt))))
            .Should().BeEmpty("si scrive solo ciò che il dominio ha cambiato");
    }

    [Fact]
    public async Task RemovedSession_IsDeletedAndTheStatsRecomputed()
    {
        var seeded = await SeedAsync();
        var before = await ReadChildrenAsync(seeded.EntryId);

        await UpdateAsync(seeded, Loader.WithStats, entry => entry.RemoveGameSession(seeded.WonSessionId));

        var after = await ReadChildrenAsync(seeded.EntryId);
        after.Sessions.Keys.Should().BeEquivalentTo([seeded.LostSessionId]);
        Lines(Differences(
                before.Sessions.Where(s => s.Key != seeded.WonSessionId).ToDictionary(s => s.Key, s => s.Value),
                after.Sessions))
            .Should().BeEmpty();

        await using var verification = database.CreateDbContext();
        var stored = await verification.UserLibraryEntries.AsNoTracking().SingleAsync(e => e.Id == seeded.EntryId);
        stored.TimesPlayed.Should().Be(1);
        stored.LastPlayed.Should().Be(FirstPlayedAt);
        stored.AvgDuration.Should().Be(60);
        stored.WinRate.Should().Be(0m);
    }

    /// <summary>
    /// Una sessione scritta da fuori dell'aggregato dopo la lettura (oggi lo fa il seme dei test
    /// E2E) non va cancellata: la lettura non l'ha vista, quindi il dominio non l'ha tolta. Una
    /// riconciliazione contro le righe del database la cancellerebbe.
    /// </summary>
    [Fact]
    public async Task SessionWrittenAfterTheRead_IsNotDeletedByTheUpdate()
    {
        var seeded = await SeedAsync();

        await using (var context = database.CreateDbContext())
        {
            var repository = CreateRepository(context);
            var aggregate = await repository.GetUserGameWithStatsAsync(seeded.UserId, seeded.GameId);

            await using (var elsewhere = database.CreateDbContext())
            {
                elsewhere.Set<UserGameSessionEntity>().Add(Session(
                    seeded.EntryId, Utc("2025-05-20T20:00:00.000001"), 30, didWin: true, "Bruno", "Dal seme", "e2e-children4505-1760000000002"));
                await elsewhere.SaveChangesAsync();
            }

            aggregate!.RemoveGameSession(seeded.WonSessionId);
            await repository.UpdateAsync(aggregate);
            await context.SaveChangesAsync();
        }

        var after = await ReadChildrenAsync(seeded.EntryId);
        after.Sessions.Should().HaveCount(2, "tolta la partita vinta, restano quella persa e quella scritta da fuori");
        after.Sessions.Keys.Should().Contain(seeded.LostSessionId).And.NotContain(seeded.WonSessionId);
    }

    // ─── Checklist ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task NewChecklistItem_IsInserted()
    {
        var seeded = await SeedAsync();
        var before = await ReadChildrenAsync(seeded.EntryId);
        var newItemId = Guid.Empty;

        await UpdateAsync(seeded, Loader.WithStats, entry =>
            newItemId = entry.AddChecklistItem("Scegli il primo giocatore", "Il più giovane").Id);

        var after = await ReadChildrenAsync(seeded.EntryId);
        after.Checklist.Keys.Should().BeEquivalentTo([seeded.BoardItemId, seeded.CompletedItemId, seeded.LastItemId, newItemId]);
        after.Checklist[newItemId][nameof(UserGameChecklistEntity.Description)].Should().Be("Scegli il primo giocatore");
        after.Checklist[newItemId][nameof(UserGameChecklistEntity.DisplayOrder)].Should().Be(3);
        Lines(Differences(before.Checklist, after.Checklist.Where(c => c.Key != newItemId).ToDictionary(c => c.Key, c => c.Value)))
            .Should().BeEmpty();
    }

    [Fact]
    public async Task ChangedChecklistItem_WritesOnlyWhatChanged()
    {
        var seeded = await SeedAsync();
        var before = await ReadChildrenAsync(seeded.EntryId);

        await UpdateAsync(seeded, Loader.WithStats, entry =>
            entry.Checklist.Single(c => c.Id == seeded.BoardItemId).MarkAsCompleted());

        var after = await ReadChildrenAsync(seeded.EntryId);
        after.Checklist[seeded.BoardItemId][nameof(UserGameChecklistEntity.IsCompleted)].Should().Be(true);

        // La voce già completata è letta completata: la sua data di modifica resta quella salvata.
        Lines(Differences(before.Checklist, after.Checklist,
                (seeded.BoardItemId, nameof(UserGameChecklistEntity.IsCompleted)),
                (seeded.BoardItemId, nameof(UserGameChecklistEntity.UpdatedAt))))
            .Should().BeEmpty("si scrive solo ciò che il dominio ha cambiato");
    }

    [Fact]
    public async Task RemovedChecklistItem_IsDeletedAndTheOthersReordered()
    {
        var seeded = await SeedAsync();

        await UpdateAsync(seeded, Loader.WithStats, entry => entry.RemoveChecklistItem(seeded.BoardItemId));

        var after = await ReadChildrenAsync(seeded.EntryId);
        after.Checklist.Keys.Should().BeEquivalentTo([seeded.CompletedItemId, seeded.LastItemId]);
        after.Checklist[seeded.CompletedItemId][nameof(UserGameChecklistEntity.DisplayOrder)].Should().Be(0);
        after.Checklist[seeded.LastItemId][nameof(UserGameChecklistEntity.DisplayOrder)].Should().Be(1);
        after.Checklist[seeded.CompletedItemId][nameof(UserGameChecklistEntity.IsCompleted)].Should().Be(true);
    }

    // ─── Etichette ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(Loader.WithStats)]
    [InlineData(Loader.ByUserAndGame)]
    public async Task NewLabel_IsInserted(Loader loader)
    {
        var seeded = await SeedAsync();
        var before = await ReadChildrenAsync(seeded.EntryId);

        await UpdateAsync(seeded, loader, entry => entry.AddLabel(seeded.ThirdLabelId));

        var after = await ReadChildrenAsync(seeded.EntryId);
        after.Labels.Values.Select(l => l[nameof(UserGameLabelEntity.LabelId)])
            .Should().BeEquivalentTo([seeded.FirstLabelId, seeded.SecondLabelId, seeded.ThirdLabelId]);
        Lines(Differences(before.Labels, after.Labels.Where(l => before.Labels.ContainsKey(l.Key)).ToDictionary(l => l.Key, l => l.Value)))
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData(Loader.WithStats)]
    [InlineData(Loader.ByUserAndGame)]
    public async Task RemovedLabel_IsDeleted(Loader loader)
    {
        var seeded = await SeedAsync();

        await UpdateAsync(seeded, loader, entry => entry.RemoveLabel(seeded.FirstLabelId));

        var after = await ReadChildrenAsync(seeded.EntryId);
        after.Labels.Values.Select(l => l[nameof(UserGameLabelEntity.LabelId)])
            .Should().BeEquivalentTo([seeded.SecondLabelId]);
    }

    // ─── Rimozione della voce ──────────────────────────────────────────────────────────────

    /// <summary>
    /// <c>DeleteAsync</c> riattacca l'aggregato com'è stato letto: ora che le letture portano le
    /// etichette (e quella con le statistiche tutto), il grafo rimosso le contiene.
    /// </summary>
    [Theory]
    [InlineData(Loader.WithStats)]
    [InlineData(Loader.ByUserAndGame)]
    public async Task RemovingTheEntry_RemovesItsChildRows(Loader loader)
    {
        var seeded = await SeedAsync();

        await using (var context = database.CreateDbContext())
        {
            var repository = CreateRepository(context);
            var aggregate = loader == Loader.WithStats
                ? await repository.GetUserGameWithStatsAsync(seeded.UserId, seeded.GameId)
                : await repository.GetByUserAndGameAsync(seeded.UserId, seeded.GameId);
            aggregate!.PrepareForRemoval();
            await repository.DeleteAsync(aggregate);
            await context.SaveChangesAsync();
        }

        await using var verification = database.CreateDbContext();
        (await verification.UserLibraryEntries.AsNoTracking().AnyAsync(e => e.Id == seeded.EntryId)).Should().BeFalse();
        var after = await ReadChildrenAsync(seeded.EntryId);
        after.Sessions.Should().BeEmpty();
        after.Checklist.Should().BeEmpty();
        after.Labels.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserGameWithStats_CarriesTheAssignedLabels()
    {
        var seeded = await SeedAsync();

        await using var context = database.CreateDbContext();
        var aggregate = await CreateRepository(context).GetUserGameWithStatsAsync(seeded.UserId, seeded.GameId);

        // LabelsCount dello stato in libreria (#4259) si legge da qui.
        aggregate!.Labels.Select(l => l.LabelId).Should().BeEquivalentTo([seeded.FirstLabelId, seeded.SecondLabelId]);
        aggregate.Sessions.Should().HaveCount(2);
        aggregate.Checklist.Should().HaveCount(3);
    }
}
