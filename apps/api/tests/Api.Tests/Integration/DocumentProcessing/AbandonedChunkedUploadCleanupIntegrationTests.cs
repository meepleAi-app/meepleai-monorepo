using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Persistence;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.Integration.DocumentProcessing;

/// <summary>
/// #4319 (assorbita da #4308): un caricamento a pezzi lasciato a metà non veniva mai ripulito. La riga in
/// <c>chunked_upload_sessions</c> e la cartella dei pezzi sul disco restavano finché qualcuno non richiamava
/// quella sessione: <c>FindExpiredSessionsAsync</c> esisteva e non aveva chiamanti. I pezzi sono frammenti di un
/// file dell'utente, sul disco senza una scadenza.
/// </summary>
/// <remarks>
/// Su Postgres e su una cartella vera. Ogni test lavora in una cartella di base sua, sotto la cartella temporanea,
/// così non tocca <c>meepleai_uploads</c> e non si pesta con altre classi. Gli host di integrazione tolgono i
/// servizi periodici: qui il servizio è costruito a mano e si chiama un giro alla volta.
/// </remarks>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "4319")]
public sealed class AbandonedChunkedUploadCleanupIntegrationTests : IAsyncLifetime
{
    private static readonly Guid TestUserId = new("A0000000-0000-0000-0000-000004319001");
    private static readonly Guid TestGameId = new("B0000000-0000-0000-0000-000004319001");

    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private ServiceProvider? _serviceProvider;
    private MeepleAiDbContext? _dbContext;
    private string _uploadBase = string.Empty;

    public AbandonedChunkedUploadCleanupIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_chunk_cleanup_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(connectionString);
        services.AddScoped<IChunkedUploadSessionRepository, ChunkedUploadSessionRepository>();
        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();
        await _dbContext.Database.MigrateAsync(Ct);

        _dbContext.Users.Add(new UserEntity
        {
            Id = TestUserId,
            Email = "chunk-cleanup-test@meepleai.test",
            DisplayName = "Chunk Cleanup Test User",
            Role = "user",
            CreatedAt = DateTime.UtcNow,
        });
        _dbContext.SharedGames.Add(new SharedGameEntity
        {
            Id = TestGameId,
            Title = "Chunk Cleanup Test Game",
            BggId = Guid.NewGuid().GetHashCode() & 0x7FFFFFFF,
            YearPublished = 2024,
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            CreatedAt = DateTime.UtcNow,
        });
        await _dbContext.SaveChangesAsync(Ct);

        _uploadBase = Path.Combine(Path.GetTempPath(), $"meepleai_cleanup_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_uploadBase);
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext is not null)
            await _dbContext.DisposeAsync();

        if (_serviceProvider is not null)
            await _serviceProvider.DisposeAsync();

        TryDelete(_uploadBase);
        TryDelete(_uploadBase + "_outside");

        if (!string.IsNullOrEmpty(_databaseName))
        {
            try
            {
                await _fixture.DropIsolatedDatabaseAsync(_databaseName);
            }
            catch
            {
                // Best-effort cleanup — l'isolamento è già garantito dal database dedicato.
            }
        }
    }

    /// <summary>
    /// Il caso dell'issue: una sessione scaduta e mai più richiamata. Dopo il giro non ha più la cartella né lo
    /// stato attivo.
    /// </summary>
    [Fact]
    public async Task AnExpiredSessionNeverCalledAgain_LosesItsFolder_AndItsActiveState()
    {
        var session = await SeedSessionAsync("uploading", expiresAt: DateTime.UtcNow.AddHours(-2), withFolder: true);

        var result = await RunCleanupAsync();

        Directory.Exists(session.TempDirectory).Should().BeFalse("nobody will ever call this session again");
        (await StatusOfAsync(session.Id)).Should().Be("expired");
        result.SessionsExpired.Should().Be(1);
        result.DirectoriesDeleted.Should().Be(1);
        result.Failures.Should().Be(0);
    }

    /// <summary>Una sessione ancora valida non si tocca: l'utente sta caricando.</summary>
    [Fact]
    public async Task ASessionStillValid_IsLeftAlone()
    {
        var session = await SeedSessionAsync("uploading", expiresAt: DateTime.UtcNow.AddHours(2), withFolder: true);

        var result = await RunCleanupAsync();

        Directory.Exists(session.TempDirectory).Should().BeTrue();
        File.Exists(Path.Combine(session.TempDirectory, "chunk_0000.bin")).Should().BeTrue();
        (await StatusOfAsync(session.Id)).Should().Be("uploading");
        result.SessionsExpired.Should().Be(0);
        result.DirectoriesDeleted.Should().Be(0);
    }

    /// <summary>
    /// Una cartella senza sessione si toglie, ma solo se è abbastanza vecchia: <c>InitChunkedUploadCommandHandler</c>
    /// crea la cartella prima di inserire la riga, e una cartella appena nata è un caricamento che sta partendo.
    /// </summary>
    [Fact]
    public async Task AFolderWithNoSession_GoesOnlyWhenOldEnough()
    {
        var old = MakeFolder(Path.Combine(_uploadBase, Guid.NewGuid().ToString()), age: TimeSpan.FromHours(30));
        var young = MakeFolder(Path.Combine(_uploadBase, Guid.NewGuid().ToString()), age: TimeSpan.FromMinutes(5));

        var result = await RunCleanupAsync();

        Directory.Exists(old).Should().BeFalse("no session points to it and it is older than a session can live");
        Directory.Exists(young).Should().BeTrue("a folder this young may belong to an upload that is just starting");
        result.DirectoriesDeleted.Should().Be(1);
    }

    /// <summary>
    /// Una sessione già marcata <c>expired</c> (lo fa <c>UploadChunkCommandHandler</c> quando la ritrova scaduta, senza
    /// toccare la cartella) non la vede <c>FindExpiredSessionsAsync</c>, ma la sua cartella è rimasta.
    /// </summary>
    [Fact]
    public async Task ASessionAlreadyMarkedExpired_KeepsNoOldFolderBehind()
    {
        var oldSession = await SeedSessionAsync(
            "expired", expiresAt: DateTime.UtcNow.AddDays(-2), withFolder: true, folderAge: TimeSpan.FromHours(30));
        var youngSession = await SeedSessionAsync(
            "expired", expiresAt: DateTime.UtcNow.AddMinutes(-10), withFolder: true, folderAge: TimeSpan.FromMinutes(5));

        await RunCleanupAsync();

        Directory.Exists(oldSession.TempDirectory).Should().BeFalse();
        Directory.Exists(youngSession.TempDirectory).Should().BeTrue("it is too young to rule out a request still in flight");
    }

    /// <summary>
    /// Una cartella vecchia ma legata a una sessione ancora viva non è un orfano: la sessione la protegge.
    /// </summary>
    [Fact]
    public async Task AnOldFolderOfALiveSession_IsNotAnOrphan()
    {
        var session = await SeedSessionAsync(
            "uploading", expiresAt: DateTime.UtcNow.AddHours(1), withFolder: true, folderAge: TimeSpan.FromHours(30));

        await RunCleanupAsync();

        Directory.Exists(session.TempDirectory).Should().BeTrue();
        (await StatusOfAsync(session.Id)).Should().Be("uploading");
    }

    /// <summary>
    /// Il percorso viene dal database. Se punta fuori da <c>meepleai_uploads</c> non si cancella mai: la sessione si
    /// marca scaduta, la cartella resta dov'è.
    /// </summary>
    [Fact]
    public async Task APathOutsideTheUploadFolder_IsNeverDeleted()
    {
        var outside = MakeFolder(Path.Combine(_uploadBase + "_outside", "dati-di-qualcun-altro"), age: TimeSpan.FromDays(3));
        var session = await SeedSessionAsync(
            "uploading", expiresAt: DateTime.UtcNow.AddHours(-2), withFolder: false, tempDirectory: outside);

        var result = await RunCleanupAsync();

        Directory.Exists(outside).Should().BeTrue("it is not under the upload folder");
        File.Exists(Path.Combine(outside, "chunk_0000.bin")).Should().BeTrue();
        (await StatusOfAsync(session.Id)).Should().Be("expired", "the session itself is over, whatever its path says");
        result.DirectoriesDeleted.Should().Be(0);
    }

    // ─── Supporto ────────────────────────────────────────────────

    private async Task<ChunkedUploadCleanupResult> RunCleanupAsync()
    {
        var service = new AbandonedChunkedUploadCleanupService(
            _serviceProvider!.GetRequiredService<IServiceScopeFactory>(),
            new ConfigurationBuilder().Build(),
            NullLogger<AbandonedChunkedUploadCleanupService>.Instance,
            uploadBasePath: _uploadBase);

        return await service.CleanupOnceAsync(Ct);
    }

    private async Task<ChunkedUploadSessionEntity> SeedSessionAsync(
        string status,
        DateTime expiresAt,
        bool withFolder,
        TimeSpan? folderAge = null,
        string? tempDirectory = null)
    {
        var id = Guid.NewGuid();
        var directory = tempDirectory ?? Path.Combine(_uploadBase, id.ToString());
        if (withFolder)
        {
            MakeFolder(directory, folderAge ?? TimeSpan.Zero);
        }

        var entity = new ChunkedUploadSessionEntity
        {
            Id = id,
            GameId = TestGameId,
            UserId = TestUserId,
            FileName = "regolamento.pdf",
            TotalFileSize = 20_000_000,
            TotalChunks = 2,
            ReceivedChunks = 1,
            ReceivedChunkIndices = "[0]",
            TempDirectory = directory,
            Status = status,
            CreatedAt = expiresAt.AddHours(-24),
            ExpiresAt = expiresAt,
        };
        _dbContext!.ChunkedUploadSessions.Add(entity);
        await _dbContext.SaveChangesAsync(Ct);
        return entity;
    }

    /// <summary>Una cartella con un pezzo dentro, la cui ultima scrittura risale a <paramref name="age"/> fa.</summary>
    private static string MakeFolder(string path, TimeSpan age)
    {
        Directory.CreateDirectory(path);
        File.WriteAllBytes(Path.Combine(path, "chunk_0000.bin"), [1, 2, 3]);
        Directory.SetLastWriteTimeUtc(path, DateTime.UtcNow - age);
        return path;
    }

    private async Task<string> StatusOfAsync(Guid sessionId)
    {
        using var scope = _serviceProvider!.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await db.ChunkedUploadSessions.AsNoTracking()
            .Where(s => s.Id == sessionId)
            .Select(s => s.Status)
            .SingleAsync(Ct);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort.
        }
    }
}
