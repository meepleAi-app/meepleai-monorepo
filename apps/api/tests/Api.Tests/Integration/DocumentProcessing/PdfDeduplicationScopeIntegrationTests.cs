using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Persistence;
using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Infrastructure.Entities.UserLibrary;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.DocumentProcessing;

/// <summary>
/// #4326 and #4317 (both absorbed by #4325): which existing PDF an upload may reuse.
/// </summary>
/// <remarks>
/// <para>
/// <b>Catalog upload.</b> The deduplication of a catalog game looked for the file among ALL the PDFs
/// (the old <c>FindByContentHashAsync</c> had no filter), the private ones of other users included. The uploader was then
/// linked to somebody else's private PDF, got a success answer with its id, and the catalog game stayed without a
/// document (retrieval reads by <c>shared_game_id</c>, and that PDF carries a private game). Decided (2026-10-25,
/// 2026-10-26): it looks only among the PDFs uploaded against a catalog game.
/// </para>
/// <para>
/// <b>Private upload.</b> The same file on two private games of one user gave the second a link to the PDF of the first,
/// while the content stayed tied to the first game: the chat on the second found nothing (#4317, measured). Decided on
/// 2026-10-26, "strada 1": no reuse between private games. A private game has its own PDF and its own index, even for
/// the same content; the one reuse left is the same file on the same game.
/// </para>
/// <para>On Postgres, through the real repository. The hash is any string: the service compares it, it does not compute it here.</para>
/// </remarks>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "4326")]
public sealed class PdfDeduplicationScopeIntegrationTests : IAsyncLifetime
{
    private const string Hash = "9b74c9897bac770ffc029102a200c5de";

    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private ServiceProvider? _serviceProvider;
    private MeepleAiDbContext? _db;
    private PdfDeduplicationService? _service;
    private Guid _userA;
    private Guid _userB;

    public PdfDeduplicationScopeIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_dedup_scope_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        _serviceProvider = IntegrationServiceCollectionBuilder.CreateBase(connectionString).BuildServiceProvider();
        _db = _serviceProvider.GetRequiredService<MeepleAiDbContext>();
        await _db.Database.MigrateAsync(Ct);

        _service = new PdfDeduplicationService(
            new PdfDocumentRepository(_db, _serviceProvider.GetRequiredService<IDomainEventCollector>()));

        _userA = await SeedUserAsync("a");
        _userB = await SeedUserAsync("b");
    }

    public async ValueTask DisposeAsync()
    {
        if (_db is not null)
            await _db.DisposeAsync();

        if (_serviceProvider is not null)
            await _serviceProvider.DisposeAsync();

        if (!string.IsNullOrEmpty(_databaseName))
        {
            try
            {
                await _fixture.DropIsolatedDatabaseAsync(_databaseName);
            }
            catch
            {
                // Best-effort cleanup: isolation is already guaranteed by the dedicated database.
            }
        }
    }

    // ─── Catalog upload ──────────────────────────────────────────

    /// <summary>The case of #4326: A has the file on a private game, B uploads it for a catalog game.</summary>
    [Fact]
    public async Task ACatalogUpload_DoesNotReuseThePrivatePdfOfAnotherUser()
    {
        var privateGame = await SeedPrivateGameAsync(_userA);
        await SeedPdfAsync(_userA, privateGameId: privateGame);
        var catalogGame = await SeedCatalogGameAsync();

        var result = await _service!.EvaluateAsync(Hash, catalogGame, null, _userB, Ct);

        result.Decision.Should().Be(PdfDedupDecision.NewUpload, "a private PDF is not a catalog document");
        result.ExistingPdfDocumentId.Should().BeNull();
    }

    /// <summary>The same case with the uploader's own private PDF: it would leave the catalog game empty too.</summary>
    [Fact]
    public async Task ACatalogUpload_DoesNotReuseTheOwnPrivatePdf()
    {
        var privateGame = await SeedPrivateGameAsync(_userA);
        await SeedPdfAsync(_userA, privateGameId: privateGame);
        var catalogGame = await SeedCatalogGameAsync();

        var result = await _service!.EvaluateAsync(Hash, catalogGame, null, _userA, Ct);

        result.Decision.Should().Be(PdfDedupDecision.NewUpload);
    }

    /// <summary>The catalog deduplication stays global across users, for the PDFs of the catalog.</summary>
    [Fact]
    public async Task ACatalogUpload_ReusesTheCatalogPdfOfAnotherUser()
    {
        var firstCatalogGame = await SeedCatalogGameAsync();
        var secondCatalogGame = await SeedCatalogGameAsync();
        var existing = await SeedPdfAsync(_userA, sharedGameId: firstCatalogGame);

        var result = await _service!.EvaluateAsync(Hash, secondCatalogGame, null, _userB, Ct);

        result.Decision.Should().Be(PdfDedupDecision.ReuseExisting);
        result.ExistingPdfDocumentId.Should().Be(existing);
    }

    /// <summary>A match left in the Failed state is not reusable, as before.</summary>
    [Fact]
    public async Task ACatalogUpload_DoesNotReuseAFailedCatalogPdf()
    {
        var catalogGame = await SeedCatalogGameAsync();
        await SeedPdfAsync(_userA, sharedGameId: catalogGame, state: "Failed");

        var result = await _service!.EvaluateAsync(Hash, catalogGame, null, _userB, Ct);

        result.Decision.Should().Be(PdfDedupDecision.NewUpload);
    }

    // ─── Private upload ──────────────────────────────────────────

    /// <summary>The case of #4317: the same file on two private games of one user.</summary>
    [Fact]
    public async Task APrivateUpload_DoesNotReuseThePdfOfAnotherPrivateGameOfTheSameUser()
    {
        var firstGame = await SeedPrivateGameAsync(_userA);
        var secondGame = await SeedPrivateGameAsync(_userA);
        await SeedPdfAsync(_userA, privateGameId: firstGame);

        var result = await _service!.EvaluateAsync(Hash, null, secondGame, _userA, Ct);

        result.Decision.Should().Be(PdfDedupDecision.NewUpload, "each private game has its own PDF and its own index");
        result.ExistingPdfDocumentId.Should().BeNull();
    }

    /// <summary>The user's own catalog PDF is not a private game's PDF either: the chat of a private game reads its own.</summary>
    [Fact]
    public async Task APrivateUpload_DoesNotReuseTheUsersOwnCatalogPdf()
    {
        var catalogGame = await SeedCatalogGameAsync();
        await SeedPdfAsync(_userA, sharedGameId: catalogGame);
        var privateGame = await SeedPrivateGameAsync(_userA);

        var result = await _service!.EvaluateAsync(Hash, null, privateGame, _userA, Ct);

        result.Decision.Should().Be(PdfDedupDecision.NewUpload);
    }

    /// <summary>The one reuse left: the same file again on the same private game.</summary>
    [Fact]
    public async Task APrivateUpload_ReusesThePdfOfTheSameGame()
    {
        var game = await SeedPrivateGameAsync(_userA);
        var existing = await SeedPdfAsync(_userA, privateGameId: game);

        var result = await _service!.EvaluateAsync(Hash, null, game, _userA, Ct);

        result.Decision.Should().Be(PdfDedupDecision.ReuseExisting);
        result.ExistingPdfDocumentId.Should().Be(existing);
    }

    /// <summary>Private deduplication stays isolated per user: nobody reuses somebody else's.</summary>
    [Fact]
    public async Task APrivateUpload_DoesNotReuseThePdfOfAnotherUser()
    {
        var gameOfA = await SeedPrivateGameAsync(_userA);
        await SeedPdfAsync(_userA, privateGameId: gameOfA);
        var gameOfB = await SeedPrivateGameAsync(_userB);

        var result = await _service!.EvaluateAsync(Hash, null, gameOfB, _userB, Ct);

        result.Decision.Should().Be(PdfDedupDecision.NewUpload);
    }

    // ─── Support ────────────────────────────────────────────────

    private async Task<Guid> SeedUserAsync(string name)
    {
        var id = Guid.NewGuid();
        _db!.Users.Add(new UserEntity
        {
            Id = id,
            Email = $"dedup-{name}-{id:N}@test.com",
            Role = "user",
            Tier = "premium",
            CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(Ct);
        return id;
    }

    private async Task<Guid> SeedPrivateGameAsync(Guid ownerId)
    {
        var id = Guid.NewGuid();
        _db!.PrivateGames.Add(new PrivateGameEntity
        {
            Id = id,
            OwnerId = ownerId,
            Title = "A prototype",
            MinPlayers = 1,
            MaxPlayers = 4,
            Source = PrivateGameSource.Manual,
        });
        await _db.SaveChangesAsync(Ct);
        return id;
    }

    private async Task<Guid> SeedCatalogGameAsync()
    {
        var id = Guid.NewGuid();
        _db!.SharedGames.Add(new SharedGameEntity
        {
            Id = id,
            Title = "A catalog game",
            YearPublished = 2024,
            Description = string.Empty,
            ImageUrl = string.Empty,
            ThumbnailUrl = string.Empty,
            MinPlayers = 1,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            Status = 1,
            CreatedBy = _userA,
            CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(Ct);
        return id;
    }

    private async Task<Guid> SeedPdfAsync(
        Guid uploaderId, Guid? sharedGameId = null, Guid? privateGameId = null, string state = "Ready")
    {
        var id = Guid.NewGuid();
        _db!.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = id,
            FileName = $"rulebook-{id:N}.pdf",
            FilePath = $"/tmp/rulebook-{id:N}.pdf",
            FileSizeBytes = 1024,
            UploadedByUserId = uploaderId,
            ProcessingState = state,
            ContentHash = Hash,
            SharedGameId = sharedGameId,
            PrivateGameId = privateGameId,
        });
        await _db.SaveChangesAsync(Ct);
        return id;
    }
}
