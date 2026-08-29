using Api.BoundedContexts.SharedGameCatalog.Application.Commands.Golden;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Seeding;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Infrastructure.Seeders;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Interfaces;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.SharedGameCatalog.Seeding;

/// <summary>
/// Tests for <see cref="PuertoRicoGoldenSeeder"/> (ADR-051 Sprint 2 / Task 3).
/// Combines unit tests (mock IMediator) and an integration test (Testcontainers Postgres)
/// covering: idempotency, missing-shared-game guard, embedded JSON load, and skip flag.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class PuertoRicoGoldenSeederTests : IAsyncLifetime
{
    /// <summary>
    /// BGG ID for Puerto Rico (matches seed manifest entries in dev.yml/prod.yml/staging.yml).
    /// </summary>
    private const int PuertoRicoBggId = 3076;

    private readonly SharedTestcontainersFixture _fixture;
    private MeepleAiDbContext _dbContext = null!;
    private string _databaseName = null!;
    private string _connectionString = null!;

    public PuertoRicoGoldenSeederTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_pr_seeder_{Guid.NewGuid():N}";
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        _dbContext = _fixture.CreateDbContext(_connectionString);
        await _dbContext.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    // ============================================================
    // Unit-only tests (LoadEmbedded* + SeedAsync_WhenSkipFlagTrue_*)
    // have been moved to PuertoRicoGoldenSeederUnitTests so they no
    // longer trigger the Integration-GroupC Testcontainers fixture on
    // the Backend Fast CI job. Issue #1873 review follow-up.
    // ============================================================

    // ============================================================
    // Integration: real Postgres + EF + MediatR mock
    // ============================================================

    [Fact]
    [Trait("Category", TestCategories.Integration)]
    public async Task SeedAsync_WhenSharedGameMissing_NoOpsAndSendsNoCommands()
    {
        // Arrange: no Puerto Rico SharedGame seeded.
        var mediator = new Mock<IMediator>(MockBehavior.Strict);

        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(new ConfigurationBuilder().Build())
            .AddSingleton<IMediator>(mediator.Object)
            .BuildServiceProvider();

        var context = new SeedContext(
            Profile: SeedProfile.Dev,
            DbContext: _dbContext,
            Services: services,
            Logger: NullLogger.Instance,
            SystemUserId: Guid.NewGuid());

        var seeder = new PuertoRicoGoldenSeeder();

        // Act
        await seeder.SeedAsync(context, default);

        // Assert: no claims persisted, no MediatR dispatches.
        mediator.VerifyNoOtherCalls();
        var totalClaims = await _dbContext.Set<MechanicGoldenClaimEntity>().CountAsync();
        totalClaims.Should().Be(0);
    }

    [Fact]
    [Trait("Category", TestCategories.Integration)]
    public async Task SeedAsync_PopulatesAllClaims_AndIsIdempotent()
    {
        // Arrange: seed Puerto Rico SharedGame so the seeder can find it.
        var prSharedGameId = await SeedPuertoRicoSharedGameAsync();

        // Seed the system user referenced by curator_user_id FK (CLAUDE.md issue #2620).
        var systemUserId = Guid.NewGuid();
        await SeedSystemUserAsync(systemUserId);

        // We use a real handler-style stub: each command immediately persists a minimal
        // MechanicGoldenClaimEntity with a unique Id, mirroring what the production
        // CreateMechanicGoldenClaimHandler would do (without computing real embeddings).
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(
                It.IsAny<CreateMechanicGoldenClaimCommand>(),
                It.IsAny<CancellationToken>()))
            .Returns<CreateMechanicGoldenClaimCommand, CancellationToken>(async (cmd, ct) =>
            {
                var id = Guid.NewGuid();
                _dbContext.Set<MechanicGoldenClaimEntity>().Add(new MechanicGoldenClaimEntity
                {
                    Id = id,
                    SharedGameId = cmd.SharedGameId,
                    Section = (int)cmd.Section,
                    Statement = cmd.Statement,
                    ExpectedPage = cmd.ExpectedPage,
                    SourceQuote = cmd.SourceQuote,
                    KeywordsJson = "[]",
                    CuratorUserId = cmd.CuratorUserId,
                    CreatedAt = DateTime.UtcNow,
                });
                await _dbContext.SaveChangesAsync(ct);
                return id;
            });

        var services = BuildServices(mediator.Object, _dbContext);

        var context = new SeedContext(
            Profile: SeedProfile.Dev,
            DbContext: _dbContext,
            Services: services,
            Logger: NullLogger.Instance,
            SystemUserId: systemUserId);

        var seeder = new PuertoRicoGoldenSeeder();

        // Act: first run inserts all claims.
        await seeder.SeedAsync(context, default);

        var firstCount = await _dbContext.Set<MechanicGoldenClaimEntity>()
            .CountAsync(c => c.SharedGameId == prSharedGameId);
        firstCount.Should().BeGreaterThanOrEqualTo(50,
            "fixture has ≥50 curated claims; all should land in the DB on first run");

        // Act: second run is a no-op.
        _dbContext.ChangeTracker.Clear();
        await seeder.SeedAsync(context, default);

        var secondCount = await _dbContext.Set<MechanicGoldenClaimEntity>()
            .CountAsync(c => c.SharedGameId == prSharedGameId);
        secondCount.Should().Be(firstCount, "second run must be a no-op when already seeded");

        // The mediator should have been called exactly firstCount times — only on the
        // first run. (Idempotency check on the second run skips before any dispatch.)
        mediator.Verify(
            m => m.Send(It.IsAny<CreateMechanicGoldenClaimCommand>(), It.IsAny<CancellationToken>()),
            Times.Exactly(firstCount));
    }

    [Fact]
    [Trait("Category", TestCategories.Integration)]
    public async Task SeedAsync_DispatchesCommandWithCorrectShape()
    {
        // Sanity that the JSON → DTO → command conversion preserves field semantics.
        var prSharedGameId = await SeedPuertoRicoSharedGameAsync();
        var systemUserId = Guid.NewGuid();
        await SeedSystemUserAsync(systemUserId);

        var captured = new List<CreateMechanicGoldenClaimCommand>();
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(
                It.IsAny<CreateMechanicGoldenClaimCommand>(),
                It.IsAny<CancellationToken>()))
            .Returns<CreateMechanicGoldenClaimCommand, CancellationToken>(async (cmd, ct) =>
            {
                captured.Add(cmd);
                // Persist a minimal entity so idempotency check would trip on retry.
                _dbContext.Set<MechanicGoldenClaimEntity>().Add(new MechanicGoldenClaimEntity
                {
                    Id = Guid.NewGuid(),
                    SharedGameId = cmd.SharedGameId,
                    Section = (int)cmd.Section,
                    Statement = cmd.Statement,
                    ExpectedPage = cmd.ExpectedPage,
                    SourceQuote = cmd.SourceQuote,
                    KeywordsJson = "[]",
                    CuratorUserId = cmd.CuratorUserId,
                    CreatedAt = DateTime.UtcNow,
                });
                await _dbContext.SaveChangesAsync(ct);
                return Guid.NewGuid();
            });

        var services = BuildServices(mediator.Object, _dbContext);

        var context = new SeedContext(
            Profile: SeedProfile.Dev,
            DbContext: _dbContext,
            Services: services,
            Logger: NullLogger.Instance,
            SystemUserId: systemUserId);

        var seeder = new PuertoRicoGoldenSeeder();
        await seeder.SeedAsync(context, default);

        captured.Should().NotBeEmpty();
        captured.Should().AllSatisfy(c =>
        {
            c.SharedGameId.Should().Be(prSharedGameId);
            c.CuratorUserId.Should().Be(systemUserId);
            c.Statement.Should().NotBeNullOrWhiteSpace();
            c.SourceQuote.Should().NotBeNullOrWhiteSpace();
            c.ExpectedPage.Should().BeGreaterThanOrEqualTo(1);
            Enum.IsDefined(typeof(MechanicSection), c.Section).Should().BeTrue();
        });

        // Spot-check: la fixture copre le sei sezioni originali.
        //
        // #3883: il commento diceva "0..5" e il ciclo iterava Enum.GetValues<MechanicSection>().
        // Le due cose coincidevano quando il test e' stato scritto; poi la v1.1.0 (#539 follow-up)
        // ha APPESO Setup = 6, Components = 7, EndgameScoring = 8, e il ciclo ha cominciato a
        // pretendere dalla fixture una copertura che nessuno le aveva chiesto. Il test e' invecchiato
        // sotto un'estensione legittima dell'enum — e sarebbe invecchiato di nuovo alla successiva.
        //
        // L'insieme e' ora esplicito: dice quale contratto la fixture ha davvero, e un'aggiunta
        // all'enum non lo rompe. Se un giorno la fixture dovra' coprire anche Setup, la riga da
        // cambiare e' questa, e la si cambia sapendo cosa si sta chiedendo.
        var contractedSections = new[]
        {
            MechanicSection.Summary,
            MechanicSection.Mechanics,
            MechanicSection.Victory,
            MechanicSection.Resources,
            MechanicSection.Phases,
            MechanicSection.Faq,
        };

        var seenSections = captured.Select(c => c.Section).Distinct().ToHashSet();
        foreach (var section in contractedSections)
        {
            seenSections.Should().Contain(section,
                $"Puerto Rico fixture is required to cover MechanicSection.{section}");
        }
    }

    // ============================================================
    // Helpers
    // ============================================================

    /// <summary>
    /// Seeds a minimal <see cref="UserEntity"/> with the supplied id so the
    /// <c>mechanic_golden_claims.curator_user_id → users.Id</c> FK is satisfied
    /// when the mock IMediator handler persists claims directly.
    /// </summary>
    private async Task SeedSystemUserAsync(Guid userId)
    {
        _dbContext.Users.Add(new UserEntity
        {
            Id = userId,
            Email = $"system-{userId:N}@test.local",
            PasswordHash = "hash",
        });
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    private async Task<Guid> SeedPuertoRicoSharedGameAsync()
    {
        var sharedGameId = Guid.NewGuid();
        _dbContext.SharedGames.Add(new SharedGameEntity
        {
            Id = sharedGameId,
            BggId = PuertoRicoBggId,
            Title = "Puerto Rico",
            Description = "Seeder integration test stub.",
            ImageUrl = string.Empty,
            ThumbnailUrl = string.Empty,
            YearPublished = 2002,
            MinPlayers = 3,
            MaxPlayers = 5,
            PlayingTimeMinutes = 90,
            MinAge = 12,
            Status = 1,
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
        });
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
        return sharedGameId;
    }

    /// <summary>
    /// Builds the DI container the seeder consumes via <c>SeedContext.Services</c>.
    /// Registers the real <see cref="MechanicGoldenBggTagRepository"/> (Task 4) so the
    /// BGG-tags phase can persist against the integration-test Postgres.
    /// </summary>
    private static IServiceProvider BuildServices(IMediator mediator, MeepleAiDbContext dbContext)
    {
        var eventCollector = new Mock<IDomainEventCollector>();
        eventCollector
            .Setup(e => e.GetAndClearEvents())
            .Returns(new List<IDomainEvent>().AsReadOnly());

        return new ServiceCollection()
            .AddSingleton<IConfiguration>(new ConfigurationBuilder().Build())
            .AddSingleton(mediator)
            .AddSingleton(dbContext)
            .AddSingleton(eventCollector.Object)
            .AddSingleton<IMechanicGoldenBggTagRepository, MechanicGoldenBggTagRepository>()
            .BuildServiceProvider();
    }

    // ============================================================
    // Task 4: BGG tags seeder (Integration tests only;
    // LoadEmbeddedBggTagsForTest_ReturnsParsedFixture lives in
    // PuertoRicoGoldenSeederUnitTests — see header comment above.)
    // ============================================================

    [Fact]
    [Trait("Category", TestCategories.Integration)]
    public async Task SeedAsync_PopulatesBggTagsTable()
    {
        // Arrange.
        var prSharedGameId = await SeedPuertoRicoSharedGameAsync();
        var systemUserId = Guid.NewGuid();
        await SeedSystemUserAsync(systemUserId);

        // Wire a mediator stub that persists claims directly so the claims phase
        // succeeds end-to-end (mirrors SeedAsync_PopulatesAllClaims_AndIsIdempotent).
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(
                It.IsAny<CreateMechanicGoldenClaimCommand>(),
                It.IsAny<CancellationToken>()))
            .Returns<CreateMechanicGoldenClaimCommand, CancellationToken>(async (cmd, ct) =>
            {
                var id = Guid.NewGuid();
                _dbContext.Set<MechanicGoldenClaimEntity>().Add(new MechanicGoldenClaimEntity
                {
                    Id = id,
                    SharedGameId = cmd.SharedGameId,
                    Section = (int)cmd.Section,
                    Statement = cmd.Statement,
                    ExpectedPage = cmd.ExpectedPage,
                    SourceQuote = cmd.SourceQuote,
                    KeywordsJson = "[]",
                    CuratorUserId = cmd.CuratorUserId,
                    CreatedAt = DateTime.UtcNow,
                });
                await _dbContext.SaveChangesAsync(ct);
                return id;
            });

        var services = BuildServices(mediator.Object, _dbContext);

        var context = new SeedContext(
            Profile: SeedProfile.Dev,
            DbContext: _dbContext,
            Services: services,
            Logger: NullLogger.Instance,
            SystemUserId: systemUserId);

        var seeder = new PuertoRicoGoldenSeeder();

        // Act.
        await seeder.SeedAsync(context, default);

        // Assert: Verify tags persisted via a fresh query (clear tracker so any
        // unflushed state is excluded).
        _dbContext.ChangeTracker.Clear();

        var tagCount = await _dbContext.MechanicGoldenBggTags
            .AsNoTracking()
            .CountAsync(t => t.SharedGameId == prSharedGameId);

        var fixtureSize = PuertoRicoGoldenSeeder.LoadEmbeddedBggTagsForTest().Count;
        tagCount.Should().Be(fixtureSize, "all fixture tags should land in the DB on first run");
        tagCount.Should().BeGreaterThanOrEqualTo(10, "Task 4 spec requires ≥10 BGG tags");

        // Spot-check a known mechanism + category + family tag (from bgg-tags.json).
        var allTags = await _dbContext.MechanicGoldenBggTags
            .AsNoTracking()
            .Where(t => t.SharedGameId == prSharedGameId)
            .ToListAsync();

        allTags.Should().Contain(t => t.Name == "Worker Placement" && t.Category == "Mechanism");
        allTags.Should().Contain(t => t.Name == "Economic" && t.Category == "Category");
        allTags.Should().Contain(t => t.Name == "Country: Puerto Rico" && t.Category == "Family");
    }

    [Fact]
    [Trait("Category", TestCategories.Integration)]
    public async Task SeedAsync_BggTagsAreIdempotent()
    {
        // Arrange.
        var prSharedGameId = await SeedPuertoRicoSharedGameAsync();
        var systemUserId = Guid.NewGuid();
        await SeedSystemUserAsync(systemUserId);

        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(
                It.IsAny<CreateMechanicGoldenClaimCommand>(),
                It.IsAny<CancellationToken>()))
            .Returns<CreateMechanicGoldenClaimCommand, CancellationToken>(async (cmd, ct) =>
            {
                var id = Guid.NewGuid();
                _dbContext.Set<MechanicGoldenClaimEntity>().Add(new MechanicGoldenClaimEntity
                {
                    Id = id,
                    SharedGameId = cmd.SharedGameId,
                    Section = (int)cmd.Section,
                    Statement = cmd.Statement,
                    ExpectedPage = cmd.ExpectedPage,
                    SourceQuote = cmd.SourceQuote,
                    KeywordsJson = "[]",
                    CuratorUserId = cmd.CuratorUserId,
                    CreatedAt = DateTime.UtcNow,
                });
                await _dbContext.SaveChangesAsync(ct);
                return id;
            });

        var services = BuildServices(mediator.Object, _dbContext);

        var context = new SeedContext(
            Profile: SeedProfile.Dev,
            DbContext: _dbContext,
            Services: services,
            Logger: NullLogger.Instance,
            SystemUserId: systemUserId);

        var seeder = new PuertoRicoGoldenSeeder();

        // Act: first run.
        await seeder.SeedAsync(context, default);
        _dbContext.ChangeTracker.Clear();

        var firstCount = await _dbContext.MechanicGoldenBggTags
            .AsNoTracking()
            .CountAsync(t => t.SharedGameId == prSharedGameId);

        var fixtureSize = PuertoRicoGoldenSeeder.LoadEmbeddedBggTagsForTest().Count;
        firstCount.Should().Be(fixtureSize, "first run should insert the entire fixture");

        // Act: second run — must be a no-op for tags (UpsertBatchAsync skips existing names).
        await seeder.SeedAsync(context, default);
        _dbContext.ChangeTracker.Clear();

        var secondCount = await _dbContext.MechanicGoldenBggTags
            .AsNoTracking()
            .CountAsync(t => t.SharedGameId == prSharedGameId);

        secondCount.Should().Be(firstCount, "second run must not duplicate BGG tags");
    }
}
