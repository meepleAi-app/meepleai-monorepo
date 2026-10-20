using System.Reflection;
using Api.BoundedContexts.GameManagement.Application.Commands.PlayRecords;
using Api.BoundedContexts.GameManagement.Application.EventHandlers;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.GameManagement;

/// <summary>
/// I punteggi, i giocatori e le foto di una partita registrata attraverso <c>PlayRecordRepository</c> su
/// Postgres vero (#4569).
///
/// <para>
/// <c>UpdateAsync</c> persiste un aggregato staccato. Dava a ogni punteggio un <c>Guid.NewGuid()</c> a ogni
/// scrittura, e il confronto con le righe salvate cercava quell'id: non lo trovava mai, quindi ogni punteggio
/// era un INSERT. Il database ha un indice unico su (giocatore, dimensione),
/// <c>IX_RecordScores_Player_Dimension_Unique</c>, e l'INSERT di un punteggio già salvato lo viola (23505) e
/// annulla l'intero salvataggio. Nessuna modifica di una partita con almeno un punteggio andava a buon fine:
/// note, condivisione, avvio, chiusura, foto, secondo giocatore, secondo punteggio, cancellazione. Le partite
/// che il gestore di <c>LiveSessionCompletedEvent</c> crea a fine sessione hanno sempre dei punteggi, quindi non
/// si potevano né modificare né cancellare.
/// </para>
/// <para>
/// Nessun test lo vedeva: quelli dei comandi (<c>PlayRecordCommandTests</c>) non modificano mai una partita che
/// ha già un punteggio, e il provider InMemory dei test di unità fa upsert senza guardare l'indice.
/// </para>
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "4569")]
public sealed class PlayRecordChildRowsTests(PlayRecordMigratedDatabase database)
    : IClassFixture<PlayRecordMigratedDatabase>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed record Seeded(Guid UserId, Guid RecordId, Guid PlayerId);

    private async Task<TResult> SendAsync<TResult>(IRequest<TResult> command)
    {
        using var scope = database.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(command, Ct);
    }

    private async Task SendAsync(IRequest command)
    {
        using var scope = database.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IMediator>().Send(command, Ct);
    }

    /// <summary>
    /// Una partita libera con due dimensioni abilitate e un giocatore ospite, creata dai comandi veri; i punteggi
    /// dati, invece, sono righe scritte direttamente: l'arrangiamento non passa dal codice che il test mette alla
    /// prova, e un punteggio seminato non può rompersi sul difetto che il test deve mostrare.
    /// </summary>
    private async Task<Seeded> SeedRecordWithScoresAsync(params (string Dimension, int Value)[] scores)
    {
        var userId = await database.SeedUserAsync();
        var recordId = await SendAsync(new CreatePlayRecordCommand(
            userId,
            GameId: null,
            GameName: "Gioco libero",
            SessionDate: database.Time.UtcNow.AddHours(-3),
            PlayRecordVisibility.Private,
            ScoringDimensions: ["points", "vp"],
            DimensionUnits: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["points"] = "pts", ["vp"] = "vp" }));

        await SendAsync(new AddPlayerToRecordCommand(recordId, userId, null, "Anna"));
        var playerId = await FirstPlayerIdAsync(recordId);

        foreach (var (dimension, value) in scores)
        {
            await SeedScoreAsync(playerId, dimension, value);
        }

        return new Seeded(userId, recordId, playerId);
    }

    private async Task SeedScoreAsync(Guid playerId, string dimension, int value)
    {
        await using var db = database.CreateDbContext();
        db.RecordScores.Add(new RecordScoreEntity
        {
            Id = Guid.NewGuid(),
            RecordPlayerId = playerId,
            Dimension = dimension,
            Value = value,
            Unit = dimension == "points" ? "pts" : "vp",
        });
        await db.SaveChangesAsync(Ct);
    }

    private async Task<Guid> FirstPlayerIdAsync(Guid recordId)
    {
        await using var db = database.CreateDbContext();
        return await db.RecordPlayers.AsNoTracking()
            .Where(p => p.PlayRecordId == recordId)
            .OrderBy(p => p.DisplayName)
            .Select(p => p.Id)
            .FirstAsync(Ct);
    }

    private async Task<List<RecordScoreEntity>> ScoreRowsAsync(Guid recordId)
    {
        await using var db = database.CreateDbContext();
        return await db.RecordScores.IgnoreQueryFilters().AsNoTracking()
            .Where(s => s.RecordPlayer.PlayRecordId == recordId)
            .OrderBy(s => s.RecordPlayerId).ThenBy(s => s.Dimension)
            .ToListAsync(Ct);
    }

    private static IEnumerable<(Guid Id, Guid PlayerId, string Dimension, int Value, string? Unit)> Shape(
        IEnumerable<RecordScoreEntity> rows) =>
        rows.Select(r => (r.Id, r.RecordPlayerId, r.Dimension, r.Value, r.Unit));

    // ---------------------------------------------------------------------------------------------
    // Una modifica qualunque di una partita che ha già un punteggio
    // ---------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("update-notes")]
    [InlineData("share")]
    [InlineData("start")]
    [InlineData("complete")]
    [InlineData("delete")]
    [InlineData("add-player")]
    [InlineData("add-photo")]
    public async Task EditingARecordThatHasAScore_Succeeds_AndKeepsTheScoreRowAsItWas(string edit)
    {
        var seeded = await SeedRecordWithScoresAsync(("points", 42));
        var before = await ScoreRowsAsync(seeded.RecordId);
        before.Should().ContainSingle();

        await ApplyAsync(edit, seeded);

        var after = await ScoreRowsAsync(seeded.RecordId);
        Shape(after).Should().BeEquivalentTo(Shape(before),
            "the edit does not touch the score: same row, same id, same value. Before #4569 the save died on " +
            "IX_RecordScores_Player_Dimension_Unique (23505) because the stored score was inserted again");
    }

    private async Task ApplyAsync(string edit, Seeded record)
    {
        switch (edit)
        {
            case "update-notes":
                await SendAsync(new UpdatePlayRecordCommand(record.RecordId, record.UserId, Notes: "Serata bellissima"));
                break;
            case "share":
                await SendAsync(new GeneratePlayRecordShareTokenCommand(record.RecordId, record.UserId));
                break;
            case "start":
                await SendAsync(new StartPlayRecordCommand(record.RecordId, record.UserId));
                break;
            case "complete":
                await SendAsync(new CompletePlayRecordCommand(record.RecordId, record.UserId, TimeSpan.FromHours(2)));
                break;
            case "delete":
                await SendAsync(new DeletePlayRecordCommand(record.RecordId, record.UserId));
                break;
            case "add-player":
                await SendAsync(new AddPlayerToRecordCommand(record.RecordId, record.UserId, null, "Bruno"));
                break;
            case "add-photo":
                // Il gestore di caricamento ha bisogno di archivio e OCR: qui conta ciò che fa dopo, cioè
                // AddPhoto + UpdateAsync + SaveChanges sulla partita letta.
                await AddPhotoAsync(record.RecordId, record.UserId, "a".PadRight(64, '0'));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(edit), edit, "unknown edit");
        }
    }

    private async Task AddPhotoAsync(Guid recordId, Guid userId, string sha256)
    {
        using var scope = database.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPlayRecordRepository>();
        var record = await repository.GetByIdAsync(recordId, Ct);
        record!.AddPhoto(
            Guid.NewGuid(), $"play-record-photos/{sha256[..8]}.jpg", null, 1024, sha256, null, null, null, userId,
            database.Time);
        await repository.UpdateAsync(record, Ct);
        await scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>().SaveChangesAsync(Ct);
    }

    [Fact]
    public async Task DeletingARecordThatHasScores_HidesIt_AndKeepsItsRows()
    {
        var seeded = await SeedRecordWithScoresAsync(("points", 42), ("vp", 7));

        await SendAsync(new DeletePlayRecordCommand(seeded.RecordId, seeded.UserId));

        await using var db = database.CreateDbContext();
        (await db.PlayRecords.AsNoTracking().AnyAsync(r => r.Id == seeded.RecordId, Ct))
            .Should().BeFalse("the soft-delete query filter hides it");
        (await db.PlayRecords.IgnoreQueryFilters().AsNoTracking().SingleAsync(r => r.Id == seeded.RecordId, Ct))
            .IsDeleted.Should().BeTrue();
        (await ScoreRowsAsync(seeded.RecordId)).Should().HaveCount(2, "a soft delete does not remove the rows");
    }

    // ---------------------------------------------------------------------------------------------
    // Punteggi: aggiunta, modifica
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task ASecondScoreOnAnotherDimension_AddsARow_AndLeavesTheFirstOne()
    {
        var seeded = await SeedRecordWithScoresAsync(("points", 42));
        var first = (await ScoreRowsAsync(seeded.RecordId)).Single();

        await SendAsync(new RecordScoreCommand(seeded.RecordId, seeded.UserId, seeded.PlayerId, "vp", 7, "vp"));

        var rows = await ScoreRowsAsync(seeded.RecordId);
        rows.Select(r => (r.Dimension, r.Value)).Should().BeEquivalentTo(new[] { ("points", 42), ("vp", 7) });
        rows.Single(r => r.Dimension == "points").Id.Should().Be(first.Id, "the stored score is not rewritten");
    }

    [Fact]
    public async Task ANewValueForAnExistingDimension_UpdatesTheRowInPlace()
    {
        var seeded = await SeedRecordWithScoresAsync(("points", 42), ("vp", 7));
        var rowsBefore = await ScoreRowsAsync(seeded.RecordId);

        await SendAsync(new RecordScoreCommand(seeded.RecordId, seeded.UserId, seeded.PlayerId, "points", 50, "pts"));

        var rows = await ScoreRowsAsync(seeded.RecordId);
        rows.Should().HaveCount(2, "the score replaces the previous one for the same dimension: no duplicate");
        var points = rows.Single(r => r.Dimension == "points");
        points.Value.Should().Be(50);
        points.Id.Should().Be(rowsBefore.Single(r => r.Dimension == "points").Id, "the row is updated, not replaced");
        rows.Single(r => r.Dimension == "vp").Should().BeEquivalentTo(rowsBefore.Single(r => r.Dimension == "vp"));
    }

    [Fact]
    public async Task TheSameDimensionInAnotherCase_ReplacesTheScore_InsteadOfAddingOne()
    {
        // Il dominio confronta le dimensioni senza badare alle maiuscole; l'indice unico di Postgres sì.
        var seeded = await SeedRecordWithScoresAsync(("points", 42));
        var stored = (await ScoreRowsAsync(seeded.RecordId)).Single();

        await SendAsync(new RecordScoreCommand(seeded.RecordId, seeded.UserId, seeded.PlayerId, "POINTS", 50, "pts"));

        var rows = await ScoreRowsAsync(seeded.RecordId);
        var row = rows.Should().ContainSingle("the aggregate holds one score for the dimension").Subject;
        row.Value.Should().Be(50);
        row.Dimension.Should().Be("POINTS", "the aggregate carries the dimension as it was last recorded");
        row.Id.Should().Be(stored.Id, "it is the same score: the row is updated, not deleted and inserted again");
    }

    [Fact]
    public async Task TwoPlayersScoredOnTheSameDimension_KeepBothRows_AfterAnEdit()
    {
        var seeded = await SeedRecordWithScoresAsync(("points", 42));
        await SendAsync(new AddPlayerToRecordCommand(seeded.RecordId, seeded.UserId, null, "Bruno"));
        var brunoId = await PlayerIdByNameAsync(seeded.RecordId, "Bruno");
        await SeedScoreAsync(brunoId, "points", 35);
        var before = await ScoreRowsAsync(seeded.RecordId);
        before.Should().HaveCount(2);

        await SendAsync(new UpdatePlayRecordCommand(seeded.RecordId, seeded.UserId, Location: "Casa di Anna"));

        Shape(await ScoreRowsAsync(seeded.RecordId)).Should().BeEquivalentTo(Shape(before),
            "the key of a score is (player, dimension): the same dimension of another player is another score");
    }

    /// <summary>
    /// Nessuna operazione del dominio toglie un punteggio: <c>RecordPlayer.RecordScore</c> sostituisce quello della
    /// stessa dimensione. Il ramo che cancella una riga letta e tolta dal dominio serve a quando ne esisterà una, e
    /// qui lo si prende come lo prenderebbe lei: togliendo il punteggio dalla lista dell'aggregato dopo la lettura.
    /// </summary>
    [Fact]
    public async Task AScoreTheDomainDroppedAfterTheRead_IsDeleted_AndTheOthersStay()
    {
        var seeded = await SeedRecordWithScoresAsync(("points", 42), ("vp", 7));
        var before = await ScoreRowsAsync(seeded.RecordId);

        using (var scope = database.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IPlayRecordRepository>();
            var record = await repository.GetByIdAsync(seeded.RecordId, Ct);
            var scores = (List<RecordScore>)typeof(RecordPlayer)
                .GetField("_scores", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(record!.Players.Single())!;
            scores.RemoveAll(s => string.Equals(s.Dimension, "vp", StringComparison.Ordinal));

            await repository.UpdateAsync(record, Ct);
            await scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>().SaveChangesAsync(Ct);
        }

        Shape(await ScoreRowsAsync(seeded.RecordId)).Should().BeEquivalentTo(
            Shape(before.Where(r => r.Dimension == "points")));
    }

    private async Task<Guid> PlayerIdByNameAsync(Guid recordId, string displayName)
    {
        await using var db = database.CreateDbContext();
        return await db.RecordPlayers.AsNoTracking()
            .Where(p => p.PlayRecordId == recordId && p.DisplayName == displayName)
            .Select(p => p.Id)
            .SingleAsync(Ct);
    }

    // ---------------------------------------------------------------------------------------------
    // Una partita creata a fine sessione, come la crea LiveSessionCompletedEventHandler
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task ARecordGeneratedAtTheEndOfALiveSession_CanBeEdited_Shared_AndDeleted()
    {
        var userId = await database.SeedUserAsync();
        var anna = new CompletedPlayerSnapshot(Guid.NewGuid(), null, "Anna", 9, 1);
        var bruno = new CompletedPlayerSnapshot(Guid.NewGuid(), null, "Bruno", 5, 2);
        var liveEvent = new LiveSessionCompletedEvent(
            sessionId: Guid.NewGuid(),
            completedAt: database.Time.UtcNow,
            totalTurns: 6,
            gameId: null,
            gameName: "Partita dal vivo",
            createdByUserId: userId,
            visibility: PlayRecordVisibility.Private,
            groupId: null,
            sessionDate: database.Time.UtcNow.AddHours(-2),
            startedAt: database.Time.UtcNow.AddHours(-1),
            notes: "Si è chiusa a fine sessione",
            players: [anna, bruno],
            scores:
            [
                new CompletedScoreSnapshot(anna.PlayerId, "points", 4, "pts"),
                new CompletedScoreSnapshot(anna.PlayerId, "points", 5, "pts"),
                new CompletedScoreSnapshot(anna.PlayerId, "vp", 2, "vp"),
                new CompletedScoreSnapshot(bruno.PlayerId, "points", 5, "pts"),
            ]);

        using (var scope = database.CreateScope())
        {
            var handler = new LiveSessionCompletedEventHandler(
                scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>(),
                NullLogger<LiveSessionCompletedEventHandler>.Instance,
                database.Time,
                scope.ServiceProvider.GetRequiredService<IPlayRecordRepository>());
            await handler.Handle(liveEvent, Ct);
        }

        Guid recordId;
        await using (var db = database.CreateDbContext())
        {
            // Il gestore inghiotte gli errori di scrittura: se la partita non c'è, il difetto è prima di questo test.
            recordId = await db.PlayRecords.AsNoTracking()
                .Where(r => r.SourceEventId == liveEvent.EventId)
                .Select(r => r.Id)
                .SingleAsync(Ct);
        }

        var before = await ScoreRowsAsync(recordId);
        before.Select(r => (r.Dimension, r.Value)).Should().BeEquivalentTo(
            new[] { ("points", 9), ("vp", 2), ("points", 5) },
            "the generated record carries the totals per player and dimension");

        await SendAsync(new UpdatePlayRecordCommand(recordId, userId, Notes: "Corretta dopo la partita"));
        await SendAsync(new GeneratePlayRecordShareTokenCommand(recordId, userId));
        await SendAsync(new DeletePlayRecordCommand(recordId, userId));

        Shape(await ScoreRowsAsync(recordId)).Should().BeEquivalentTo(Shape(before));
        await using var after = database.CreateDbContext();
        (await after.PlayRecords.IgnoreQueryFilters().AsNoTracking().SingleAsync(r => r.Id == recordId, Ct))
            .IsDeleted.Should().BeTrue();
    }

    // ---------------------------------------------------------------------------------------------
    // Concorrenza (ADR-060): la riga padre porta xmin, e i punteggi non ne hanno uno loro
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// I punteggi non hanno un token di concorrenza. Una scrittura da una lettura superata fallisce per intero
    /// perché l'UPDATE della partita, nella stessa transazione, controlla <c>xmin</c>. Quell'UPDATE c'è sempre
    /// perché <c>Update()</c> su un grafo staccato marca ogni colonna della radice: se un giorno la radice si
    /// salvasse solo quando cambia, i punteggi resterebbero senza protezione.
    /// </summary>
    [Fact]
    public async Task ConcurrentScoreWrites_TheSecondSaveFailsOnTheRecordXmin()
    {
        var seeded = await SeedRecordWithScoresAsync(("points", 42));

        using var scopeA = database.CreateScope();
        using var scopeB = database.CreateScope();
        var repositoryA = scopeA.ServiceProvider.GetRequiredService<IPlayRecordRepository>();
        var repositoryB = scopeB.ServiceProvider.GetRequiredService<IPlayRecordRepository>();
        var recordA = await repositoryA.GetByIdAsync(seeded.RecordId, Ct);
        var recordB = await repositoryB.GetByIdAsync(seeded.RecordId, Ct);

        recordA!.RecordScore(seeded.PlayerId, new RecordScore("vp", 3, "vp"), database.Time);
        await repositoryA.UpdateAsync(recordA, Ct);
        await scopeA.ServiceProvider.GetRequiredService<MeepleAiDbContext>().SaveChangesAsync(Ct);

        // B ha letto prima che A salvasse, e corregge un punteggio che c'era già: è un UPDATE di una riga che
        // esiste, nessun indice unico lo ferma. Lo ferma solo xmin della partita.
        recordB!.RecordScore(seeded.PlayerId, new RecordScore("points", 43, "pts"), database.Time);
        await repositoryB.UpdateAsync(recordB, Ct);
        var saveB = () => scopeB.ServiceProvider.GetRequiredService<MeepleAiDbContext>().SaveChangesAsync(Ct);

        await saveB.Should().ThrowAsync<DbUpdateConcurrencyException>(
            "B read the record before A saved it: its write must not overwrite A's");
        var rows = await ScoreRowsAsync(seeded.RecordId);
        rows.Select(r => (r.Dimension, r.Value)).Should().BeEquivalentTo(new[] { ("points", 42), ("vp", 3) },
            "B's correction was rolled back, A's score stays");
    }

    [Fact]
    public async Task ConcurrentScoreWritesOnADimensionNobodyHadYet_TheSecondSaveFails_AndKeepsTheFirst()
    {
        var seeded = await SeedRecordWithScoresAsync(("points", 42));

        using var scopeA = database.CreateScope();
        using var scopeB = database.CreateScope();
        var repositoryA = scopeA.ServiceProvider.GetRequiredService<IPlayRecordRepository>();
        var repositoryB = scopeB.ServiceProvider.GetRequiredService<IPlayRecordRepository>();
        var recordA = await repositoryA.GetByIdAsync(seeded.RecordId, Ct);
        var recordB = await repositoryB.GetByIdAsync(seeded.RecordId, Ct);

        recordA!.RecordScore(seeded.PlayerId, new RecordScore("vp", 3, "vp"), database.Time);
        await repositoryA.UpdateAsync(recordA, Ct);
        await scopeA.ServiceProvider.GetRequiredService<MeepleAiDbContext>().SaveChangesAsync(Ct);

        recordB!.RecordScore(seeded.PlayerId, new RecordScore("vp", 4, "vp"), database.Time);
        await repositoryB.UpdateAsync(recordB, Ct);
        var saveB = () => scopeB.ServiceProvider.GetRequiredService<MeepleAiDbContext>().SaveChangesAsync(Ct);

        // Qui B inserisce lo stesso (giocatore, dimensione) di A. Osservato: risponde l'indice unico, non xmin.
        // EF manda l'INSERT del punteggio prima dell'UPDATE della partita, quindi B riceve un DbUpdateException
        // con dentro la PostgresException 23505, e NON un DbUpdateConcurrencyException (409): all'API è un 500.
        // L'ordine è quello con cui EF emette i comandi, non una garanzia. Il tipo esatto è nell'asserzione
        // perché la sottoclasse non la soddisfa: se l'ordine cambiasse, B riceverebbe la concorrenza e il test
        // dirà che il caso è diventato un 409.
        var thrown = await saveB.Should().ThrowExactlyAsync<DbUpdateException>();
        var postgres = thrown.Which.InnerException.Should().BeOfType<PostgresException>().Subject;
        postgres.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
        postgres.ConstraintName.Should().Be("IX_RecordScores_Player_Dimension_Unique");
        var rows = await ScoreRowsAsync(seeded.RecordId);
        rows.Should().HaveCount(2);
        rows.Single(r => r.Dimension == "vp").Value.Should().Be(3);
    }

    // ---------------------------------------------------------------------------------------------
    // Il repository: che cosa tocca il change tracker
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task UpdateAsyncWithNoChange_LeavesEveryScoreRowUnchangedInTheTracker()
    {
        var seeded = await SeedRecordWithScoresAsync(("points", 42), ("vp", 7));

        using var scope = database.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPlayRecordRepository>();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var record = await repository.GetByIdAsync(seeded.RecordId, Ct);

        await repository.UpdateAsync(record!, Ct);

        var entries = db.ChangeTracker.Entries<RecordScoreEntity>().ToList();
        entries.Should().HaveCount(2);
        entries.Should().OnlyContain(e => e.State == EntityState.Unchanged,
            "a stored score that the domain did not change is not written: no INSERT, no UPDATE");
    }

    [Fact]
    public async Task UpdateAsyncWithAChangedValue_WritesOnlyThatScoreRow_AndOnlyItsValue()
    {
        var seeded = await SeedRecordWithScoresAsync(("points", 42), ("vp", 7));

        using var scope = database.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPlayRecordRepository>();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var record = await repository.GetByIdAsync(seeded.RecordId, Ct);
        record!.RecordScore(seeded.PlayerId, new RecordScore("points", 50, "pts"), database.Time);

        await repository.UpdateAsync(record, Ct);

        var modified = db.ChangeTracker.Entries<RecordScoreEntity>().Where(e => e.State != EntityState.Unchanged).ToList();
        var entry = modified.Should().ContainSingle().Subject;
        entry.State.Should().Be(EntityState.Modified);
        entry.Entity.Dimension.Should().Be("points");
        entry.Properties.Where(p => p.IsModified).Select(p => p.Metadata.Name).Should().BeEquivalentTo(new[] { "Value" });
    }

    [Fact]
    public async Task DeleteAsync_OfARecordThatHasScores_RemovesTheRecordAndEveryRowUnderIt()
    {
        // La foto prima dei punteggi: aggiungerla passa da UpdateAsync, e qui conta solo DeleteAsync.
        var seeded = await SeedRecordWithScoresAsync();
        await AddPhotoAsync(seeded.RecordId, seeded.UserId, "b".PadRight(64, '0'));
        await SeedScoreAsync(seeded.PlayerId, "points", 42);
        await SeedScoreAsync(seeded.PlayerId, "vp", 7);

        using (var scope = database.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IPlayRecordRepository>();
            var record = await repository.GetByIdAsync(seeded.RecordId, Ct);
            await repository.DeleteAsync(record!, Ct);
            await scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>().SaveChangesAsync(Ct);
        }

        await using var db = database.CreateDbContext();
        (await db.PlayRecords.IgnoreQueryFilters().AnyAsync(r => r.Id == seeded.RecordId, Ct)).Should().BeFalse();
        (await db.RecordPlayers.AnyAsync(p => p.PlayRecordId == seeded.RecordId, Ct)).Should().BeFalse();
        (await ScoreRowsAsync(seeded.RecordId)).Should().BeEmpty();
        (await db.PlayRecordPhotos.AnyAsync(p => p.PlayRecordId == seeded.RecordId, Ct)).Should().BeFalse();
    }

    // ---------------------------------------------------------------------------------------------
    // Giocatori e foto: gli altri figli della partita. Hanno id stabili (il dominio li porta), quindi il
    // difetto dei punteggi non li riguarda: questi test lo fissano.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task APlayerAddedToARecordThatHasPlayers_KeepsTheOnesItHad()
    {
        var userId = await database.SeedUserAsync();
        var recordId = await SendAsync(new CreatePlayRecordCommand(
            userId, null, "Gioco libero", database.Time.UtcNow.AddHours(-3), PlayRecordVisibility.Private));
        await SendAsync(new AddPlayerToRecordCommand(recordId, userId, null, "Anna"));
        var annaId = await PlayerIdByNameAsync(recordId, "Anna");

        await SendAsync(new AddPlayerToRecordCommand(recordId, userId, null, "Bruno"));

        await using var db = database.CreateDbContext();
        var players = await db.RecordPlayers.AsNoTracking().Where(p => p.PlayRecordId == recordId).ToListAsync(Ct);
        players.Select(p => p.DisplayName).Should().BeEquivalentTo(new[] { "Anna", "Bruno" });
        players.Single(p => p.DisplayName == "Anna").Id.Should().Be(annaId);
    }

    [Fact]
    public async Task APhotoAddedToARecordThatHasAPhoto_KeepsTheOneItHad()
    {
        var userId = await database.SeedUserAsync();
        var recordId = await SendAsync(new CreatePlayRecordCommand(
            userId, null, "Gioco libero", database.Time.UtcNow.AddHours(-3), PlayRecordVisibility.Private));
        await AddPhotoAsync(recordId, userId, "c".PadRight(64, '0'));
        List<PlayRecordPhotoEntity> before;
        await using (var db = database.CreateDbContext())
        {
            before = await db.PlayRecordPhotos.AsNoTracking().Where(p => p.PlayRecordId == recordId).ToListAsync(Ct);
        }

        await AddPhotoAsync(recordId, userId, "d".PadRight(64, '0'));

        await using var after = database.CreateDbContext();
        var photos = await after.PlayRecordPhotos.AsNoTracking().Where(p => p.PlayRecordId == recordId).ToListAsync(Ct);
        photos.Should().HaveCount(2);
        photos.Single(p => p.Id == before.Single().Id).Should().BeEquivalentTo(before.Single());
    }
}
