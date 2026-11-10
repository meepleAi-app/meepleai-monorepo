using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.Integration.GameManagement;

/// <summary>
/// Concorrenza ottimistica su <c>game_night_invitations</c> (#4744).
///
/// <c>GameNightInvitationRepository.UpdateAsync</c> mappa l'aggregato su un'entità staccata e chiama
/// <c>DbSet.Update()</c>: EF scrive TUTTE le colonne a partire da quanto il gestore ha letto. Senza un token di
/// concorrenza vince l'ultimo che scrive, senza errore: l'ospite risponde mentre l'organizzatrice ritira, e uno dei
/// due sparisce. Come per le altre entità passate a <c>xmin</c> (ADR-060), i test coprono il percorso reale (il
/// repository, non il DbContext) e in entrambe le direzioni:
///
/// <list type="number">
/// <item>uno scrittore solo deve <b>riuscire</b> (un token che non arriva all'UPDATE diventa <c>WHERE xmin = 0</c>,
/// che non corrisponde mai: rifiuterebbe ogni scrittura);</item>
/// <item>due scrittori sovrapposti: il secondo deve essere <b>rifiutato</b>, in entrambi gli ordini.</item>
/// </list>
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "4744")]
public sealed class GameNightInvitationXminConcurrencyTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = null!;
    private string _connectionString = null!;
    private MeepleAiDbContext _dbContext = null!;
    private Guid _organizerId;

    public GameNightInvitationXminConcurrencyTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"invitation_xmin_{Guid.NewGuid():N}";
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        _dbContext = _fixture.CreateDbContext(_connectionString);
        await _dbContext.Database.MigrateAsync();
        _organizerId = Guid.NewGuid();
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    private static GameNightInvitationRepository RepositoryOn(MeepleAiDbContext db)
    {
        var collector = new Mock<IDomainEventCollector>();
        collector
            .Setup(e => e.GetAndClearEvents())
            .Returns(new List<Api.SharedKernel.Domain.Interfaces.IDomainEvent>().AsReadOnly());
        return new GameNightInvitationRepository(db, collector.Object, NullLogger<GameNightInvitationRepository>.Instance);
    }

    private async Task<GameNightInvitation> SeedPendingInvitationAsync(string? testRunId = null)
    {
        var nightId = Guid.NewGuid();
        _dbContext.GameNightEvents.Add(new GameNightEventEntity
        {
            Id = nightId,
            OrganizerId = _organizerId,
            Title = "Friday Catan",
            ScheduledAt = Now.AddDays(2),
            Location = "Home",
            MaxPlayers = 6,
            GameIdsJson = "[]",
            Status = "Published",
            CreatedAt = Now,
            UpdatedAt = Now
        });
        await _dbContext.SaveChangesAsync();

        var invitation = GameNightInvitation.Create(nightId, "guest@example.com", Now.AddDays(7), _organizerId, Now);
        await RepositoryOn(_dbContext).AddAsync(invitation, CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        if (testRunId is not null)
        {
            await _dbContext.GameNightInvitations
                .Where(i => i.Id == invitation.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.TestRunId, testRunId));
        }

        _dbContext.ChangeTracker.Clear();
        return invitation;
    }

    [Fact(DisplayName = "Inviti: un solo scrittore riesce, il token non blocca chi non ha rivali")]
    public async Task Update_WithNoConcurrentWriter_Succeeds()
    {
        var seeded = await SeedPendingInvitationAsync();
        var repository = RepositoryOn(_dbContext);

        var loaded = await repository.GetByIdAsync(seeded.Id, CancellationToken.None);
        loaded!.Accept(null, Now.AddHours(1), "Marco").Should().BeTrue();
        await repository.UpdateAsync(loaded, CancellationToken.None);
        Func<Task> act = async () => await _dbContext.SaveChangesAsync();

        await act.Should().NotThrowAsync(
            "senza scritture concorrenti l'update deve passare: se il token letto dal DB non arriva all'UPDATE, " +
            "la clausola diventa `WHERE xmin = 0` e nessuna riga corrisponde");

        _dbContext.ChangeTracker.Clear();
        var reloaded = await repository.GetByIdAsync(seeded.Id, CancellationToken.None);
        reloaded!.Status.Should().Be(GameNightInvitationStatus.Accepted);
        reloaded.RespondedByName.Should().Be("Marco");
    }

    [Fact(DisplayName = "Inviti: l'ospite risponde per primo, il ritiro dell'organizzatrice è rifiutato")]
    public async Task Update_TheGuestAnswersFirst_TheWithdrawalAfterIt_IsRefused()
    {
        var seeded = await SeedPendingInvitationAsync();
        await using var dbGuest = _fixture.CreateDbContext(_connectionString);
        await using var dbOrganizer = _fixture.CreateDbContext(_connectionString);
        var repoGuest = RepositoryOn(dbGuest);
        var repoOrganizer = RepositoryOn(dbOrganizer);

        var seenByGuest = await repoGuest.GetByIdAsync(seeded.Id, CancellationToken.None);
        var seenByOrganizer = await repoOrganizer.GetByIdAsync(seeded.Id, CancellationToken.None);
        seenByGuest.Should().NotBeSameAs(seenByOrganizer, "sono due scope indipendenti sulla stessa riga");

        seenByGuest!.Accept(null, Now.AddHours(1), "Marco");
        await repoGuest.UpdateAsync(seenByGuest, CancellationToken.None);
        await dbGuest.SaveChangesAsync();

        seenByOrganizer!.Cancel(Now.AddHours(1));
        await repoOrganizer.UpdateAsync(seenByOrganizer, CancellationToken.None);
        Func<Task> act = async () => await dbOrganizer.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>(
            "l'organizzatrice ha letto «in attesa» prima della risposta: ritirare adesso cancellerebbe in silenzio " +
            "il «sì» che l'ospite ha già avuto confermato");
    }

    [Fact(DisplayName = "Inviti: l'organizzatrice ritira per prima, la risposta dell'ospite è rifiutata")]
    public async Task Update_TheOrganizerWithdrawsFirst_TheAnswerAfterIt_IsRefused()
    {
        var seeded = await SeedPendingInvitationAsync();
        await using var dbGuest = _fixture.CreateDbContext(_connectionString);
        await using var dbOrganizer = _fixture.CreateDbContext(_connectionString);
        var repoGuest = RepositoryOn(dbGuest);
        var repoOrganizer = RepositoryOn(dbOrganizer);

        var seenByGuest = await repoGuest.GetByIdAsync(seeded.Id, CancellationToken.None);
        var seenByOrganizer = await repoOrganizer.GetByIdAsync(seeded.Id, CancellationToken.None);

        seenByOrganizer!.Cancel(Now.AddHours(1));
        await repoOrganizer.UpdateAsync(seenByOrganizer, CancellationToken.None);
        await dbOrganizer.SaveChangesAsync();

        seenByGuest!.Accept(null, Now.AddHours(1), "Marco");
        await repoGuest.UpdateAsync(seenByGuest, CancellationToken.None);
        Func<Task> act = async () => await dbGuest.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>(
            "l'ospite ha letto «in attesa» prima del ritiro: accettare adesso rimetterebbe in gioco un invito ritirato");
    }

    [Fact(DisplayName = "Inviti: riscrivere una riga non azzera TestRunId (la pulizia E2E la troverebbe ancora)")]
    public async Task Update_DoesNotEraseTheTestRunId()
    {
        var seeded = await SeedPendingInvitationAsync(testRunId: "run-4744");
        var repository = RepositoryOn(_dbContext);

        var loaded = await repository.GetByIdAsync(seeded.Id, CancellationToken.None);
        loaded!.Accept(null, Now.AddHours(1), "Marco");
        await repository.UpdateAsync(loaded, CancellationToken.None);
        await _dbContext.SaveChangesAsync();

        _dbContext.ChangeTracker.Clear();
        var testRunId = await _dbContext.GameNightInvitations
            .AsNoTracking()
            .Where(i => i.Id == seeded.Id)
            .Select(i => i.TestRunId)
            .SingleAsync();
        testRunId.Should().Be("run-4744",
            "MapToPersistence non porta TestRunId, e `Update()` lo scriveva NULL: la riga di test sfuggiva a CleanupTestEntities");
    }

    // ── I marcatori «scritto a» (#4744) ───────────────────────────────────────────────────────────────────────
    // I gestori di evento che li scrivono girano secondi dopo le email, da una copia dell'invito letta PRIMA: se
    // riscrivessero la copia intera, un «sì» o un ritiro arrivati nel frattempo sparirebbero. Il repository scrive
    // UNA colonna sola, e solo se è ancora vuota.

    public static TheoryData<string> Marks => new()
    {
        nameof(IGameNightInvitationRepository.MarkRsvpConfirmationSentAsync),
        nameof(IGameNightInvitationRepository.MarkCancellationNoticeSentAsync),
        nameof(IGameNightInvitationRepository.MarkReminder24hSentAsync),
    };

    private static Task MarkAsync(GameNightInvitationRepository repository, string mark, Guid id, DateTimeOffset at) =>
        mark switch
        {
            nameof(IGameNightInvitationRepository.MarkRsvpConfirmationSentAsync) =>
                repository.MarkRsvpConfirmationSentAsync(id, at, CancellationToken.None),
            nameof(IGameNightInvitationRepository.MarkCancellationNoticeSentAsync) =>
                repository.MarkCancellationNoticeSentAsync(id, at, CancellationToken.None),
            nameof(IGameNightInvitationRepository.MarkReminder24hSentAsync) =>
                repository.MarkReminder24hSentAsync(id, at, CancellationToken.None),
            _ => throw new ArgumentOutOfRangeException(nameof(mark), mark, null),
        };

    private static DateTimeOffset? MarkOf(GameNightInvitationEntity row, string mark) =>
        mark switch
        {
            nameof(IGameNightInvitationRepository.MarkRsvpConfirmationSentAsync) => row.RsvpConfirmationSentAt,
            nameof(IGameNightInvitationRepository.MarkCancellationNoticeSentAsync) => row.CancellationNoticeSentAt,
            nameof(IGameNightInvitationRepository.MarkReminder24hSentAsync) => row.Reminder24hSentAt,
            _ => throw new ArgumentOutOfRangeException(nameof(mark), mark, null),
        };

    private async Task<GameNightInvitationEntity> RowAsync(Guid id)
    {
        await using var db = _fixture.CreateDbContext(_connectionString);
        return await db.GameNightInvitations.AsNoTracking().SingleAsync(i => i.Id == id);
    }

    [Theory(DisplayName = "Inviti: il marcatore scrive la sua colonna e lascia stare tutto il resto")]
    [MemberData(nameof(Marks))]
    public async Task Mark_WritesOnlyItsColumn_AndLeavesAnAnswerThatArrivedMeanwhile(string mark)
    {
        var seeded = await SeedPendingInvitationAsync(testRunId: "run-4744");
        await using var dbHandler = _fixture.CreateDbContext(_connectionString);
        var staleCopyOfTheHandler = await RepositoryOn(dbHandler).GetByIdAsync(seeded.Id, CancellationToken.None);
        staleCopyOfTheHandler!.Status.Should().Be(GameNightInvitationStatus.Pending, "è la copia letta prima della risposta");

        // Mentre il gestore manda l'email, l'ospite risponde.
        await using (var dbGuest = _fixture.CreateDbContext(_connectionString))
        {
            var repoGuest = RepositoryOn(dbGuest);
            var seenByGuest = await repoGuest.GetByIdAsync(seeded.Id, CancellationToken.None);
            seenByGuest!.Accept(null, Now.AddHours(1), "Marco").Should().BeTrue();
            await repoGuest.UpdateAsync(seenByGuest, CancellationToken.None);
            await dbGuest.SaveChangesAsync();
        }

        await MarkAsync(RepositoryOn(dbHandler), mark, seeded.Id, Now.AddHours(2));

        var row = await RowAsync(seeded.Id);
        MarkOf(row, mark).Should().Be(Now.AddHours(2));
        row.Status.Should().Be(nameof(GameNightInvitationStatus.Accepted), "il marcatore non riscrive lo stato che ha letto");
        row.RespondedByName.Should().Be("Marco");
        row.TestRunId.Should().Be("run-4744");
    }

    [Theory(DisplayName = "Inviti: il marcatore non sovrascrive un marcatore già scritto")]
    [MemberData(nameof(Marks))]
    public async Task Mark_IsWrittenOnce_TheSecondWriteKeepsTheFirstTime(string mark)
    {
        var seeded = await SeedPendingInvitationAsync();
        var repository = RepositoryOn(_dbContext);

        await MarkAsync(repository, mark, seeded.Id, Now.AddHours(1));
        await MarkAsync(repository, mark, seeded.Id, Now.AddHours(5));

        MarkOf(await RowAsync(seeded.Id), mark).Should().Be(Now.AddHours(1),
            "una consegna ripetuta dell'evento non sposta l'ora a cui l'indirizzo è stato scritto");
    }

    [Theory(DisplayName = "Inviti: il marcatore di un invito sparito non fa danni")]
    [MemberData(nameof(Marks))]
    public async Task Mark_OfAnInvitationThatNoLongerExists_IsANoOp(string mark)
    {
        var act = () => MarkAsync(RepositoryOn(_dbContext), mark, Guid.NewGuid(), Now);

        await act.Should().NotThrowAsync();
    }

    [Fact(DisplayName = "Inviti: un marcatore cambia la versione della riga, chi aveva letto prima è rifiutato")]
    public async Task Mark_BumpsTheRowVersion_SoAStaleRewriteIsRefused()
    {
        // Il prezzo del marcatore mirato, dichiarato: scrive la riga, quindi sposta xmin. Chi aveva letto prima non
        // può riscriverla per intero; ritenta da una lettura nuova (InvitationWriteRetry), invece di annullare il
        // marcatore.
        var seeded = await SeedPendingInvitationAsync();
        await using var dbStale = _fixture.CreateDbContext(_connectionString);
        var repoStale = RepositoryOn(dbStale);
        var stale = await repoStale.GetByIdAsync(seeded.Id, CancellationToken.None);

        await RepositoryOn(_dbContext).MarkRsvpConfirmationSentAsync(seeded.Id, Now.AddHours(1), CancellationToken.None);

        stale!.Cancel(Now.AddHours(2));
        await repoStale.UpdateAsync(stale, CancellationToken.None);
        Func<Task> act = async () => await dbStale.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
        (await RowAsync(seeded.Id)).RsvpConfirmationSentAt.Should().Be(Now.AddHours(1), "il marcatore non è stato perso");
    }
}
