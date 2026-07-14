using Api.BoundedContexts.DocumentProcessing.Application.Jobs;
using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Application.EventHandlers;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Quartz;
using Xunit;

namespace Api.Tests.Integration.SharedGameCatalog;

/// <summary>
/// Regression for the has_knowledge_base NoTracking silent no-op (PERF-06, cf. #2734, #2660).
///
/// Both writers that maintain <c>shared_games.has_knowledge_base</c> load the SharedGame row and
/// mutate it before <c>SaveChangesAsync</c>. <see cref="MeepleAiDbContext"/> defaults to
/// <c>QueryTrackingBehavior.NoTracking</c> in production (and in
/// <see cref="IntegrationWebApplicationFactory"/>), so without an explicit <c>.AsTracking()</c> the
/// save persists NOTHING — the "AI-ready" flag silently stays false.
///
/// The two writers:
///  - <see cref="KbFlagDriftAuditJob"/> (periodic reconcile) — repairs drifted games.
///  - <see cref="VectorDocumentIndexedForKbFlagHandler"/> (inline projection) — flips the flag on index.
///
/// Category=Unit tests missed this because <c>TestDbContextFactory</c> uses the EF Core in-memory
/// provider with default TrackAll semantics; these tests run against real Postgres via
/// Testcontainers with the production NoTracking default, so the mutation must actually persist.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "2248")]
public sealed class KbFlagNoTrackingPersistenceTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _databaseName = $"kb_flag_notracking_{Guid.NewGuid():N}";
    private WebApplicationFactory<Program> _factory = null!;

    public KbFlagNoTrackingPersistenceTests(SharedTestcontainersFixture fixture) => _fixture = fixture;

    public async ValueTask InitializeAsync()
    {
        var conn = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        await TestcontainersWaitHelpers.WaitForPostgresReadyAsync(conn);
        _factory = IntegrationWebApplicationFactory.Create(conn);
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>().Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory != null) await _factory.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    private static IJobExecutionContext FakeJobContext()
    {
        var ctx = new Mock<IJobExecutionContext>();
        ctx.SetupGet(c => c.FireTimeUtc).Returns(DateTimeOffset.UtcNow);
        ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        ctx.SetupProperty(c => c.Result);
        return ctx.Object;
    }

    /// <summary>Seeds User → SharedGame(HasKnowledgeBase=false) → Ready PdfDocument (FK order).</summary>
    private async Task<Guid> SeedDriftedGameAsync(bool withReadyPdf)
    {
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        db.Users.Add(new UserEntity
        {
            Id = userId,
            Email = $"seed-{userId:N}@meepleai.dev",
            Tier = "premium",
            Role = "admin",
        });
        db.SharedGames.Add(new SharedGameEntity
        {
            Id = gameId,
            Title = "Gloomhaven",
            HasKnowledgeBase = false,
        });
        if (withReadyPdf)
        {
            db.PdfDocuments.Add(new PdfDocumentEntity
            {
                Id = Guid.NewGuid(),
                FileName = "rules.pdf",
                FilePath = "/uploads/rules.pdf",
                UploadedByUserId = userId,
                SharedGameId = gameId,
                ProcessingState = "Ready",
            });
        }
        await db.SaveChangesAsync();
        return gameId;
    }

    private async Task<bool> ReloadHasKnowledgeBaseAsync(Guid gameId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var game = await db.SharedGames.AsNoTracking().FirstAsync(g => g.Id == gameId);
        return game.HasKnowledgeBase;
    }

    [Fact(DisplayName = "#2248: KbFlagDriftAuditJob persists HasKnowledgeBase=true under NoTracking (real DB)")]
    public async Task ReconcileJob_PersistsHasKnowledgeBaseFlag()
    {
        var gameId = await SeedDriftedGameAsync(withReadyPdf: true);

        // Execute the reconcile against a NoTracking DbContext resolved from the real container.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var job = new KbFlagDriftAuditJob(
                scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>(),
                scope.ServiceProvider.GetRequiredService<IHybridCacheService>(),
                scope.ServiceProvider.GetRequiredService<ICacheInvalidationRetryPolicy>(),
                scope.ServiceProvider.GetRequiredService<ILogger<KbFlagDriftAuditJob>>());

            await job.Execute(FakeJobContext());
        }

        // Fresh context: the flag must have actually flipped (fails without .AsTracking()).
        (await ReloadHasKnowledgeBaseAsync(gameId)).Should().BeTrue(
            "KbFlagDriftAuditJob must load SharedGames .AsTracking() so the flip persists under the NoTracking default");
    }

    [Fact(DisplayName = "#2248: VectorDocumentIndexedForKbFlagHandler persists HasKnowledgeBase=true under NoTracking (real DB)")]
    public async Task InlineHandler_PersistsHasKnowledgeBaseFlag()
    {
        var gameId = await SeedDriftedGameAsync(withReadyPdf: false);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var handler = new VectorDocumentIndexedForKbFlagHandler(
                scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>(),
                scope.ServiceProvider.GetRequiredService<IHybridCacheService>(),
                scope.ServiceProvider.GetRequiredService<ICacheInvalidationRetryPolicy>(),
                scope.ServiceProvider.GetRequiredService<ILogger<VectorDocumentIndexedForKbFlagHandler>>());

            var evt = new VectorDocumentIndexedEvent(
                documentId: Guid.NewGuid(),
                gameId: Guid.NewGuid(),
                chunkCount: 12,
                sharedGameId: gameId);

            await handler.Handle(evt, CancellationToken.None);
        }

        (await ReloadHasKnowledgeBaseAsync(gameId)).Should().BeTrue(
            "VectorDocumentIndexedForKbFlagHandler must load the SharedGame .AsTracking() so the flip persists under the NoTracking default");
    }
}
