using Api.BoundedContexts.DocumentProcessing.Infrastructure.Persistence;
using Api.Tests.BoundedContexts.DocumentProcessing.TestHelpers;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.Integration.DocumentProcessing;

/// <summary>
/// Regressione di #3694 — il write-path detached di <see cref="PdfDocumentRepository"/>.
///
/// #3658 ha convertito <c>pdf_documents</c> da <c>byte[] RowVersion</c> (<c>bytea</c>) alla
/// colonna di sistema <c>xmin</c>. La conversione era corretta a livello di schema, ma
/// <c>UpdateAsync</c> persiste un grafo <b>detached</b> (<c>MapToPersistence</c> +
/// <c>DbSet.Update()</c>) e il token <b>non attraversava il dominio</b>: l'aggregato non aveva la
/// proprietà, e nessuno dei due mapper la toccava.
///
/// Su un'entità mai tracciata EF non ha una riga da cui ricavare l'<i>original value</i> del token
/// e usa quello che trova sulla proprietà — <c>0</c>, che non è mai un xid reale. Ogni UPDATE
/// emetteva perciò <c>WHERE id = @id AND xmin = 0</c>, colpiva 0 righe e sollevava
/// <c>DbUpdateConcurrencyException</c> <b>anche senza alcuna concorrenza</b>.
///
/// Prima di #3658 la coincidenza lo mascherava: <c>RowVersion</c> era NULL sull'entità <i>e</i>
/// sulla riga, quindi EF generava <c>row_version IS NULL</c>, sempre vero.
///
/// I due test coprono le direzioni opposte, ed è la ragione per cui il guasto era passato: chi
/// verifica solo la seconda vede un token che «funziona», perché un token rotto in questo modo
/// solleva l'eccezione attesa — per il motivo sbagliato.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "3694")]
public sealed class PdfDocumentXminWritePathTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = null!;
    private string _connectionString = null!;
    private MeepleAiDbContext _dbContext = null!;

    public PdfDocumentXminWritePathTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"pdf_xmin_writepath_{Guid.NewGuid():N}";
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        _dbContext = _fixture.CreateDbContext(_connectionString);
        await _dbContext.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    private static PdfDocumentRepository CreateRepository(MeepleAiDbContext dbContext) =>
        new(dbContext, new Mock<IDomainEventCollector>().Object);

    /// <summary>
    /// Semina l'utente e il gioco — entrambi FK di <c>pdf_documents</c>
    /// (<c>FK_pdf_documents_users_UploadedByUserId</c> e il gioco) — più un documento già
    /// completato. Con id inventati l'INSERT fallisce con 23503 prima ancora di arrivare al
    /// token, e il rosso direbbe la cosa sbagliata (pitfall #2620).
    /// </summary>
    private async Task<Guid> SeedDocumentAsync()
    {
        var userId = Guid.NewGuid();
        _dbContext.Set<UserEntity>().Add(new UserEntity
        {
            Id = userId,
            Email = $"pdf-xmin-{userId:N}@test.com",
            DisplayName = "Pdf Xmin Write-Path Test User",
            Role = "user",
            Tier = "free",
            CreatedAt = DateTime.UtcNow
        });

        var gameId = Guid.NewGuid();
        _dbContext.SharedGames.Add(new SharedGameEntity { Id = gameId, Title = "Gioco con regolamento" });
        await _dbContext.SaveChangesAsync();

        var document = new PdfDocumentBuilder()
            .WithGameId(gameId)
            .WithUploadedBy(userId)
            .ThatIsCompleted()
            .Build();

        var repository = CreateRepository(_dbContext);
        await repository.AddAsync(document, CancellationToken.None);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        return document.Id;
    }

    [Fact(DisplayName = "PdfDocument: un solo scrittore riesce — nessuna concorrenza, nessun 409")]
    public async Task UpdateAsync_WithNoConcurrentWriter_Succeeds()
    {
        // ── Arrange ───────────────────────────────────────────────────────────────
        var documentId = await SeedDocumentAsync();
        var repository = CreateRepository(_dbContext);

        // ── Act: rileggo, muto e persisto — nessun altro scrittore ────────────────
        var loaded = await repository.GetByIdAsync(documentId, CancellationToken.None);
        loaded.Should().NotBeNull();

        loaded!.SetActiveForRag(false);
        await repository.UpdateAsync(loaded, CancellationToken.None);

        Func<Task> act = async () => await _dbContext.SaveChangesAsync();

        // ── Assert ────────────────────────────────────────────────────────────────
        await act.Should().NotThrowAsync(
            "senza scritture concorrenti l'update deve passare. Se il token letto dal DB non " +
            "attraversa l'aggregato, l'UPDATE emette `WHERE xmin = 0` — mai un xid reale — " +
            "colpisce 0 righe e ogni scrittura sul documento diventa un 409 (#3694)");
    }

    [Fact(DisplayName = "PdfDocument: due scrittori concorrenti, il secondo è rifiutato")]
    public async Task UpdateAsync_AfterConcurrentWrite_ThrowsConcurrencyException()
    {
        // ── Arrange ───────────────────────────────────────────────────────────────
        var documentId = await SeedDocumentAsync();

        await using var dbA = _fixture.CreateDbContext(_connectionString);
        await using var dbB = _fixture.CreateDbContext(_connectionString);
        var repoA = CreateRepository(dbA);
        var repoB = CreateRepository(dbB);

        var docA = await repoA.GetByIdAsync(documentId, CancellationToken.None);
        var docB = await repoB.GetByIdAsync(documentId, CancellationToken.None);
        docA.Should().NotBeNull();
        docB.Should().NotBeNull();

        // ── Act: A committa per primo, B resta con un token vecchio ───────────────
        docA!.SetActiveForRag(false);
        await repoA.UpdateAsync(docA, CancellationToken.None);
        await dbA.SaveChangesAsync();

        docB!.SetContentHash("hash-scritto-da-b");
        await repoB.UpdateAsync(docB, CancellationToken.None);
        Func<Task> act = async () => await dbB.SaveChangesAsync();

        // ── Assert ────────────────────────────────────────────────────────────────
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>(
            "B ha letto prima del commit di A: la protezione deve restare attiva. Questo test " +
            "da solo non basta a dire che il token funziona — passerebbe anche con un token " +
            "sempre a 0, che rifiuta ogni scrittura");
    }
}
