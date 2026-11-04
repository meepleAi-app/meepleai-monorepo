using Api.BoundedContexts.Testing.Application.Commands;
using Api.BoundedContexts.Testing.Infrastructure;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.GameManagement;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.Integration.Testing;

/// <summary>
/// La fixture di <see cref="TestSeedConstraintViolationIntegrationTests"/>: host per classe,
/// database fresco per test (#4050).
/// </summary>
public sealed class TestSeedConstraintHostFixture(SharedTestcontainersFixture shared)
    : SharedHostPerTestDatabaseFixture(shared, "t4054_seedconflict");

/// <summary>
/// Issue #4054 — un vincolo violato dai seeder E2E deve arrivare al chiamante come 4xx che nomina
/// il campo, non come <c>internal_server_error</c>.
/// </summary>
/// <remarks>
/// <para>
/// Questi test girano su Postgres reale e non su InMemory per una ragione che decide il risultato:
/// il provider InMemory <b>non ha indici unici né chiavi esterne</b>, quindi il secondo INSERT
/// passa e il difetto non esiste affatto. La prova che il 500 c'era, e che ora non c'è più, non è
/// esprimibile senza il vincolo vero — e lo stesso vale per la domanda su cui poggia il messaggio:
/// se i campi <c>constraint_name</c>/<c>table_name</c> di Postgres sopravvivano alla redazione del
/// DETAIL. Qui si misura, non si assume.
/// </para>
/// </remarks>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Testing")]
[Trait("Issue", "4054")]
public sealed class TestSeedConstraintViolationIntegrationTests
    : IClassFixture<TestSeedConstraintHostFixture>, IAsyncLifetime
{
    private readonly TestSeedConstraintHostFixture _hostFixture;
    private WebApplicationFactory<Program> _factory = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public TestSeedConstraintViolationIntegrationTests(TestSeedConstraintHostFixture hostFixture)
    {
        _hostFixture = hostFixture;
    }

    public async ValueTask InitializeAsync()
    {
        // 🔴 BeginTestAsync DEVE precedere ogni uso di Factory: prima di questa riga l'host punta
        // ancora al database di avvio della fixture, e un seed scritto lì non lo vedrebbe nessuno.
        await _hostFixture.BeginTestAsync();
        _factory = _hostFixture.Factory;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private IServiceScope NewScope() => _factory.Services.CreateScope();

    private static MeepleAiDbContext Db(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

    private static SeedTestGameNightCommandHandler GameNightHandler(MeepleAiDbContext db) =>
        new(db, NullLogger<SeedTestGameNightCommandHandler>.Instance);

    private static SeedTestPlayerCommandHandler PlayerHandler(MeepleAiDbContext db) =>
        new(db, NullLogger<SeedTestPlayerCommandHandler>.Instance);

    private static UserEntity NewUser(string email, string testRunId) => new()
    {
        Id = Guid.NewGuid(),
        Email = email,
        DisplayName = "Seed conflict probe",
        PasswordHash = null!,
        Role = "user",
        Tier = "free",
        CreatedAt = DateTime.UtcNow,
        EmailVerified = true,
        Language = "en",
        EmailNotifications = true,
        Theme = "system",
        DataRetentionDays = 90,
        TestRunId = testRunId,
    };

    // ── 1. Il difetto di #4054, sull'endpoint da cui è stato osservato ───────────────────────

    [Fact]
    public async Task SeedGameNight_WithOwnerEmailAlreadyTaken_Throws409NamingTheField()
    {
        const string testRunId = "e2e-dupowneremail01-1717603200000";
        const string email = "anna.host@meepleai.test";

        using (var first = NewScope())
        {
            await GameNightHandler(Db(first)).Handle(
                new SeedTestGameNightCommand
                {
                    TestRunId = testRunId,
                    Status = "Published",
                    OwnerEmail = email,
                },
                Ct);
        }

        using var second = NewScope();
        var handler = GameNightHandler(Db(second));
        var retry = new SeedTestGameNightCommand
        {
            // Un testRunId diverso è il caso reale: ogni spec Playwright ne genera uno nuovo, ma
            // tre spec cross-asse condividono lo stesso ANNA_PERSONA.email.
            TestRunId = "e2e-dupowneremail02-1717603200001",
            Status = "Published",
            OwnerEmail = email,
        };

        var thrown = await handler.Invoking(h => h.Handle(retry, Ct))
            .Should().ThrowAsync<ConflictException>();

        thrown.Which.StatusCode.Should().Be(409);
        thrown.Which.ErrorCode.Should().Be("conflict");
        thrown.Which.Message.Should().Contain("ownerEmail")
            .And.Contain(email)
            .And.Contain("CREATES a new host user");
    }

    [Fact]
    public async Task SeedGameNight_WithFreshOwnerEmail_StillSucceeds()
    {
        // La guardia non deve rifiutare il caso normale: senza questo, un `AnyAsync` scritto male
        // (per esempio senza il confronto sull'email) renderebbe l'endpoint inutilizzabile e il
        // test del conflitto resterebbe comunque verde.
        using var scope = NewScope();
        var db = Db(scope);

        var response = await GameNightHandler(db).Handle(
            new SeedTestGameNightCommand
            {
                TestRunId = "e2e-freshowner00001-1717603200000",
                Status = "Draft",
                OwnerEmail = "fresh-owner@e2e.test",
            },
            Ct);

        response.GameNightId.Should().NotBe(Guid.Empty);
        (await db.Users.CountAsync(u => u.Email == "fresh-owner@e2e.test", Ct)).Should().Be(1);
    }

    // ── 2. Lo stesso difetto sull'altro endpoint che accetta un campo vincolato ──────────────

    [Fact]
    public async Task SeedPlayer_WithUserIdAlreadyOnRoster_Throws409NamingTheField()
    {
        const string testRunId = "e2e-duprsvpuserid01-1717603200000";
        Guid gameNightId;

        using (var setup = NewScope())
        {
            var db = Db(setup);
            var night = await GameNightHandler(db).Handle(
                new SeedTestGameNightCommand
                {
                    TestRunId = testRunId,
                    Status = "Published",
                    OwnerEmail = "roster-host@e2e.test",
                },
                Ct);
            gameNightId = night.GameNightId;
        }

        var playerUserId = Guid.NewGuid();
        var rsvp = new SeedTestPlayerCommand
        {
            TestRunId = testRunId,
            GameNightId = gameNightId,
            Role = "player",
            UserId = playerUserId,
        };

        using (var firstRsvp = NewScope())
        {
            await PlayerHandler(Db(firstRsvp)).Handle(rsvp, Ct);
        }

        using var retry = NewScope();
        var thrown = await PlayerHandler(Db(retry)).Invoking(h => h.Handle(rsvp, Ct))
            .Should().ThrowAsync<ConflictException>();

        thrown.Which.StatusCode.Should().Be(409);
        thrown.Which.ErrorCode.Should().Be("conflict");
        thrown.Which.Message.Should().Contain("userId")
            .And.Contain(playerUserId.ToString())
            .And.Contain(gameNightId.ToString());
    }

    // ── 3. La rete: quello che le guardie pre-INSERT non possono coprire ────────────────────

    [Fact]
    public async Task SaveSeedAsync_OnUniqueViolation_Throws409CarryingTheConstraintName()
    {
        const string email = "race-winner@e2e.test";

        using (var winner = NewScope())
        {
            Db(winner).Users.Add(NewUser(email, "e2e-racewinner00001-1717603200000"));
            await Db(winner).SaveChangesAsync(Ct);
        }

        // Il caso che nessuna guardia pre-INSERT può chiudere: il controllo è già passato e la riga
        // concorrente arriva dopo. Qui è serializzato per renderlo deterministico, ma è la stessa
        // finestra che due worker Playwright aprono davvero.
        using var loser = NewScope();
        var db = Db(loser);
        db.Users.Add(NewUser(email, "e2e-raceloser000001-1717603200001"));

        var thrown = await db.Invoking(d => d.SaveSeedAsync(Ct))
            .Should().ThrowAsync<ConflictException>();

        thrown.Which.StatusCode.Should().Be(409);
        thrown.Which.ErrorCode.Should().Be("conflict");
        // La misura che il messaggio richiede: i campi constraint_name/table_name di Postgres
        // sopravvivono alla redazione del DETAIL, quindi nominano la colonna anche quando
        // «Key (email)=(…) already exists» non è disponibile.
        thrown.Which.Message.Should().Contain("IX_users_Email").And.Contain("users");
    }

    [Fact]
    public async Task SaveChangesAsync_OnTheSameUniqueViolation_IsTheOpaque500ThisFixed()
    {
        // Il CONTROLLO del test precedente: senza la traduzione la stessa situazione produce una
        // DbUpdateException, che ApiExceptionHandlerMiddleware non riconosce e manda nel fallback
        // 500 `internal_server_error`. Se un giorno EF iniziasse a tradurla da sé, questo test
        // diventerebbe rosso ed è il momento di togliere la rete, non di allargarla.
        const string email = "control-case@e2e.test";

        using (var first = NewScope())
        {
            Db(first).Users.Add(NewUser(email, "e2e-controlfirst0001-1717603200000"));
            await Db(first).SaveChangesAsync(Ct);
        }

        using var second = NewScope();
        var db = Db(second);
        db.Users.Add(NewUser(email, "e2e-controlsecond001-1717603200001"));

        var thrown = await db.Invoking(d => d.SaveChangesAsync(Ct))
            .Should().ThrowAsync<DbUpdateException>();

        thrown.Which.Should().NotBeAssignableTo<HttpException>();
        // Non basta `DbUpdateException`: il middleware tratta la SOTTOCLASSE
        // DbUpdateConcurrencyException come 409 (riga 132 di ApiExceptionHandlerMiddleware), e
        // un'asserzione sulla classe base passerebbe anche in quel caso — cioè anche se il 500 non
        // ci fosse mai stato. È l'assenza di quella sottoclasse a provare il fallback.
        thrown.Which.Should().NotBeOfType<DbUpdateConcurrencyException>();
    }

    [Fact]
    public async Task SaveSeedAsync_OnForeignKeyViolation_Throws400CarryingTheConstraintName()
    {
        using var scope = NewScope();
        var db = Db(scope);

        // event_id è l'unica FK di game_night_rsvps (user_id non ne ha): puntarla a una serata
        // inesistente è il modo più diretto di produrre un 23503 dal grafo dei seeder.
        db.Set<GameNightRsvpEntity>().Add(new GameNightRsvpEntity
        {
            Id = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Status = "Accepted",
            RespondedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            TestRunId = "e2e-fkviolation0001-1717603200000",
        });

        var thrown = await db.Invoking(d => d.SaveSeedAsync(Ct))
            .Should().ThrowAsync<BadRequestException>();

        thrown.Which.StatusCode.Should().Be(400);
        thrown.Which.ErrorCode.Should().Be("bad_request");
        thrown.Which.Message.Should().Contain("game_night_rsvps");
    }
}
