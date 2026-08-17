using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.Integration.DocumentProcessing;

/// <summary>
/// Concorrenza ottimistica su <c>photo_batch_uploads</c> (#3651, lotto 8).
///
/// <para>
/// La race è strutturale: un batch di foto viene aggiornato pagina per pagina mentre l'OCR
/// procede, e più worker possono chiudere pagine dello stesso batch nello stesso momento. Senza
/// token attivo i contatori (<c>IndexedPages</c>, <c>LowConfidencePageCount</c>) si perdono a
/// vicenda: il batch risulta completo con meno pagine di quante ne abbia davvero indicizzate.
/// </para>
/// <para>
/// <b>La genealogia di questa riga merita di essere ricordata.</b> Il commento sull'entità
/// racconta che <c>row_version bytea</c> era <c>NOT NULL</c> e faceva fallire l'INSERT — lo stesso
/// difetto trovato su <c>ab_test_sessions</c> nel lotto 5. La migration
/// <c>20260524190307_FixPhotoBatchUploadRowVersionNullable</c> lo «risolse» rendendo la colonna
/// nullable: il sintomo rumoroso sparì, e al suo posto restò il guasto silenzioso di #3651 — una
/// protezione dichiarata che non protegge. Questo test è ciò che quel fix non aveva.
/// </para>
/// <para>
/// Write-path: <c>PhotoBatchUploadRepository</c> legge <c>AsNoTracking()</c> e riattacca con
/// <c>Update()</c> (<c>:75</c>). L'aggregato <b>è</b> l'entità EF e trasporta il token letto, e il
/// test non-conteso lo verifica invece di assumerlo (#3688).
/// </para>
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "3651")]
public sealed class PhotoBatchUploadXminConcurrencyTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = null!;
    private string _connectionString = null!;
    private MeepleAiDbContext _dbContext = null!;

    public PhotoBatchUploadXminConcurrencyTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"photobatch_xmin_{Guid.NewGuid():N}";
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        _dbContext = _fixture.CreateDbContext(_connectionString);
        await _dbContext.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    private static PhotoBatchUploadRepository CreateRepository(MeepleAiDbContext dbContext) =>
        new(dbContext, new Mock<IDomainEventCollector>().Object);

    /// <summary>
    /// `UserId` ha una FK verso `users` (<c>HasOne&lt;UserEntity&gt;()</c> nella configuration):
    /// senza l'utente l'INSERT fallisce con 23503 prima del token, e il rosso direbbe la cosa
    /// sbagliata (pitfall #2620).
    /// </summary>
    private async Task<Guid> SeedBatchAsync()
    {
        var userId = Guid.NewGuid();
        _dbContext.Users.Add(new Api.Infrastructure.Entities.UserEntity
        {
            Id = userId,
            Email = $"fotografo-{userId:N}@meepleai.test",
            Tier = "free",
            Role = "user",
        });
        await _dbContext.SaveChangesAsync();

        var batch = PhotoBatchUpload.Create(userId, Guid.NewGuid(), "it", totalPages: 5);
        // RecordPageIndexed richiede lo stato Processing: senza, i test fallirebbero con una
        // InvalidOperationException di dominio invece che sul concurrency token.
        batch.StartProcessing();
        await CreateRepository(_dbContext).AddAsync(batch);
        await _dbContext.SaveChangesAsync();

        return batch.Id;
    }

    [Fact]
    public async Task Update_AfterConcurrentWrite_ThrowsConcurrencyException()
    {
        var id = await SeedBatchAsync();

        await using var firstContext = _fixture.CreateDbContext(_connectionString);
        await using var secondContext = _fixture.CreateDbContext(_connectionString);

        var seenByFirst = await firstContext.Set<PhotoBatchUpload>().FirstAsync(b => b.Id == id);
        var seenBySecond = await secondContext.Set<PhotoBatchUpload>().FirstAsync(b => b.Id == id);

        seenByFirst.RecordPageIndexed(pageNumber: 1, confidence: 0.9, warnings: []);
        await firstContext.SaveChangesAsync();

        // Il secondo worker chiude un'altra pagina partendo da un conteggio ormai superato.
        seenBySecond.RecordPageIndexed(pageNumber: 2, confidence: 0.4, warnings: []);

        var secondWrite = async () => await secondContext.SaveChangesAsync();

        await secondWrite.Should().ThrowAsync<DbUpdateConcurrencyException>(
            "due worker che chiudono pagine dello stesso batch devono escludersi: senza conflitto "
            + "rilevato un incremento sovrascrive l'altro e il batch risulta completo con meno "
            + "pagine di quante ne abbia indicizzate (#3651)");
    }

    [Fact]
    public async Task Update_WithoutConcurrentWrite_Succeeds()
    {
        var id = await SeedBatchAsync();

        await using var context = _fixture.CreateDbContext(_connectionString);
        var repository = CreateRepository(context);

        var batch = await repository.GetByIdAsync(id);
        batch.Should().NotBeNull();

        batch!.RecordPageIndexed(pageNumber: 1, confidence: 0.95, warnings: []);
        await repository.UpdateAsync(batch);

        var write = async () => await context.SaveChangesAsync();

        // Il repository legge AsNoTracking e riattacca con Update() (:75): se il token non
        // viaggiasse con l'aggregato, la WHERE avrebbe xmin = 0 e questa scrittura non contesa
        // fallirebbe comunque (#3688).
        await write.Should().NotThrowAsync(
            "una scrittura non contesa deve passare anche attraverso Update() su grafo detached");

        await using var verification = _fixture.CreateDbContext(_connectionString);
        var persisted = await verification.Set<PhotoBatchUpload>().FirstAsync(b => b.Id == id);
        persisted.IndexedPages.Should().Be(1);
    }
}
