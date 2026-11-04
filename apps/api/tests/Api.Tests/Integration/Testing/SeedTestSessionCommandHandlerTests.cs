using Api.BoundedContexts.GameManagement.Application.Queries.GameNights;
using Api.BoundedContexts.Testing.Application.Commands;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.Integration.Testing;

/// <summary>
/// Fixture della classe: host e database costruiti una volta sola. Il perche', i numeri e le
/// condizioni per applicare lo stesso schema altrove stanno in <see cref="IntegrationHostFixture"/>.
///
/// <para>
/// 🔴 <b>Perche' condividere il database e' sicuro QUI.</b> Ogni test usa il proprio
/// <c>testRunId</c> letterale, mai riutilizzato da un altro <c>[Fact]</c> in questa classe
/// (es. <c>"e2e-sessionlive11-..."</c> vs <c>"e2e-livequery1234-..."</c> vs
/// <c>"e2e-notfound00001-..."</c>), e ogni asserzione legge o il record appena creato via
/// <c>response.SessionId</c> (Guid fresco) o filtra per quel <c>testRunId</c>. Il test
/// <c>Handle_IsLiveTrue_GetGameNightLive_ReportsSessionIsLive</c> interroga
/// <c>GetGameNightLiveQuery</c> scoped al <c>GameNightId</c> seminato dal test stesso, quindi non
/// puo' vedere sessioni di altri test.
/// </para>
/// <para>
/// Nota: la classe passava da <c>EnsureCreatedAsync</c> a schema-da-modello; la fixture condivisa
/// usa sempre <c>MigrateAsync</c> (vedi <see cref="IntegrationHostFixture"/>), verificato qui non
/// avere effetti collaterali sui test.
/// </para>
///
/// ⚠️ <b>Vincolo sull'asse di scrittura, non solo di lettura.</b> Il seeder inserisce l'utente
/// proprietario con un <c>Add</c> cieco, senza find-or-create, contro l'indice unico su
/// <c>UserEntity.Email</c>. Con un database per test una collisione era strutturalmente
/// impossibile; con uno condiviso diventa un 23505. Le email derivano da
/// <c>testRunId[..16]</c>: due <c>testRunId</c> che coincidono nei primi 16 caratteri
/// collidono. Aggiungendo test a questa classe, le email letterali devono restare
/// globalmente uniche all'interno della classe.
/// </summary>
public sealed class SeedTestSessionHostFixture(SharedTestcontainersFixture shared)
    : IntegrationHostFixture(shared, "test_seed_session");

/// <summary>
/// Issue #1928 Task B (DEC-B-1, DEC-B-8) — Integration tests for
/// SeedTestSessionCommandHandler. Pattern reuse from T1.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Testing")]
[Trait("Issue", "1928")]
public sealed class SeedTestSessionCommandHandlerTests : IClassFixture<SeedTestSessionHostFixture>
{
    private readonly WebApplicationFactory<Program> _factory;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public SeedTestSessionCommandHandlerTests(SeedTestSessionHostFixture host)
    {
        _factory = host.Factory;
    }

    private async Task<Guid> SeedParentGameNightAsync(MeepleAiDbContext db, string testRunId)
    {
        var gnHandler = new SeedTestGameNightCommandHandler(db, NullLogger<SeedTestGameNightCommandHandler>.Instance);
        var gnResponse = await gnHandler.Handle(new SeedTestGameNightCommand
        {
            TestRunId = testRunId,
            Status = "Published",
            OwnerEmail = $"owner-{testRunId[..16]}@e2e.test",
        }, TestCancellationToken);
        return gnResponse.GameNightId;
    }

    [Fact]
    public async Task Handle_IsLiveTrue_CreatesSessionWithStartedAtSetAndCompletedAtNull()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var testRunId = "e2e-sessionlive11-1717603200000";
        var gameNightId = await SeedParentGameNightAsync(db, testRunId);

        var handler = new SeedTestSessionCommandHandler(db, NullLogger<SeedTestSessionCommandHandler>.Instance);
        var cmd = new SeedTestSessionCommand
        {
            TestRunId = testRunId,
            GameNightId = gameNightId,
            IsLive = true,
            ScoreType = "Points",
        };

        var response = await handler.Handle(cmd, TestCancellationToken);

        var session = await db.Set<GameNightSessionEntity>()
            .SingleAsync(s => s.Id == response.SessionId, TestCancellationToken);
        session.StartedAt.Should().NotBeNull();
        session.CompletedAt.Should().BeNull();
        session.TestRunId.Should().Be(testRunId);
        session.Status.Should().Be("InProgress");

        // Epic #3188 FIX 2: the link being InProgress is not enough — under D4, GetGameNightLive
        // derives IsLive from the tracking Session. Assert the seeded tracking Session is live
        // (started_at != null && finalized_at == null) so the fixture is truthful.
        var trackingSession = await db.SessionTrackingSessions.AsNoTracking()
            .SingleAsync(s => s.Id == response.SessionId, TestCancellationToken);
        trackingSession.StartedAt.Should().NotBeNull();
        trackingSession.FinalizedAt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_IsLiveTrue_GetGameNightLive_ReportsSessionIsLive()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var testRunId = "e2e-livequery1234-1717603200000";

        // Seed the parent night and keep the organizer id — the D4 live read is participant-guarded.
        var gnHandler = new SeedTestGameNightCommandHandler(db, NullLogger<SeedTestGameNightCommandHandler>.Instance);
        var gn = await gnHandler.Handle(new SeedTestGameNightCommand
        {
            TestRunId = testRunId,
            Status = "Published",
            OwnerEmail = $"owner-{testRunId[..16]}@e2e.test",
        }, TestCancellationToken);

        var handler = new SeedTestSessionCommandHandler(db, NullLogger<SeedTestSessionCommandHandler>.Instance);
        var response = await handler.Handle(new SeedTestSessionCommand
        {
            TestRunId = testRunId,
            GameNightId = gn.GameNightId,
            IsLive = true,
        }, TestCancellationToken);

        // Resolve the read model end-to-end via a fresh scope + mediator (full DI) and assert it
        // reports the seeded session as live — the D4 truthfulness contract this fix restores.
        using var readScope = _factory.Services.CreateScope();
        var mediator = readScope.ServiceProvider.GetRequiredService<IMediator>();
        var live = await mediator.Send(
            new GetGameNightLiveQuery(gn.GameNightId, gn.OwnerId), TestCancellationToken);

        live.Sessions.Should().ContainSingle(s => s.SessionId == response.SessionId)
            .Which.IsLive.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_IsLiveFalse_CreatesSessionWithStartedAtNull()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var testRunId = "e2e-sessionoff112-1717603200000";
        var gameNightId = await SeedParentGameNightAsync(db, testRunId);

        var handler = new SeedTestSessionCommandHandler(db, NullLogger<SeedTestSessionCommandHandler>.Instance);
        var cmd = new SeedTestSessionCommand
        {
            TestRunId = testRunId,
            GameNightId = gameNightId,
            IsLive = false,
        };

        var response = await handler.Handle(cmd, TestCancellationToken);

        var session = await db.Set<GameNightSessionEntity>()
            .SingleAsync(s => s.Id == response.SessionId, TestCancellationToken);
        session.StartedAt.Should().BeNull();
        session.CompletedAt.Should().BeNull();
        session.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task Handle_GameNightNotFound_ThrowsNotFoundException()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var handler = new SeedTestSessionCommandHandler(db, NullLogger<SeedTestSessionCommandHandler>.Instance);
        var cmd = new SeedTestSessionCommand
        {
            TestRunId = "e2e-notfound00001-1717603200000",
            GameNightId = Guid.NewGuid(),
            IsLive = false,
        };

        Func<Task> act = async () => await handler.Handle(cmd, TestCancellationToken);

        await act.Should().ThrowAsync<Api.Middleware.Exceptions.NotFoundException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task Handle_MultipleSessions_IncrementPlayOrder()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var testRunId = "e2e-multisess1234-1717603200000";
        var gameNightId = await SeedParentGameNightAsync(db, testRunId);

        var handler = new SeedTestSessionCommandHandler(db, NullLogger<SeedTestSessionCommandHandler>.Instance);
        var cmd1 = new SeedTestSessionCommand
        {
            TestRunId = testRunId,
            GameNightId = gameNightId,
            IsLive = false,
        };
        var cmd2 = new SeedTestSessionCommand
        {
            TestRunId = testRunId,
            GameNightId = gameNightId,
            IsLive = false,
        };

        var r1 = await handler.Handle(cmd1, TestCancellationToken);
        var r2 = await handler.Handle(cmd2, TestCancellationToken);

        var s1 = await db.Set<GameNightSessionEntity>().SingleAsync(s => s.Id == r1.SessionId, TestCancellationToken);
        var s2 = await db.Set<GameNightSessionEntity>().SingleAsync(s => s.Id == r2.SessionId, TestCancellationToken);
        s2.PlayOrder.Should().BeGreaterThan(s1.PlayOrder);
    }

    [Fact]
    public async Task Handle_TestRunIdStamp_AppliedToSession()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var testRunId = "e2e-stampcase0123-1717603200000";
        var gameNightId = await SeedParentGameNightAsync(db, testRunId);

        var handler = new SeedTestSessionCommandHandler(db, NullLogger<SeedTestSessionCommandHandler>.Instance);
        var cmd = new SeedTestSessionCommand
        {
            TestRunId = testRunId,
            GameNightId = gameNightId,
            IsLive = true,
        };

        var response = await handler.Handle(cmd, TestCancellationToken);

        var session = await db.Set<GameNightSessionEntity>()
            .SingleAsync(s => s.Id == response.SessionId, TestCancellationToken);
        session.TestRunId.Should().Be(testRunId);
    }

    [Fact]
    public async Task Handle_ResponseShape_CorrectFields()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var testRunId = "e2e-shapecase0123-1717603200000";
        var gameNightId = await SeedParentGameNightAsync(db, testRunId);

        var handler = new SeedTestSessionCommandHandler(db, NullLogger<SeedTestSessionCommandHandler>.Instance);
        var cmd = new SeedTestSessionCommand
        {
            TestRunId = testRunId,
            GameNightId = gameNightId,
            IsLive = true,
        };

        var response = await handler.Handle(cmd, TestCancellationToken);

        response.SessionId.Should().NotBe(Guid.Empty);
        response.GameNightId.Should().Be(gameNightId);
        response.IsLive.Should().BeTrue();
        response.TestRunId.Should().Be(testRunId);
    }
}
