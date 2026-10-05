using Api.BoundedContexts.SessionTracking.Infrastructure.Services;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Exceptions;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.BoundedContexts.SessionTracking.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.Infrastructure.Entities.GameManagement;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Npgsql;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Integration tests for the extended <see cref="CreateSessionCommand"/> (Session Flow v2.1 — T4).
/// Uses an isolated PostgreSQL database (via <see cref="SharedTestcontainersFixture"/>) and the T0
/// seeders to verify ad-hoc GameNight creation, KB readiness gating, and guest participant handling.
///
/// Tests 4 (existing-night attach) and 5 (active-session-in-night conflict) are deferred — they
/// depend on the PauseSessionCommand that lands in T5.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "SessionTracking")]
[Trait("Feature", "SessionFlowV2.1")]
public sealed class CreateSessionCommandAdHocTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private string _isolatedDbConnectionString = string.Empty;
    private ServiceProvider? _serviceProvider;
    private MeepleAiDbContext? _dbContext;
    private CreateSessionCommandHandler? _handler;
    private PauseSessionCommandHandler? _pauseHandler;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public CreateSessionCommandAdHocTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_createsession_adhoc_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(_isolatedDbConnectionString);
        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();

        // Apply migrations with transient-error retry (T3 pattern).
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await _dbContext.Database.MigrateAsync(TestCancellationToken);
                break;
            }
            catch (NpgsqlException) when (attempt < 2)
            {
                await Task.Delay(500, TestCancellationToken);
            }
        }

        // Wire the command handler with real collaborators where possible and stub quota.
        var eventCollector = _serviceProvider.GetRequiredService<IDomainEventCollector>();
        var unitOfWork = _serviceProvider.GetRequiredService<IUnitOfWork>();
        var mediator = _serviceProvider.GetRequiredService<IMediator>();
        var sessionRepo = new SessionRepository(_dbContext, eventCollector);

        var quotaMock = new Mock<ISessionQuotaService>();
        quotaMock
            .Setup(s => s.CheckQuotaAsync(
                It.IsAny<Guid>(),
                It.IsAny<UserTier>(),
                It.IsAny<Role>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SessionQuotaResult.Allowed(0, 100));

        var loggerFactory = _serviceProvider.GetRequiredService<ILoggerFactory>();
        _handler = new CreateSessionCommandHandler(
            sessionRepo,
            unitOfWork,
            quotaMock.Object,
            _dbContext,
            mediator,
            loggerFactory.CreateLogger<CreateSessionCommandHandler>(),
            TimeProvider.System,
            new DiaryStreamService());

        // T5: PauseSessionCommandHandler shares the same SessionRepository / UnitOfWork
        // so the two scenarios that depend on Pause can drive a real domain transition.
        _pauseHandler = new PauseSessionCommandHandler(sessionRepo, unitOfWork, _dbContext, TimeProvider.System, new DiaryStreamService());
    }

    public async ValueTask DisposeAsync()
    {
        _dbContext?.Dispose();
        if (_serviceProvider is not null)
        {
            await _serviceProvider.DisposeAsync();
        }

        if (!string.IsNullOrEmpty(_databaseName))
        {
            try
            {
                await _fixture.DropIsolatedDatabaseAsync(_databaseName);
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }

    /// <summary>
    /// ⚠️ Questo test FALLISCE, da prima del 2026-10-03, ed e' una delle quattro voci della
    /// baseline <c>Core</c> di <c>dev-async</c>. Tracciato in <b>#4074</b> — non e' un flaky e
    /// non e' da correggere qui.
    ///
    /// <para>Misurato: l'asserzione su <c>Status</c> trova <c>"Published"</c> dove attende
    /// <c>"InProgress"</c>. La causa non e' «il prodotto sbaglia» ne' «il test sbaglia»: la spec
    /// del dominio si contraddice. Il testo dell'invariante #15 dice «triggered by first Session
    /// creation (draft or live)», la sua riga nella tabella di mappatura dice
    /// «<c>HandleFirstSessionStarted</c> on <c>SessionStartedDomainEvent</c>» — e quell'evento e'
    /// alzato solo da <c>Session.OpenLiveMode()</c>, cioe' solo al go-live. L'implementazione
    /// segue la mappatura, questo test segue il testo, e ciascuno e' fedele a una meta'.</para>
    ///
    /// <para>#4074 chiede di scegliere quale meta' vale e di correggere la spec nella stessa PR
    /// del codice. Non viene messo in <c>Skip</c> di proposito: un salto farebbe salire il
    /// conteggio dei salti della baseline, che il gate tratta come regressione.</para>
    /// </summary>
    [Fact]
    public async Task Handle_NoGameNightProvided_CreatesAdHocNightImplicitly()
    {
        // Arrange — seed user + library game with Ready KB (via T0 seeder).
        var (userId, gameId) = await _fixture.SeedUserWithLibraryGameAndIndexedKbAsync(_dbContext!, vectorCount: 3);

        var command = new CreateSessionCommand(
            UserId: userId,
            GameId: gameId,
            SessionType: nameof(Api.BoundedContexts.SessionTracking.Domain.Entities.SessionType.Generic),
            SessionDate: null,
            Location: null,
            Participants: new List<ParticipantDto>
            {
                new() { DisplayName = "Owner", IsOwner = true, UserId = userId }
            });

        // Act
        var result = await _handler!.Handle(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.GameNightWasCreated.Should().BeTrue();
        result.GameNightEventId.Should().NotBeEmpty();
        result.SessionId.Should().NotBeEmpty();
        result.SessionCode.Should().NotBeNullOrEmpty();

        // The persisted GameNightEvent must be InProgress with the game attached.
        var persistedNight = await _dbContext!.GameNightEvents
            .AsNoTracking()
            .Include(e => e.Sessions)
            .FirstOrDefaultAsync(e => e.Id == result.GameNightEventId, TestCancellationToken);

        persistedNight.Should().NotBeNull();
        persistedNight!.Status.Should().Be("InProgress");
        persistedNight.OrganizerId.Should().Be(userId);
        persistedNight.GameIdsJson.Should().Contain(gameId.ToString());

        // The link row must point at our new session.
        persistedNight.Sessions.Should().ContainSingle(s => s.SessionId == result.SessionId);

        // Diary events: session_created + gamenight_created.
        var diaryEvents = await _dbContext.SessionEvents
            .AsNoTracking()
            .Where(e => e.SessionId == result.SessionId)
            .Select(e => e.EventType)
            .ToListAsync(TestCancellationToken);

        diaryEvents.Should().Contain("session_created");
        diaryEvents.Should().Contain("gamenight_created");
    }

    [Fact]
    public async Task Handle_KbNotReady_Throws422()
    {
        // Arrange — seed user + library game WITHOUT an indexed KB.
        var (userId, gameId) = await _fixture.SeedUserWithLibraryGameButNoKbAsync(_dbContext!);

        var command = new CreateSessionCommand(
            UserId: userId,
            GameId: gameId,
            SessionType: nameof(Api.BoundedContexts.SessionTracking.Domain.Entities.SessionType.Generic),
            SessionDate: null,
            Location: null,
            Participants: new List<ParticipantDto>
            {
                new() { DisplayName = "Owner", IsOwner = true, UserId = userId }
            });

        // Act / Assert
        var ex = await Assert.ThrowsAsync<UnprocessableEntityException>(
            () => _handler!.Handle(command, TestCancellationToken));

        ex.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
        ex.ErrorCode.Should().Be("kb_not_ready");

        // No partial state should leak through: no session, no night, no diary events.
        var sessionsCount = await _dbContext!.SessionTrackingSessions.AsNoTracking().CountAsync(TestCancellationToken);
        sessionsCount.Should().Be(0);

        var nightsCount = await _dbContext.GameNightEvents.AsNoTracking().CountAsync(TestCancellationToken);
        nightsCount.Should().Be(0);

        var diaryCount = await _dbContext.SessionEvents.AsNoTracking().CountAsync(TestCancellationToken);
        diaryCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_GuestNamesProvided_AddsParticipantsWithDisplayNames()
    {
        // Arrange — ready KB + 2 guest names.
        var (userId, gameId) = await _fixture.SeedUserWithLibraryGameAndIndexedKbAsync(_dbContext!, vectorCount: 2);

        var command = new CreateSessionCommand(
            UserId: userId,
            GameId: gameId,
            SessionType: nameof(Api.BoundedContexts.SessionTracking.Domain.Entities.SessionType.Generic),
            SessionDate: null,
            Location: null,
            Participants: new List<ParticipantDto>
            {
                new() { DisplayName = "Owner", IsOwner = true, UserId = userId }
            },
            GameNightEventId: null,
            GuestNames: new[] { "Luca", "Sara" });

        // Act
        var result = await _handler!.Handle(command, TestCancellationToken);

        // Assert — owner + 2 guests = 3 participants total in the response.
        result.Participants.Should().HaveCount(3);
        result.Participants.Should().ContainSingle(p => p.IsOwner);
        result.Participants.Should().Contain(p => p.DisplayName == "Luca" && !p.IsOwner);
        result.Participants.Should().Contain(p => p.DisplayName == "Sara" && !p.IsOwner);

        // Guest participants must be persisted (no UserId) alongside the owner.
        _dbContext!.ChangeTracker.Clear();
        var persistedSession = await _dbContext.SessionTrackingSessions
            .AsNoTracking()
            .Include(s => s.Participants)
            .FirstOrDefaultAsync(s => s.Id == result.SessionId, TestCancellationToken);

        persistedSession.Should().NotBeNull();
        persistedSession!.Participants.Should().HaveCount(3);
        persistedSession.Participants.Should().Contain(p => p.DisplayName == "Luca" && p.UserId == null);
        persistedSession.Participants.Should().Contain(p => p.DisplayName == "Sara" && p.UserId == null);
    }

    [Fact]
    public async Task Handle_ExistingNightProvided_AttachesGameToNight()
    {
        // Arrange — create a first session that owns a fresh ad-hoc GameNight, then pause it
        // to free the InProgress slot so a second session can be attached to the same night.
        var (userId, gameId1) = await _fixture.SeedUserWithLibraryGameAndIndexedKbAsync(_dbContext!, vectorCount: 2);
        var first = await _handler!.Handle(BuildCommand(userId, gameId1), TestCancellationToken);

        await _pauseHandler!.Handle(
            new PauseSessionCommand(first.SessionId, userId),
            TestCancellationToken);

        // Seed a second library game with a Ready KB on the same user.
        var gameId2 = await _fixture.SeedAnotherLibraryGameAsync(_dbContext!, userId);

        // Build a fresh, isolated handler stack on the same database so the second
        // CreateSession runs against a clean change tracker. The previous handler chain
        // (Create + Pause) leaves the SessionEntity tracked with a [Timestamp] RowVersion
        // that conflicts with subsequent reloads inside the same DbContext instance.
        await using var freshScope = BuildIsolatedScope();
        var freshDb = freshScope.GetRequiredService<MeepleAiDbContext>();
        var freshCreateHandler = BuildCreateHandler(freshScope, freshDb);

        // Act — create another session targeting the SAME GameNightEvent.
        var second = await freshCreateHandler.Handle(
            BuildCommand(userId, gameId2, gameNightEventId: first.GameNightEventId),
            TestCancellationToken);

        // Assert — the existing night is reused and the new game is appended to its
        // GameIdsJson list. GameNightWasCreated must be false on the second response.
        second.GameNightEventId.Should().Be(first.GameNightEventId);
        second.GameNightWasCreated.Should().BeFalse();

        var night = await freshDb.GameNightEvents
            .AsNoTracking()
            .Include(e => e.Sessions)
            .FirstAsync(e => e.Id == first.GameNightEventId, TestCancellationToken);

        night.GameIdsJson.Should().Contain(gameId1.ToString());
        night.GameIdsJson.Should().Contain(gameId2.ToString());
        night.Sessions.Should().Contain(s => s.SessionId == first.SessionId);
        night.Sessions.Should().Contain(s => s.SessionId == second.SessionId);

        // Diary: the second create must have written a "gamenight_game_added" entry.
        var addedEvents = await freshDb.SessionEvents
            .AsNoTracking()
            .Where(e => e.SessionId == second.SessionId && e.EventType == "gamenight_game_added")
            .ToListAsync(TestCancellationToken);
        addedEvents.Should().HaveCount(1);
    }

    /// <summary>
    /// 🔴 Questo test asseriva l'OPPOSTO dell'invariante documentata, e falliva da allora.
    ///
    /// <para>Si chiamava <c>Handle_ActiveSessionInNight_Throws409</c> e pretendeva una
    /// <c>ConflictException</c> dal handler di CREAZIONE quando la serata ha già una sessione
    /// attiva. La spec del dominio
    /// (<c>docs/for-developers/specs/2026-06-04-gamenight-session-domain-model.md</c>, che
    /// CLAUDE.md indica come fonte di verità) dice il contrario su due invarianti:</para>
    ///
    /// <list type="bullet">
    ///   <item><b>#10 — max 1 live per GameNight</b>: la guardia vive in
    ///     <c>GameNightEvent.EnsureCanStartSession()</c> / <c>StartCurrentSession()</c> e lancia
    ///     <c>MaxLiveSessionsExceededException</c> — al <b>go-live</b>, non alla creazione. E
    ///     scatta su una sessione <c>InProgress</c>, non su una <c>Active</c> di tracking.</item>
    ///   <item><b>#13</b>: «salvataggio draft con live attiva <b>permesso</b> + warning non
    ///     bloccante». Il testo di #10 lo ripete: «L'utente può sempre creare draft anche con
    ///     live attiva (per registrazioni retroattive)».</item>
    /// </list>
    ///
    /// <para>L'implementazione segue la spec: il link nasce <c>Pending</c> e l'indice unico che
    /// produce il 409 è <b>parziale su InProgress</b>, quindi una seconda creazione non lo viola.
    /// Misurato in locale: <c>Assert.Throws() Failure: No exception was thrown</c>.</para>
    ///
    /// <para>L'invariante #10 è già coperta dove vive — <c>GameNightEventMaxLiveTests</c> (9
    /// asserzioni) più <c>GoLiveSessionCommandHandlerTests</c>,
    /// <c>StartGameNightSessionCommandHandlerTests</c>,
    /// <c>AttachGamebookCampaignToGameNightCommandHandlerTests</c>,
    /// <c>GoLiveSessionConcurrencyTests</c> — quindi il test precedente non aggiungeva
    /// copertura: aggiungeva un rosso permanente nella baseline di <c>dev-async</c> (#4024).</para>
    ///
    /// <para>Riscritto su ciò che il handler di creazione deve davvero garantire, e che nessun
    /// altro test asserisce: la creazione è <b>permessa</b>, e la serata resta <b>senza slot
    /// live occupato</b> — è questo che rende #10 e #13 coerenti fra loro.</para>
    /// </summary>
    [Fact]
    public async Task Handle_SecondSessionWhileFirstIsActive_IsAllowedAndLeavesLiveSlotFree()
    {
        // Arrange — una serata ad-hoc con una prima sessione (link Pending, Session Active).
        var (userId, gameId1) = await _fixture.SeedUserWithLibraryGameAndIndexedKbAsync(_dbContext!, vectorCount: 2);
        var first = await _handler!.Handle(BuildCommand(userId, gameId1), TestCancellationToken);

        var gameId2 = await _fixture.SeedAnotherLibraryGameAsync(_dbContext!, userId);

        // Act — invariante #13: la seconda creazione NON deve essere rifiutata.
        var second = await _handler!.Handle(
            BuildCommand(userId, gameId2, gameNightEventId: first.GameNightEventId),
            TestCancellationToken);

        // Assert — la seconda sessione esiste ed è attaccata alla stessa serata.
        second.Should().NotBeNull();
        second.SessionId.Should().NotBeEmpty().And.NotBe(first.SessionId);
        second.GameNightEventId.Should().Be(first.GameNightEventId);

        var night = await _dbContext!.GameNightEvents
            .AsNoTracking()
            .Include(e => e.Sessions)
            .FirstAsync(e => e.Id == first.GameNightEventId, TestCancellationToken);

        night.Sessions.Should().Contain(s => s.SessionId == first.SessionId);
        night.Sessions.Should().Contain(s => s.SessionId == second.SessionId);

        // 🔴 L'asserzione che tiene insieme #13 e #10, e senza la quale questo test direbbe solo
        // «non lancia»: nessuno dei due link è InProgress, quindi lo slot live è ancora libero e
        // l'invariante #10 non è stata aggirata — è stata semplicemente non toccata, perché
        // creare non è andare live.
        night.Sessions.Should().NotContain(s => s.Status == GameNightSessionStatus.InProgress.ToString());
    }

    /// <summary>
    /// Builds an isolated DI scope (fresh service provider) pointing at the same
    /// PostgreSQL database. Used to obtain a clean DbContext + handler stack between
    /// operations that mutate the same Session aggregate within a single test, in
    /// order to avoid stale [Timestamp] concurrency conflicts on the shared tracker.
    /// </summary>
    private ServiceProvider BuildIsolatedScope()
    {
        var services = IntegrationServiceCollectionBuilder.CreateBase(_isolatedDbConnectionString);
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Wires a fresh <see cref="CreateSessionCommandHandler"/> against the given DI scope.
    /// Mirrors the bootstrap done in <see cref="InitializeAsync"/>.
    /// </summary>
    private static CreateSessionCommandHandler BuildCreateHandler(
        ServiceProvider scope,
        MeepleAiDbContext db)
    {
        var eventCollector = scope.GetRequiredService<IDomainEventCollector>();
        var unitOfWork = scope.GetRequiredService<IUnitOfWork>();
        var mediator = scope.GetRequiredService<IMediator>();
        var sessionRepo = new SessionRepository(db, eventCollector);

        var quotaMock = new Mock<ISessionQuotaService>();
        quotaMock
            .Setup(s => s.CheckQuotaAsync(
                It.IsAny<Guid>(),
                It.IsAny<UserTier>(),
                It.IsAny<Role>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SessionQuotaResult.Allowed(0, 100));

        var loggerFactory = scope.GetRequiredService<ILoggerFactory>();
        return new CreateSessionCommandHandler(
            sessionRepo,
            unitOfWork,
            quotaMock.Object,
            db,
            mediator,
            loggerFactory.CreateLogger<CreateSessionCommandHandler>(),
            TimeProvider.System,
            new DiaryStreamService());
    }

    private static CreateSessionCommand BuildCommand(
        Guid userId,
        Guid gameId,
        Guid? gameNightEventId = null) =>
        new(
            UserId: userId,
            GameId: gameId,
            SessionType: nameof(Api.BoundedContexts.SessionTracking.Domain.Entities.SessionType.Generic),
            SessionDate: null,
            Location: null,
            Participants: new List<ParticipantDto>
            {
                new() { DisplayName = "Owner", IsOwner = true, UserId = userId }
            },
            GameNightEventId: gameNightEventId,
            GuestNames: null);
}
