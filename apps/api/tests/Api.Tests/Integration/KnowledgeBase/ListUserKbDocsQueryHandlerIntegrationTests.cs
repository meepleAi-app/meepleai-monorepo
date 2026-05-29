using Api.BoundedContexts.KnowledgeBase.Application.Queries.ListUserKbDocs;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.KnowledgeBase;

/// <summary>
/// Integration tests for <see cref="ListUserKbDocsQueryHandler"/>.
/// Validates AC1-AC4 + AC6: user-scoping, cross-game listing, pagination,
/// state filtering (ready/all), and GameName resolution. BE-1 #1588.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class ListUserKbDocsQueryHandlerIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private ListUserKbDocsQueryHandler? _handler;
    private ServiceProvider? _serviceProvider;

    private static readonly Guid UserA = Guid.NewGuid();
    private static readonly Guid UserB = Guid.NewGuid();

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public ListUserKbDocsQueryHandlerIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_list_user_kb_docs_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(connectionString);

        // ISharedGameRepository is not in CreateBase — register the real implementation
        // so the handler's bulk game-name fetch (GetNamesByIdsAsync) hits the actual table.
        services.AddScoped<ISharedGameRepository, SharedGameRepository>();

        // CreateBase registers Mock.Of<IHybridCacheService>() which returns null from
        // GetOrCreateAsync. Override with the pass-through test implementation so the
        // handler's cache wrapper always invokes the factory.
        services.AddScoped<IHybridCacheService, TestHybridCacheService>();

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();

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

        _handler = new ListUserKbDocsQueryHandler(
            _dbContext,
            _serviceProvider.GetRequiredService<ISharedGameRepository>(),
            _serviceProvider.GetRequiredService<Api.Services.IHybridCacheService>(),
            _serviceProvider.GetRequiredService<ILogger<ListUserKbDocsQueryHandler>>());
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext is not null)
        {
            await _dbContext.DisposeAsync();
        }

        if (_serviceProvider is not null)
        {
            await _serviceProvider.DisposeAsync();
        }

        if (!string.IsNullOrEmpty(_databaseName))
        {
            try { await _fixture.DropIsolatedDatabaseAsync(_databaseName); }
            catch { /* best-effort cleanup */ }
        }
    }

    // ─── Seed helpers ────────────────────────────────────────────────────────

    private async Task<SharedGameEntity> SeedGameAsync(string title)
    {
        var game = new SharedGameEntity
        {
            Id = Guid.NewGuid(),
            Title = title,
            YearPublished = 2024,
            Description = string.Empty,
            ImageUrl = string.Empty,
            ThumbnailUrl = string.Empty,
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            Status = 1, // Published
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };
        _dbContext!.SharedGames.Add(game);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
        return game;
    }

    private async Task SeedUserAsync(Guid userId)
    {
        _dbContext!.Users.Add(new UserEntity
        {
            Id = userId,
            Email = $"u-{userId:N}@test.local",
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    private static PdfDocumentEntity NewDoc(
        Guid uploaderId,
        Guid? sharedGameId,
        string state,
        string fileName,
        DateTime uploadedAt,
        DateTime? processedAt) => new()
        {
            Id = Guid.NewGuid(),
            FileName = fileName,
            FilePath = $"/tmp/{fileName}",
            FileSizeBytes = 1024,
            ContentType = "application/pdf",
            UploadedByUserId = uploaderId,
            UploadedAt = uploadedAt,
            ProcessingState = state,
            ProcessedAt = processedAt,
            SharedGameId = sharedGameId,
        };

    // ─── AC1: User-scoping ───────────────────────────────────────────────────

    [Fact(Timeout = 30000)]
    public async Task Returns_only_docs_for_caller()
    {
        await SeedUserAsync(UserA);
        await SeedUserAsync(UserB);
        var game = await SeedGameAsync("Catan");
        var t = DateTime.UtcNow.AddDays(-1);
        _dbContext!.PdfDocuments.AddRange(
            NewDoc(UserA, game.Id, "Ready", "a1.pdf", t, t),
            NewDoc(UserA, game.Id, "Ready", "a2.pdf", t, t),
            NewDoc(UserB, game.Id, "Ready", "b1.pdf", t, t));
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var result = await _handler!.Handle(new ListUserKbDocsQuery(UserA), TestCancellationToken);

        result.Total.Should().Be(2);
        result.Items.Should().HaveCount(2);
        result.Items.Should().OnlyContain(d => d.FileName.StartsWith("a"));
    }

    // ─── AC2: Cross-game + recency sort (ProcessedAt ?? UploadedAt DESC) ────

    [Fact(Timeout = 30000)]
    public async Task Returns_cross_game_docs_sorted_by_recency()
    {
        await SeedUserAsync(UserA);
        var g1 = await SeedGameAsync("Catan");
        var g2 = await SeedGameAsync("Carcassonne");
        var now = DateTime.UtcNow;
        _dbContext!.PdfDocuments.AddRange(
            NewDoc(UserA, g1.Id, "Ready", "old.pdf", now.AddDays(-5), now.AddDays(-5)),
            NewDoc(UserA, g2.Id, "Ready", "new.pdf", now.AddDays(-1), now.AddDays(-1)),
            NewDoc(UserA, g1.Id, "Ready", "mid.pdf", now.AddDays(-3), now.AddDays(-3)));
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var result = await _handler!.Handle(new ListUserKbDocsQuery(UserA), TestCancellationToken);

        result.Items.Should().HaveCount(3);
        result.Items.Select(d => d.FileName).Should().ContainInOrder("new.pdf", "mid.pdf", "old.pdf");
        result.Items.Select(d => d.GameId).Should().Contain(g1.Id);
        result.Items.Select(d => d.GameId).Should().Contain(g2.Id);
    }

    // ─── AC3: Pagination ─────────────────────────────────────────────────────

    [Fact(Timeout = 30000)]
    public async Task Pagination_returns_correct_slice_and_total()
    {
        await SeedUserAsync(UserA);
        var game = await SeedGameAsync("Catan");
        var now = DateTime.UtcNow;
        for (int i = 0; i < 5; i++)
        {
            _dbContext!.PdfDocuments.Add(
                NewDoc(UserA, game.Id, "Ready", $"doc-{i}.pdf", now.AddDays(-i), now.AddDays(-i)));
        }
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        var page1 = await _handler!.Handle(
            new ListUserKbDocsQuery(UserA, Page: 1, PageSize: 2),
            TestCancellationToken);
        var page2 = await _handler!.Handle(
            new ListUserKbDocsQuery(UserA, Page: 2, PageSize: 2),
            TestCancellationToken);

        page1.Total.Should().Be(5);
        page1.Items.Should().HaveCount(2);
        page1.Items.Select(d => d.FileName).Should().ContainInOrder("doc-0.pdf", "doc-1.pdf");

        page2.Total.Should().Be(5);
        page2.Items.Should().HaveCount(2);
        page2.Items.Select(d => d.FileName).Should().ContainInOrder("doc-2.pdf", "doc-3.pdf");
    }

    // ─── AC4: state=ready (default) vs state=all ─────────────────────────────

    [Fact(Timeout = 30000)]
    public async Task State_ready_default_excludes_non_ready()
    {
        await SeedUserAsync(UserA);
        var game = await SeedGameAsync("Catan");
        var now = DateTime.UtcNow;
        _dbContext!.PdfDocuments.AddRange(
            NewDoc(UserA, game.Id, "Ready", "ready.pdf", now, now),
            NewDoc(UserA, game.Id, "Pending", "pending.pdf", now, null),
            NewDoc(UserA, game.Id, "Failed", "failed.pdf", now, now));
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var result = await _handler!.Handle(new ListUserKbDocsQuery(UserA), TestCancellationToken);

        result.Items.Should().HaveCount(1);
        result.Items.Single().FileName.Should().Be("ready.pdf");
        result.Items.Single().ProcessingState.Should().Be("Ready");
    }

    [Fact(Timeout = 30000)]
    public async Task State_all_includes_every_state()
    {
        await SeedUserAsync(UserA);
        var game = await SeedGameAsync("Catan");
        var now = DateTime.UtcNow;
        _dbContext!.PdfDocuments.AddRange(
            NewDoc(UserA, game.Id, "Ready", "ready.pdf", now, now),
            NewDoc(UserA, game.Id, "Pending", "pending.pdf", now, null),
            NewDoc(UserA, game.Id, "Failed", "failed.pdf", now, now));
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var result = await _handler!.Handle(
            new ListUserKbDocsQuery(UserA, State: "all"),
            TestCancellationToken);

        result.Items.Should().HaveCount(3);
        result.Items.Select(d => d.ProcessingState)
            .Should().BeEquivalentTo(new[] { "Ready", "Pending", "Failed" });
    }

    // ─── AC5 (partial: handler-side empty user) ──────────────────────────────

    [Fact(Timeout = 30000)]
    public async Task Empty_library_returns_zero_total_and_empty_items()
    {
        await SeedUserAsync(UserA);
        // No docs seeded for UserA.

        var result = await _handler!.Handle(new ListUserKbDocsQuery(UserA), TestCancellationToken);

        result.Total.Should().Be(0);
        result.Items.Should().BeEmpty();
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
    }

    // ─── AC6: GameName resolution + null GameId ─────────────────────────────

    [Fact(Timeout = 30000)]
    public async Task Resolves_GameName_via_repository_and_handles_null_GameId()
    {
        await SeedUserAsync(UserA);
        var game = await SeedGameAsync("Catan");
        var now = DateTime.UtcNow;
        _dbContext!.PdfDocuments.AddRange(
            NewDoc(UserA, game.Id, "Ready", "with-game.pdf", now, now),
            NewDoc(UserA, null, "Ready", "orphan.pdf", now.AddSeconds(-1), now.AddSeconds(-1)));
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var result = await _handler!.Handle(new ListUserKbDocsQuery(UserA), TestCancellationToken);

        result.Items.Should().HaveCount(2);

        var withGame = result.Items.Single(d => d.FileName == "with-game.pdf");
        withGame.GameId.Should().Be(game.Id);
        withGame.GameName.Should().Be("Catan");

        var orphan = result.Items.Single(d => d.FileName == "orphan.pdf");
        orphan.GameId.Should().BeNull();
        orphan.GameName.Should().BeNull();
    }

    // ─── AC7: UpdatedAt = ProcessedAt ?? UploadedAt (explicit field) ────────

    [Fact(Timeout = 30000)]
    public async Task UpdatedAt_equals_ProcessedAt_when_processed()
    {
        await SeedUserAsync(UserA);
        var game = await SeedGameAsync("Catan");
        var uploadTime = DateTime.UtcNow.AddDays(-5);
        var processTime = DateTime.UtcNow.AddDays(-1);
        _dbContext!.PdfDocuments.Add(
            NewDoc(UserA, game.Id, "Ready", "processed.pdf", uploadTime, processTime));
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var result = await _handler!.Handle(new ListUserKbDocsQuery(UserA), TestCancellationToken);

        result.Items.Should().HaveCount(1);
        var dto = result.Items.Single();
        // DB round-trip may lose microsecond precision; use tolerance of 1ms.
        dto.UpdatedAt.Should().BeCloseTo(processTime, TimeSpan.FromMilliseconds(1),
            "UpdatedAt should equal ProcessedAt when present");
    }

    [Fact(Timeout = 30000)]
    public async Task UpdatedAt_equals_UploadedAt_when_ProcessedAt_null()
    {
        await SeedUserAsync(UserA);
        var game = await SeedGameAsync("Catan");
        var uploadTime = DateTime.UtcNow;
        _dbContext!.PdfDocuments.Add(
            NewDoc(UserA, game.Id, "Pending", "unprocessed.pdf", uploadTime, null));
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var result = await _handler!.Handle(
            new ListUserKbDocsQuery(UserA, State: "all"),
            TestCancellationToken);

        result.Items.Should().HaveCount(1);
        var dto = result.Items.Single();
        // DB round-trip may lose microsecond precision; use tolerance of 1ms.
        dto.UpdatedAt.Should().BeCloseTo(uploadTime, TimeSpan.FromMilliseconds(1),
            "UpdatedAt should equal UploadedAt when ProcessedAt is null");
    }
}
