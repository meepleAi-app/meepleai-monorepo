using Api.BoundedContexts.Testing.Application.Commands;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.GameManagement;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Infrastructure.Entities.UserLibrary;
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
/// Fixture della classe: host e database costruiti una volta sola. Il perche', i numeri e le
/// condizioni per applicare lo stesso schema altrove stanno in <see cref="IntegrationHostFixture"/>.
///
/// <para>
/// 🔴 <b>Perche' condividere il database e' sicuro QUI.</b> Ogni test usa il proprio
/// <c>testRunId</c> letterale, mai riutilizzato da un altro <c>[Fact]</c> in questa classe
/// (es. <c>"e2e-cleanupAaaaaa-..."</c> vs <c>"e2e-emptyscope0000-..."</c> vs
/// <c>"e2e-onlygn0000000-..."</c>). <c>CleanupTestEntitiesCommandHandler</c> filtra ogni cascade
/// per <c>TestRunId == request.TestRunId</c>, quindi i conteggi <c>Deleted*</c> non possono mai
/// includere le righe seminate da un altro test — il <c>TestRunId</c> funziona come chiave di
/// partizione, esattamente come <c>UserId</c> nel riferimento.
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
public sealed class CleanupTestEntitiesHostFixture(SharedTestcontainersFixture shared)
    : IntegrationHostFixture(shared, "test_cleanup");

/// <summary>
/// Issue #1928 Task B (DEC-B-1, DEC-B-3, DEC-B-8) + Issue #1929 Task C Macro 3a
/// (DEC-C-8) — Integration tests for CleanupTestEntitiesCommandHandler
/// cascade-by-TestRunId. Macro 3a extends cascade to UserLibraryEntries +
/// SharedGames; new test verifies library cascade.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Testing")]
[Trait("Issue", "1928")]
public sealed class CleanupTestEntitiesCommandHandlerTests : IClassFixture<CleanupTestEntitiesHostFixture>
{
    private readonly WebApplicationFactory<Program> _factory;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public CleanupTestEntitiesCommandHandlerTests(CleanupTestEntitiesHostFixture host)
    {
        _factory = host.Factory;
    }

    /// <summary>Seeds 1 GameNight Published + 1 RSVP player + 1 guest invitation + 1 live session + 1 library-game (Macro 3a).</summary>
    private async Task SeedFullScopeAsync(MeepleAiDbContext db, string testRunId)
    {
        var gnHandler = new SeedTestGameNightCommandHandler(db, NullLogger<SeedTestGameNightCommandHandler>.Instance);
        var sessHandler = new SeedTestSessionCommandHandler(db, NullLogger<SeedTestSessionCommandHandler>.Instance);
        var playerHandler = new SeedTestPlayerCommandHandler(db, NullLogger<SeedTestPlayerCommandHandler>.Instance);
        var libGameHandler = new SeedTestLibraryGameCommandHandler(db, NullLogger<SeedTestLibraryGameCommandHandler>.Instance);

        var gn = await gnHandler.Handle(new SeedTestGameNightCommand
        {
            TestRunId = testRunId,
            Status = "Published",
            OwnerEmail = $"owner-{testRunId[..16]}@e2e.test",
        }, TestCancellationToken);

        await sessHandler.Handle(new SeedTestSessionCommand
        {
            TestRunId = testRunId,
            GameNightId = gn.GameNightId,
            IsLive = true,
        }, TestCancellationToken);

        await playerHandler.Handle(new SeedTestPlayerCommand
        {
            TestRunId = testRunId,
            GameNightId = gn.GameNightId,
            Role = "player",
        }, TestCancellationToken);

        await playerHandler.Handle(new SeedTestPlayerCommand
        {
            TestRunId = testRunId,
            GameNightId = gn.GameNightId,
            Role = "guest",
            DisplayName = "E2E Guest",
        }, TestCancellationToken);

        // Issue #1929 Macro 3a — also seed library game (separate owner email to avoid User uniqueness collision).
        await libGameHandler.Handle(new SeedTestLibraryGameCommand
        {
            TestRunId = testRunId,
            OwnerEmail = $"libowner-{testRunId[..16]}@e2e.test",
        }, TestCancellationToken);
    }

    [Fact]
    public async Task Handle_HappyPath_DeletesAllScopedEntities_PreservesOthers()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var testRunIdA = "e2e-cleanupAaaaaa-1717603200000";
        var testRunIdB = "e2e-cleanupBbbbbb-1717603200000";

        await SeedFullScopeAsync(db, testRunIdA);
        await SeedFullScopeAsync(db, testRunIdB);

        var handler = new CleanupTestEntitiesCommandHandler(db, NullLogger<CleanupTestEntitiesCommandHandler>.Instance);
        var response = await handler.Handle(
            new CleanupTestEntitiesCommand { TestRunId = testRunIdA },
            TestCancellationToken);

        response.DeletedGameNights.Should().Be(1);
        response.DeletedSessions.Should().Be(1);
        response.DeletedInvitations.Should().Be(1);
        response.DeletedRsvps.Should().Be(1);
        response.DeletedUsers.Should().BeGreaterThanOrEqualTo(1);
        // Issue #1929 Macro 3a — library cascade assertions
        response.DeletedLibraryEntries.Should().Be(1);
        // Epic #3188 FIX 2 — SeedFullScopeAsync seeds an IsLive session, which now materializes its
        // own TestRunId-stamped SharedGame (for the live tracking Session FK) in addition to the
        // library game, so 2 shared games are cascade-deleted.
        response.DeletedSharedGames.Should().Be(2);
        // Issue #1929 Macro 4 (DEC-C-10 REVISION) — UserGameSessions not seeded in SeedFullScopeAsync
        response.DeletedUserGameSessions.Should().Be(0);

        // Scope A fully deleted
        (await db.GameNightEvents.AnyAsync(g => g.TestRunId == testRunIdA, TestCancellationToken))
            .Should().BeFalse();
        (await db.UserLibraryEntries.AnyAsync(e => e.TestRunId == testRunIdA, TestCancellationToken))
            .Should().BeFalse();
        (await db.SharedGames.AnyAsync(g => g.TestRunId == testRunIdA, TestCancellationToken))
            .Should().BeFalse();
        // Scope B preserved
        (await db.GameNightEvents.AnyAsync(g => g.TestRunId == testRunIdB, TestCancellationToken))
            .Should().BeTrue();
        (await db.UserLibraryEntries.AnyAsync(e => e.TestRunId == testRunIdB, TestCancellationToken))
            .Should().BeTrue();
        (await db.SharedGames.AnyAsync(g => g.TestRunId == testRunIdB, TestCancellationToken))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Handle_EmptyScope_TestRunIdNoEntities_ReturnsZeros()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var handler = new CleanupTestEntitiesCommandHandler(db, NullLogger<CleanupTestEntitiesCommandHandler>.Instance);
        var response = await handler.Handle(
            new CleanupTestEntitiesCommand { TestRunId = "e2e-emptyscope0000-1717603200000" },
            TestCancellationToken);

        response.DeletedGameNights.Should().Be(0);
        response.DeletedSessions.Should().Be(0);
        response.DeletedInvitations.Should().Be(0);
        response.DeletedRsvps.Should().Be(0);
        response.DeletedUsers.Should().Be(0);
        response.DeletedLibraryEntries.Should().Be(0);
        response.DeletedSharedGames.Should().Be(0);
        response.DeletedUserGameSessions.Should().Be(0);
    }

    [Fact]
    public async Task Handle_OnlyGameNight_NoChildren_DeletesGameNightAndUser()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var testRunId = "e2e-onlygn0000000-1717603200000";

        var gnHandler = new SeedTestGameNightCommandHandler(db, NullLogger<SeedTestGameNightCommandHandler>.Instance);
        await gnHandler.Handle(new SeedTestGameNightCommand
        {
            TestRunId = testRunId,
            Status = "Draft",
            OwnerEmail = "onlygn@e2e.test",
        }, TestCancellationToken);

        var handler = new CleanupTestEntitiesCommandHandler(db, NullLogger<CleanupTestEntitiesCommandHandler>.Instance);
        var response = await handler.Handle(
            new CleanupTestEntitiesCommand { TestRunId = testRunId },
            TestCancellationToken);

        response.DeletedGameNights.Should().Be(1);
        response.DeletedUsers.Should().Be(1);
        response.DeletedSessions.Should().Be(0);
    }

    [Fact]
    public async Task Handle_IdempotentRetry_SecondCallReturnsZeros()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var testRunId = "e2e-idempotent000-1717603200000";
        await SeedFullScopeAsync(db, testRunId);

        var handler = new CleanupTestEntitiesCommandHandler(db, NullLogger<CleanupTestEntitiesCommandHandler>.Instance);
        var first = await handler.Handle(new CleanupTestEntitiesCommand { TestRunId = testRunId }, TestCancellationToken);
        var second = await handler.Handle(new CleanupTestEntitiesCommand { TestRunId = testRunId }, TestCancellationToken);

        first.DeletedGameNights.Should().Be(1);
        second.DeletedGameNights.Should().Be(0);
        second.DeletedSessions.Should().Be(0);
        second.DeletedUsers.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ParallelCleanups_DifferentTestRunIds_NoCollision()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var testRunIdA = "e2e-parallelcleA0-1717603200000";
        var testRunIdB = "e2e-parallelcleB0-1717603200000";
        await SeedFullScopeAsync(db, testRunIdA);
        await SeedFullScopeAsync(db, testRunIdB);

        var handler = new CleanupTestEntitiesCommandHandler(db, NullLogger<CleanupTestEntitiesCommandHandler>.Instance);
        var rA = await handler.Handle(new CleanupTestEntitiesCommand { TestRunId = testRunIdA }, TestCancellationToken);
        var rB = await handler.Handle(new CleanupTestEntitiesCommand { TestRunId = testRunIdB }, TestCancellationToken);

        rA.DeletedGameNights.Should().Be(1);
        rB.DeletedGameNights.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ResponseShape_CorrectFields()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var testRunId = "e2e-shapecase0123-1717603200000";
        await SeedFullScopeAsync(db, testRunId);

        var handler = new CleanupTestEntitiesCommandHandler(db, NullLogger<CleanupTestEntitiesCommandHandler>.Instance);
        var response = await handler.Handle(new CleanupTestEntitiesCommand { TestRunId = testRunId }, TestCancellationToken);

        response.TestRunId.Should().Be(testRunId);
        response.DurationMs.Should().BeGreaterThanOrEqualTo(0);
    }

    /// <summary>
    /// Issue #1929 Task C Macro 3a (DEC-C-8) — Library-only cascade scope verifies
    /// UserLibraryEntries + SharedGames are deleted by TestRunId without depending
    /// on GameNight entities being present.
    /// </summary>
    [Fact]
    public async Task Handle_LibraryGameOnly_CascadesLibraryEntryAndSharedGame()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var testRunIdA = "e2e-libonlyAaaaa0-1717603200000";
        var testRunIdB = "e2e-libonlyBbbbb0-1717603200000";

        var libHandler = new SeedTestLibraryGameCommandHandler(db, NullLogger<SeedTestLibraryGameCommandHandler>.Instance);
        await libHandler.Handle(new SeedTestLibraryGameCommand
        {
            TestRunId = testRunIdA,
            OwnerEmail = "libA@e2e.test",
        }, TestCancellationToken);
        await libHandler.Handle(new SeedTestLibraryGameCommand
        {
            TestRunId = testRunIdB,
            OwnerEmail = "libB@e2e.test",
        }, TestCancellationToken);

        var handler = new CleanupTestEntitiesCommandHandler(db, NullLogger<CleanupTestEntitiesCommandHandler>.Instance);
        var response = await handler.Handle(
            new CleanupTestEntitiesCommand { TestRunId = testRunIdA },
            TestCancellationToken);

        response.DeletedLibraryEntries.Should().Be(1);
        response.DeletedSharedGames.Should().Be(1);
        response.DeletedUsers.Should().Be(1);
        response.DeletedGameNights.Should().Be(0);
        // Issue #1929 Macro 4 (DEC-C-10 REVISION) — no UserGameSessions seeded in this test
        response.DeletedUserGameSessions.Should().Be(0);

        // Scope A library fully deleted
        (await db.UserLibraryEntries.AnyAsync(e => e.TestRunId == testRunIdA, TestCancellationToken))
            .Should().BeFalse();
        (await db.SharedGames.AnyAsync(g => g.TestRunId == testRunIdA, TestCancellationToken))
            .Should().BeFalse();
        // Scope B library preserved
        (await db.UserLibraryEntries.AnyAsync(e => e.TestRunId == testRunIdB, TestCancellationToken))
            .Should().BeTrue();
        (await db.SharedGames.AnyAsync(g => g.TestRunId == testRunIdB, TestCancellationToken))
            .Should().BeTrue();
    }
}
