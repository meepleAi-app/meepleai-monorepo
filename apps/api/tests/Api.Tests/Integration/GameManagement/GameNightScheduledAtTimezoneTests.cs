using Api.BoundedContexts.DocumentProcessing.Application.Queries.Queue;
using Api.BoundedContexts.GameManagement.Application.Queries.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.Integration.GameManagement;

/// <summary>
/// #4055. Riproduce, contro un PostgreSQL reale, il rifiuto di Npgsql che faceva rispondere 500
/// a <c>POST</c>/<c>PUT /api/v1/game-nights</c> per qualunque offset diverso da UTC:
/// <c>Cannot write DateTimeOffset with Offset=01:00:00 to PostgreSQL type "timestamp with time
/// zone", only offset 0 (UTC) is supported</c>.
/// </summary>
/// <remarks>
/// <para>
/// Serve un database vero. I test unit del percorso (compresi i preesistenti
/// <c>CheckGameNightConflictQueryHandlerTests</c>) girano su <c>UseInMemoryDatabase</c>, che
/// accetta qualunque offset: e' il motivo per cui il difetto e' arrivato in produzione con la
/// copertura unit verde. Misura sullo stack locale il 2026-10-04, prima del fix: POST con
/// <c>+01:00</c>/<c>-05:00</c> → 500, con <c>Z</c>/<c>+00:00</c> → 201;
/// <c>GET /game-nights/check-conflict?at=…+01:00</c> → 400 e
/// <c>GET /admin/queue?fromDate=…+01:00</c> → 400, entrambe con
/// <c>DateTimeOffsetConverter.WriteCore</c> in cima allo stack.
/// </para>
/// <para>
/// I tre test coprono i tre punti in cui un <see cref="DateTimeOffset"/> del client raggiungeva
/// Npgsql: la scrittura della colonna (aggregato → repository) e due parametri di query
/// (<c>check-conflict</c> e la coda di elaborazione, condivisa da <c>/admin/queue</c> e
/// <c>/admin/kb/processing-queue</c>). Le due query non richiedono righe: l'eccezione nasceva
/// dalla scrittura del parametro, prima di qualunque risultato.
/// </para>
/// </remarks>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "4055")]
public sealed class GameNightScheduledAtTimezoneTests : IAsyncLifetime
{
    /// <summary>20:00 a Roma e 14:00 a New York sono lo stesso istante: 19:00Z.</summary>
    private static readonly DateTimeOffset RomeEvening = new(2026, 12, 20, 20, 0, 0, TimeSpan.FromHours(1));
    private static readonly DateTimeOffset NewYorkAfternoon = new(2026, 12, 20, 14, 0, 0, TimeSpan.FromHours(-5));
    private static readonly DateTimeOffset SameInstantUtc = new(2026, 12, 20, 19, 0, 0, TimeSpan.Zero);

    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = null!;
    private string _connectionString = null!;
    private MeepleAiDbContext _dbContext = null!;

    public GameNightScheduledAtTimezoneTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"gamenight_tz_{Guid.NewGuid():N}";
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        _dbContext = _fixture.CreateDbContext(_connectionString);
        await _dbContext.Database.MigrateAsync(TestCancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    private GameNightEventRepository CreateRepository(MeepleAiDbContext dbContext) =>
        new(dbContext, Mock.Of<IDomainEventCollector>(), Mock.Of<ILogger<GameNightEventRepository>>());

    /// <summary>
    /// Il percorso di scrittura di <c>POST /game-nights</c> senza HTTP: aggregato → repository →
    /// <c>SaveChangesAsync</c>. Prima del fix, questo <c>SaveChangesAsync</c> lanciava
    /// <see cref="ArgumentException"/> ed e' l'eccezione che il chiamante vedeva come 500.
    /// </summary>
    [Theory]
    [InlineData("+01:00")]
    [InlineData("-05:00")]
    public async Task Save_WithNonUtcScheduledAt_PersistsTheSameInstant(string offsetLabel)
    {
        var scheduledAt = offsetLabel == "+01:00" ? RomeEvening : NewYorkAfternoon;
        var repository = CreateRepository(_dbContext);
        var night = GameNightEvent.Create(Guid.NewGuid(), "Serata con offset", scheduledAt);

        await repository.AddAsync(night, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        await using var readContext = _fixture.CreateDbContext(_connectionString);
        var persisted = await readContext.GameNightEvents
            .AsNoTracking()
            .SingleAsync(e => e.Id == night.Id, TestCancellationToken);

        persisted.ScheduledAt.Should().Be(SameInstantUtc);
        persisted.ScheduledAt.Offset.Should().Be(TimeSpan.Zero, "timestamptz rilegge sempre a offset 0");
    }

    /// <summary>
    /// Il percorso di aggiornamento di <c>PUT /game-nights/{id}</c>: la serata nasce in UTC e
    /// viene spostata con un orario locale. Prima del fix il secondo <c>SaveChangesAsync</c>
    /// lanciava, quindi la riga restava all'istante originale.
    /// </summary>
    [Fact]
    public async Task Update_WithNonUtcScheduledAt_PersistsTheSameInstant()
    {
        var repository = CreateRepository(_dbContext);
        var night = GameNightEvent.Create(
            Guid.NewGuid(), "Serata da spostare", new DateTimeOffset(2026, 11, 1, 18, 0, 0, TimeSpan.Zero));
        await repository.AddAsync(night, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        await using var updateContext = _fixture.CreateDbContext(_connectionString);
        var updateRepository = CreateRepository(updateContext);
        var loaded = await updateRepository.GetByIdAsync(night.Id, TestCancellationToken);
        loaded.Should().NotBeNull();

        loaded!.Update("Serata spostata", null, RomeEvening, null, null, null);
        await updateRepository.UpdateAsync(loaded, TestCancellationToken);
        await updateContext.SaveChangesAsync(TestCancellationToken);

        await using var readContext = _fixture.CreateDbContext(_connectionString);
        var persisted = await readContext.GameNightEvents
            .AsNoTracking()
            .SingleAsync(e => e.Id == night.Id, TestCancellationToken);

        persisted.ScheduledAt.Should().Be(SameInstantUtc);
    }

    /// <summary>
    /// <c>GET /game-nights/check-conflict?at=…</c>: <c>at</c> finisce in due parametri Npgsql
    /// (gli estremi della finestra ±2h) confrontati con <c>scheduled_at</c>. L'asserzione non e'
    /// solo «non lancia»: la serata salvata a 19:00Z deve risultare in conflitto con le 20:00+01:00,
    /// che sono lo stesso istante.
    /// </summary>
    [Fact]
    public async Task CheckConflict_WithNonUtcProposedAt_MatchesTheSameInstant()
    {
        var organizerId = Guid.NewGuid();
        var repository = CreateRepository(_dbContext);
        var night = GameNightEvent.Create(organizerId, "Serata gia' in agenda", SameInstantUtc);
        await repository.AddAsync(night, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        await using var queryContext = _fixture.CreateDbContext(_connectionString);
        var handler = new CheckGameNightConflictQueryHandler(queryContext);

        var result = await handler.Handle(
            new CheckGameNightConflictQuery(organizerId, RomeEvening), TestCancellationToken);

        result.HasConflict.Should().BeTrue();
        result.Conflicts.Should().ContainSingle().Which.Id.Should().Be(night.Id);
    }

    /// <summary>
    /// <c>GET /admin/queue</c> e <c>GET /admin/kb/processing-queue</c> condividono
    /// <see cref="GetProcessingQueueQuery"/>, e <c>fromDate</c>/<c>toDate</c> diventano parametri
    /// contro <c>created_at</c>. Nessuna riga serve: prima del fix l'eccezione arrivava dalla
    /// scrittura del parametro, non dal confronto.
    /// </summary>
    [Fact]
    public async Task ProcessingQueue_WithNonUtcDateFilters_DoesNotThrow()
    {
        await using var queryContext = _fixture.CreateDbContext(_connectionString);
        var handler = new GetProcessingQueueQueryHandler(queryContext);

        var result = await handler.Handle(
            new GetProcessingQueueQuery(
                FromDate: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(1)),
                ToDate: new DateTimeOffset(2026, 12, 31, 23, 59, 59, TimeSpan.FromHours(-5))),
            TestCancellationToken);

        result.Total.Should().Be(0);
        result.Jobs.Should().BeEmpty();
    }
}
