using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.SessionTracking;

/// <summary>
/// #2638 / SI-7: persistence coverage for the multi-context glossary model.
///
/// Test A proves the <c>contexts</c> JSONB column round-trips a multi-element list
/// through the real EF pipeline. Test B proves the upsert-update path actually
/// persists a <see cref="GamebookGlossaryEntry.ReplaceContexts"/> mutation — which
/// only works because <see cref="IGamebookGlossaryRepository.GetByIdAsync"/> loads
/// <c>.AsTracking()</c> under the global NoTracking default (same class as #2660).
/// </summary>
[Collection("Integration-GroupB")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SessionTracking")]
[Trait("Issue", "2638")]
public sealed class GamebookGlossaryContextsPersistenceTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _databaseName = $"glossary_contexts_persist_{Guid.NewGuid():N}";
    private WebApplicationFactory<Program> _factory = null!;

    private static readonly Guid CampaignId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid CreatedBy = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    public GamebookGlossaryContextsPersistenceTests(SharedTestcontainersFixture fixture) => _fixture = fixture;

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

    [Fact(DisplayName = "#2638 Test A: contexts JSONB round-trips a multi-element list on create")]
    public async Task Create_WithTwoContexts_RoundTripsThroughJsonb()
    {
        var bookA = Guid.NewGuid();
        var bookB = Guid.NewGuid();
        Guid entryId;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IGamebookGlossaryRepository>();
            var entry = GamebookGlossaryEntry.Create(
                CampaignId, "sentinel", "soldato di guardia", GlossarySource.Manual, CreatedBy,
                contexts: new[]
                {
                    GlossaryContext.Create(bookA, "§147", null),
                    GlossaryContext.Create(bookB, "§63", "punto di osservazione strategica"),
                });
            await repo.AddAsync(entry, CancellationToken.None);
            await repo.SaveChangesAsync(CancellationToken.None);
            entryId = entry.Id;
        }

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var reread = await db.GamebookGlossaryEntries.AsNoTracking().FirstAsync(e => e.Id == entryId);

            reread.Contexts.Should().HaveCount(2);
            reread.Contexts.Should().ContainSingle(c =>
                c.BookId == bookA && c.ParagraphRef == "§147" && c.Definition == null);
            reread.Contexts.Should().ContainSingle(c =>
                c.BookId == bookB && c.ParagraphRef == "§63" && c.Definition == "punto di osservazione strategica");
        }
    }

    [Fact(DisplayName = "#2638 Test B: ReplaceContexts persists through the .AsTracking() getter")]
    public async Task Upsert_ReplaceContexts_PersistsViaTrackingGetter()
    {
        var legacyBook = Guid.NewGuid();
        var bookA = Guid.NewGuid();
        var bookB = Guid.NewGuid();
        Guid entryId;

        // Seed with a single (legacy) context.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IGamebookGlossaryRepository>();
            var entry = GamebookGlossaryEntry.Create(
                CampaignId, "Voidstone", "Pietra del Vuoto", GlossarySource.Manual, CreatedBy,
                firstSeenBookId: legacyBook);
            await repo.AddAsync(entry, CancellationToken.None);
            await repo.SaveChangesAsync(CancellationToken.None);
            entryId = entry.Id;
        }

        // Load via the repo getter (must be .AsTracking()), replace the full set, save.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IGamebookGlossaryRepository>();
            var loaded = await repo.GetByIdAsync(entryId, CancellationToken.None);
            loaded.Should().NotBeNull();
            loaded!.ReplaceContexts(
                new[]
                {
                    GlossaryContext.Create(bookA, "§1", null),
                    GlossaryContext.Create(bookB, "§2", "def"),
                },
                CreatedBy);
            await repo.SaveChangesAsync(CancellationToken.None);
        }

        // Fresh read confirms the mutation persisted (would be the legacy single context if
        // GetByIdAsync had not tracked the entity — silent no-op under NoTracking).
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var reread = await db.GamebookGlossaryEntries.AsNoTracking().FirstAsync(e => e.Id == entryId);

            reread.Contexts.Should().HaveCount(2,
                "ReplaceContexts must persist through SaveChanges — requires .AsTracking() on the getter (#2638)");
            reread.Contexts.Should().NotContain(c => c.BookId == legacyBook);
            reread.Contexts.Should().ContainSingle(c => c.BookId == bookA && c.ParagraphRef == "§1");
            reread.Contexts.Should().ContainSingle(c => c.BookId == bookB && c.ParagraphRef == "§2" && c.Definition == "def");
        }
    }
}
