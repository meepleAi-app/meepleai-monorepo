using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.Authentication;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.Integration.KnowledgeBase;

/// <summary>
/// Postgres-backed integration tests for the #2324 distinct-thread upgrade of
/// the chunk usage counter. The unit tests cover happy/sad paths with the EF
/// InMemory provider; this fixture pins the wire behaviour that matters in
/// production:
/// <list type="bullet">
///   <item><description>The migration applies and the junction table is
///     queryable (composite PK + secondary index materialise).</description></item>
///   <item><description>The handler increments correctly across distinct
///     threads but suppresses within the same thread.</description></item>
///   <item><description>Cascade delete on the thread FK wipes the junction.</description></item>
///   <item><description>Replaying the same (thread, message, chunks) command
///     is idempotent (composite PK + handler early-return).</description></item>
/// </list>
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "2324")]
[Collection("Integration-GroupA")]
public class IncrementChunkUsageCountsHandlerIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private MeepleAiDbContext? _dbContext;
    private string? _connectionString;
    private readonly string _databaseName = $"test_chunk_usage_inc_{Guid.NewGuid():N}";

    public IncrementChunkUsageCountsHandlerIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        await TestcontainersWaitHelpers.WaitForPostgresReadyAsync(_connectionString);
        _dbContext = await Api.Tests.Infrastructure.TestHelpers.CreateDbContextAndMigrateAsync(_connectionString);
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null) await _dbContext.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    [Fact]
    public async Task Handle_DistinctThreadIncrement_AcrossTwoThreads_TicksTwice()
    {
        var ct = TestContext.Current.CancellationToken;
        var (pdfId, chunk) = await SeedSharedGameAndChunkAsync(chunkIndex: 5, ct);

        var thread1 = await SeedThreadAsync(ct);
        var thread2 = await SeedThreadAsync(ct);

        var sut = new IncrementChunkUsageCountsCommandHandler(
            _dbContext!,
            NullLogger<IncrementChunkUsageCountsCommandHandler>.Instance);

        await sut.Handle(
            new IncrementChunkUsageCountsCommand(
                thread1.Id,
                Guid.NewGuid(),
                new[] { new ChunkUsageLocator(pdfId, 5) }),
            ct);
        await sut.Handle(
            new IncrementChunkUsageCountsCommand(
                thread2.Id,
                Guid.NewGuid(),
                new[] { new ChunkUsageLocator(pdfId, 5) }),
            ct);

        var reloaded = await _dbContext!.TextChunks.AsNoTracking().FirstAsync(c => c.Id == chunk.Id, ct);
        reloaded.UsageCount.Should().Be(2);

        var junctionRows = await _dbContext.ChatMessageChunkCitations.AsNoTracking()
            .Where(j => j.ChunkId == chunk.Id)
            .CountAsync(ct);
        junctionRows.Should().Be(2);
    }

    [Fact]
    public async Task Handle_SameThreadTwoMessages_TicksOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        var (pdfId, chunk) = await SeedSharedGameAndChunkAsync(chunkIndex: 7, ct);
        var thread = await SeedThreadAsync(ct);

        var sut = new IncrementChunkUsageCountsCommandHandler(
            _dbContext!,
            NullLogger<IncrementChunkUsageCountsCommandHandler>.Instance);

        var msg1 = Guid.NewGuid();
        var msg2 = Guid.NewGuid();

        await sut.Handle(
            new IncrementChunkUsageCountsCommand(
                thread.Id, msg1, new[] { new ChunkUsageLocator(pdfId, 7) }),
            ct);
        await sut.Handle(
            new IncrementChunkUsageCountsCommand(
                thread.Id, msg2, new[] { new ChunkUsageLocator(pdfId, 7) }),
            ct);

        var reloaded = await _dbContext!.TextChunks.AsNoTracking().FirstAsync(c => c.Id == chunk.Id, ct);
        reloaded.UsageCount.Should().Be(1);

        var junctionRows = await _dbContext.ChatMessageChunkCitations.AsNoTracking()
            .Where(j => j.ThreadId == thread.Id && j.ChunkId == chunk.Id)
            .CountAsync(ct);
        junctionRows.Should().Be(2);
    }

    [Fact]
    public async Task DeleteThread_CascadesJunctionRows()
    {
        var ct = TestContext.Current.CancellationToken;
        var (pdfId, chunk) = await SeedSharedGameAndChunkAsync(chunkIndex: 11, ct);
        var thread = await SeedThreadAsync(ct);

        var sut = new IncrementChunkUsageCountsCommandHandler(
            _dbContext!,
            NullLogger<IncrementChunkUsageCountsCommandHandler>.Instance);

        await sut.Handle(
            new IncrementChunkUsageCountsCommand(
                thread.Id, Guid.NewGuid(), new[] { new ChunkUsageLocator(pdfId, 11) }),
            ct);

        // Pre-condition: junction row exists.
        var pre = await _dbContext!.ChatMessageChunkCitations.AsNoTracking()
            .Where(j => j.ThreadId == thread.Id)
            .CountAsync(ct);
        pre.Should().Be(1);

        // Act — drop the thread → FK cascade on the junction.
        _dbContext.ChatThreads.Remove(thread);
        await _dbContext.SaveChangesAsync(ct);

        var post = await _dbContext.ChatMessageChunkCitations.AsNoTracking()
            .Where(j => j.ThreadId == thread.Id)
            .CountAsync(ct);
        post.Should().Be(0);

        // The chunk row survives (it belongs to the pdf, not the thread).
        var chunkSurvives = await _dbContext.TextChunks.AsNoTracking()
            .AnyAsync(c => c.Id == chunk.Id, ct);
        chunkSurvives.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ReplayedCommand_IsIdempotent_NoDuplicateJunctionRow()
    {
        var ct = TestContext.Current.CancellationToken;
        var (pdfId, chunk) = await SeedSharedGameAndChunkAsync(chunkIndex: 13, ct);
        var thread = await SeedThreadAsync(ct);

        var sut = new IncrementChunkUsageCountsCommandHandler(
            _dbContext!,
            NullLogger<IncrementChunkUsageCountsCommandHandler>.Instance);

        var msgId = Guid.NewGuid();
        var cmd = new IncrementChunkUsageCountsCommand(
            thread.Id, msgId, new[] { new ChunkUsageLocator(pdfId, 13) });

        await sut.Handle(cmd, ct);
        await sut.Handle(cmd, ct); // replay — must not double-tick or violate PK

        var reloaded = await _dbContext!.TextChunks.AsNoTracking().FirstAsync(c => c.Id == chunk.Id, ct);
        reloaded.UsageCount.Should().Be(1);

        var junctionRows = await _dbContext.ChatMessageChunkCitations.AsNoTracking()
            .Where(j => j.ThreadId == thread.Id && j.MessageId == msgId && j.ChunkId == chunk.Id)
            .CountAsync(ct);
        junctionRows.Should().Be(1);
    }

    // ─── Seeding helpers ──────────────────────────────────────────────────────

    private async Task<(Guid pdfId, TextChunkEntity chunk)> SeedSharedGameAndChunkAsync(
        int chunkIndex,
        CancellationToken ct)
    {
        var userId = Guid.NewGuid();
        var user = new UserEntity
        {
            Id = userId,
            Email = $"u{userId:N}@test",
            DisplayName = "Tester",
            Role = "User",
        };
        var sharedGame = new SharedGameEntity { Id = Guid.NewGuid(), Title = $"Game {Guid.NewGuid():N}" };
        var pdfId = Guid.NewGuid();
        var pdf = new PdfDocumentEntity
        {
            Id = pdfId,
            FileName = "test.pdf",
            FilePath = "/tmp/test.pdf",
            FileSizeBytes = 1024,
            ContentType = "application/pdf",
            UploadedByUserId = userId,
            UploadedAt = DateTime.UtcNow,
            ProcessingState = "Ready",
            ProcessedAt = DateTime.UtcNow,
            SharedGameId = sharedGame.Id,
        };
        var chunk = new TextChunkEntity
        {
            Id = Guid.NewGuid(),
            PdfDocumentId = pdfId,
            ChunkIndex = chunkIndex,
            Content = $"snippet-{chunkIndex}",
            CharacterCount = 12,
            CreatedAt = DateTime.UtcNow,
            SharedGameId = sharedGame.Id,
        };

        _dbContext!.Users.Add(user);
        _dbContext.SharedGames.Add(sharedGame);
        _dbContext.PdfDocuments.Add(pdf);
        _dbContext.TextChunks.Add(chunk);
        await _dbContext.SaveChangesAsync(ct);

        return (pdfId, chunk);
    }

    private async Task<ChatThreadEntity> SeedThreadAsync(CancellationToken ct)
    {
        var userId = Guid.NewGuid();
        var user = new UserEntity
        {
            Id = userId,
            Email = $"u{userId:N}@test",
            DisplayName = "Threader",
            Role = "User",
        };
        var thread = new ChatThreadEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            LastMessageAt = DateTime.UtcNow,
        };

        _dbContext!.Users.Add(user);
        _dbContext.ChatThreads.Add(thread);
        await _dbContext.SaveChangesAsync(ct);

        return thread;
    }
}
