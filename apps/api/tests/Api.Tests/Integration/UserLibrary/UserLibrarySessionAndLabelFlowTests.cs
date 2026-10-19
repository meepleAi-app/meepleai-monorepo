using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Application.Commands.Labels;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.BoundedContexts.UserLibrary.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities.UserLibrary;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using static Api.Tests.Integration.UserLibrary.UserLibraryChildRows;

namespace Api.Tests.Integration.UserLibrary;

/// <summary>
/// Registrare una partita e assegnare o togliere un'etichetta lungo gli handler veri (#4505):
/// repository, unità di lavoro e Postgres veri.
///
/// <para>
/// I tre handler leggono la voce con <c>GetByUserAndGameAsync</c>, la cambiano e chiamano
/// <c>UpdateAsync</c>, che persisteva il grafo staccato con <c>DbSet.Update</c>. Una sessione
/// nuova ha già l'id, quindi finiva <c>Modified</c>: un UPDATE di una riga che non esiste, cioè
/// <c>DbUpdateConcurrencyException</c> e un 500. Le etichette il mapper non le scriveva proprio:
/// l'assegnazione rispondeva 200 senza salvare nulla, e la rimozione 404 perché la lettura non
/// le caricava. Su InMemory non si vede: lì <c>Update</c> fa un upsert.
/// </para>
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "UserLibrary")]
[Trait("Issue", "4505")]
public sealed class UserLibrarySessionAndLabelFlowTests(UserLibraryMigratedDatabase database)
    : IClassFixture<UserLibraryMigratedDatabase>
{
    private static readonly DateTime EarlierPlayedAt = Utc("2025-05-01T20:00:00.123456");
    private static readonly DateTime EarlierCreatedAt = Utc("2025-05-01T23:10:00.654321");
    private static readonly DateTime NewPlayedAt = Utc("2025-06-14T21:45:00.000001");
    private static readonly DateTime AssignedAt = Utc("2025-03-03T10:00:00.111111");

    private const string EarlierSessionTestRunId = "e2e-flow4505-1760000000000";

    private static UserLibraryRepository CreateRepository(MeepleAiDbContext context) =>
        new(context, new Mock<IDomainEventCollector>().Object);

    private sealed record Seeded(Guid UserId, Guid GameId, Guid EntryId, Guid EarlierSessionId, Guid FirstLabelId, Guid SecondLabelId);

    /// <summary>
    /// Una voce con una partita già registrata (statistiche coerenti con quella) e due etichette
    /// dell'utente, non assegnate. La sessione porta anche il marcatore E2E: una scrittura che la
    /// toccasse senza volerlo lo azzererebbe.
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
            TimesPlayed = 1,
            LastPlayed = EarlierPlayedAt,
            WinRate = 0m,
            AvgDuration = 60,
            CompetitiveSessions = 1,
        };
        context.UserLibraryEntries.Add(entry);

        var earlierSession = new UserGameSessionEntity
        {
            Id = Guid.NewGuid(),
            UserLibraryEntryId = entry.Id,
            PlayedAt = EarlierPlayedAt,
            DurationMinutes = 60,
            DidWin = false,
            Players = "Anna, Bruno",
            Notes = "Prima partita",
            CreatedAt = EarlierCreatedAt,
            UpdatedAt = EarlierCreatedAt,
            TestRunId = EarlierSessionTestRunId,
        };
        context.Set<UserGameSessionEntity>().Add(earlierSession);

        var firstLabel = new GameLabelEntity { Id = Guid.NewGuid(), Name = "Da rigiocare", Color = "#3366ff", UserId = userId };
        var secondLabel = new GameLabelEntity { Id = Guid.NewGuid(), Name = "Con la famiglia", Color = "#ff9900", UserId = userId };
        context.GameLabels.AddRange(firstLabel, secondLabel);

        await context.SaveChangesAsync();
        return new Seeded(userId, gameId, entry.Id, earlierSession.Id, firstLabel.Id, secondLabel.Id);
    }

    private async Task AssignAsync(Guid entryId, Guid labelId)
    {
        await using var context = database.CreateDbContext();
        context.UserGameLabels.Add(new UserGameLabelEntity
        {
            Id = Guid.NewGuid(),
            UserLibraryEntryId = entryId,
            LabelId = labelId,
            AssignedAt = AssignedAt,
        });
        await context.SaveChangesAsync();
    }

    private async Task<List<Guid>> AssignedLabelIdsAsync(Guid entryId)
    {
        await using var context = database.CreateDbContext();
        return await context.UserGameLabels.AsNoTracking()
            .Where(l => l.UserLibraryEntryId == entryId)
            .Select(l => l.LabelId)
            .ToListAsync();
    }

    private async Task<Guid> RecordSessionAsync(RecordGameSessionCommand command)
    {
        await using var context = database.CreateDbContext();
        var handler = new RecordGameSessionCommandHandler(
            CreateRepository(context),
            new EfCoreUnitOfWork(context),
            TestDbContextFactory.CreateInMemoryHybridCache(),
            new Mock<IPublisher>().Object,
            NullLogger<RecordGameSessionCommandHandler>.Instance);
        return await handler.Handle(command, TestContext.Current.CancellationToken);
    }

    private async Task AddLabelAsync(Seeded seeded, Guid labelId)
    {
        await using var context = database.CreateDbContext();
        var handler = new AddLabelToGameCommandHandler(
            CreateRepository(context),
            new GameLabelRepository(context, new Mock<IDomainEventCollector>().Object),
            new EfCoreUnitOfWork(context));
        await handler.Handle(new AddLabelToGameCommand(seeded.UserId, seeded.GameId, labelId), TestContext.Current.CancellationToken);
    }

    private async Task RemoveLabelAsync(Seeded seeded, Guid labelId)
    {
        await using var context = database.CreateDbContext();
        var handler = new RemoveLabelFromGameCommandHandler(CreateRepository(context), new EfCoreUnitOfWork(context));
        await handler.Handle(new RemoveLabelFromGameCommand(seeded.UserId, seeded.GameId, labelId), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RecordGameSession_SavesTheSessionAndUpdatesTheStats()
    {
        var seeded = await SeedAsync();
        var sessionsBefore = await ReadAsync<UserGameSessionEntity>(database, seeded.EntryId);

        var sessionId = await RecordSessionAsync(new RecordGameSessionCommand(
            seeded.UserId, seeded.GameId, NewPlayedAt, DurationMinutes: 90, DidWin: true,
            Players: "Anna, Bruno, Carla", Notes: "Rivincita"));

        var sessionsAfter = await ReadAsync<UserGameSessionEntity>(database, seeded.EntryId);
        sessionsAfter.Keys.Should().BeEquivalentTo([seeded.EarlierSessionId, sessionId],
            "la partita nuova si aggiunge a quella già registrata, che la lettura non aveva caricato");

        var recorded = sessionsAfter[sessionId];
        recorded[nameof(UserGameSessionEntity.PlayedAt)].Should().Be(NewPlayedAt);
        recorded[nameof(UserGameSessionEntity.DurationMinutes)].Should().Be(90);
        recorded[nameof(UserGameSessionEntity.DidWin)].Should().Be(true);
        recorded[nameof(UserGameSessionEntity.Players)].Should().Be("Anna, Bruno, Carla");
        recorded[nameof(UserGameSessionEntity.Notes)].Should().Be("Rivincita");

        // La sessione già salvata resta com'era, marcatore E2E e date comprese.
        string.Join(Environment.NewLine, Differences(
                sessionsBefore,
                sessionsAfter.Where(s => s.Key != sessionId).ToDictionary(s => s.Key, s => s.Value)))
            .Should().BeEmpty("registrare una partita non deve toccare le altre");

        await using var verification = database.CreateDbContext();
        var stored = await verification.UserLibraryEntries.AsNoTracking().SingleAsync(e => e.Id == seeded.EntryId);
        stored.TimesPlayed.Should().Be(2);
        stored.LastPlayed.Should().Be(NewPlayedAt);
        stored.AvgDuration.Should().Be(75, "(60 + 90) / 2");
        stored.CompetitiveSessions.Should().Be(2);
        stored.WinRate.Should().Be(50m, "una vinta su due competitive");
    }

    [Fact]
    public async Task AddLabel_SavesTheAssignmentAndKeepsTheOthers()
    {
        var seeded = await SeedAsync();
        await AssignAsync(seeded.EntryId, seeded.FirstLabelId);

        await AddLabelAsync(seeded, seeded.SecondLabelId);

        (await AssignedLabelIdsAsync(seeded.EntryId)).Should().BeEquivalentTo(
            [seeded.FirstLabelId, seeded.SecondLabelId],
            "la rotta risponde con l'etichetta: deve averla salvata, senza perdere quella già assegnata");
    }

    [Fact]
    public async Task AddLabel_AlreadyAssigned_IsAConflict()
    {
        var seeded = await SeedAsync();
        await AssignAsync(seeded.EntryId, seeded.FirstLabelId);

        var addAgain = () => AddLabelAsync(seeded, seeded.FirstLabelId);

        // Il dominio rifiuta il doppione solo se la lettura gli ha dato le etichette assegnate.
        await addAgain.Should().ThrowAsync<ConflictException>();
        (await AssignedLabelIdsAsync(seeded.EntryId)).Should().BeEquivalentTo([seeded.FirstLabelId]);
    }

    [Fact]
    public async Task RemoveLabel_DeletesTheAssignmentAndKeepsTheOthers()
    {
        var seeded = await SeedAsync();
        await AssignAsync(seeded.EntryId, seeded.FirstLabelId);
        await AssignAsync(seeded.EntryId, seeded.SecondLabelId);

        await RemoveLabelAsync(seeded, seeded.FirstLabelId);

        (await AssignedLabelIdsAsync(seeded.EntryId)).Should().BeEquivalentTo(
            [seeded.SecondLabelId],
            "l'etichetta tolta sparisce, l'altra resta");
    }

    [Fact]
    public async Task RemoveLabel_NotAssigned_IsNotFound()
    {
        var seeded = await SeedAsync();
        await AssignAsync(seeded.EntryId, seeded.FirstLabelId);

        var remove = () => RemoveLabelAsync(seeded, seeded.SecondLabelId);

        await remove.Should().ThrowAsync<NotFoundException>();
        (await AssignedLabelIdsAsync(seeded.EntryId)).Should().BeEquivalentTo([seeded.FirstLabelId]);
    }
}
