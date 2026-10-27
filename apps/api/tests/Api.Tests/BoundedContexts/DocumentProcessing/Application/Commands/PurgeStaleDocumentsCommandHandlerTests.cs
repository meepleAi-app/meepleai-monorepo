using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Unit tests for PurgeStaleDocumentsCommandHandler.
/// Issue #3564: the handler mutates entities loaded from the DbContext, whose production default is
/// QueryTrackingBehavior.NoTracking (PERF-06, InfrastructureServiceExtensions). Without .AsTracking()
/// SaveChangesAsync was a silent no-op while the handler still reported the selected count, leaving
/// stale documents stuck in their in-flight state (and therefore un-reindexable, 409).
/// The DbContext here is built with the SAME tracking behavior as production — a context left on the
/// EF default (tracking) would make these tests pass even with the bug present.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public class PurgeStaleDocumentsCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);

    private static MeepleAiDbContext CreateDbContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName)
            // Mirrors InfrastructureServiceExtensions (PERF-06). Required for these tests to be
            // meaningful: the production default is what made the mutation a no-op.
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .Options;

        return new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);
    }

    private static PurgeStaleDocumentsCommandHandler CreateHandler(MeepleAiDbContext ctx) =>
        new(ctx,
            // The real pipeline over the same context, as in production: the handler is meant to close the vector
            // document of a PDF it fails, and a mock would only check that it asked.
            new PdfIndexingPipeline(
                ctx, new Mock<IMediator>().Object, TimeProvider.System, Mock.Of<ISemanticResponseCache>(),
                NullLogger<PdfIndexingPipeline>.Instance),
            new FakeTimeProvider(Now),
            NullLogger<PurgeStaleDocumentsCommandHandler>.Instance);

    private static PdfDocumentEntity Pdf(string fileName, string state, DateTime uploadedAt) => new()
    {
        Id = Guid.NewGuid(),
        FileName = fileName,
        FilePath = $"pdfs/{fileName}",
        ContentType = "application/pdf",
        ProcessingState = state,
        UploadedAt = uploadedAt
    };

    [Fact]
    public async Task Handle_WhenDocumentIsStale_PersistsFailedState()
    {
        // Arrange — a document stuck in an active state well beyond the 24h threshold
        var dbName = Guid.NewGuid().ToString();
        using var ctx = CreateDbContext(dbName);
        var stale = Pdf("stuck.pdf", "Embedding", Now.UtcDateTime.AddDays(-20));
        ctx.PdfDocuments.Add(stale);
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        // Act
        var result = await CreateHandler(ctx).Handle(new PurgeStaleDocumentsCommand(), default);

        // Assert — the reported count must reflect what was actually written
        result.PurgedCount.Should().Be(1);

        using var verifyCtx = CreateDbContext(dbName);
        var persisted = await verifyCtx.PdfDocuments.SingleAsync(p => p.Id == stale.Id);
        persisted.ProcessingState.Should().Be("Failed");
        persisted.ProcessingError.Should().Be("Processing timed out (stale) - purged by admin");
        persisted.ErrorCategory.Should().Be("Service");
        persisted.FailedAtState.Should().Be("Embedding");
        persisted.ProcessedAt.Should().Be(Now.UtcDateTime);
    }

    /// <summary>
    /// Seguito di #4609 (#4660 punto 1): il PDF scaduto va a `Failed` e il suo documento vettoriale, nato
    /// `processing` da BeginIndexingAsync, non resta in elaborazione per sempre. Un documento `completed` di un
    /// altro PDF non si tocca.
    /// </summary>
    [Fact]
    public async Task Handle_WhenDocumentIsStale_AlsoClosesItsVectorDocument_AndOnlyThat()
    {
        var dbName = Guid.NewGuid().ToString();
        using var ctx = CreateDbContext(dbName);
        var stale = Pdf("stuck.pdf", "Indexing", Now.UtcDateTime.AddDays(-20));
        var other = Pdf("done.pdf", "Ready", Now.UtcDateTime.AddDays(-20));
        ctx.PdfDocuments.AddRange(stale, other);
        ctx.VectorDocuments.Add(new VectorDocumentEntity
        {
            Id = Guid.NewGuid(), PdfDocumentId = stale.Id, ChunkCount = 3, TotalCharacters = 30, IndexingStatus = "processing",
        });
        ctx.VectorDocuments.Add(new VectorDocumentEntity
        {
            Id = Guid.NewGuid(), PdfDocumentId = other.Id, ChunkCount = 3, TotalCharacters = 30, IndexingStatus = "completed",
            IndexedAt = Now.UtcDateTime.AddDays(-20),
        });
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        await CreateHandler(ctx).Handle(new PurgeStaleDocumentsCommand(), default);

        using var verifyCtx = CreateDbContext(dbName);
        var staleRow = await verifyCtx.VectorDocuments.SingleAsync(v => v.PdfDocumentId == stale.Id);
        staleRow.IndexingStatus.Should().Be("failed");
        staleRow.IndexingError.Should().Be("Processing failed (Service)");
        (await verifyCtx.VectorDocuments.SingleAsync(v => v.PdfDocumentId == other.Id)).IndexingStatus
            .Should().Be("completed");
    }

    [Theory]
    [InlineData("Uploading")]
    [InlineData("Extracting")]
    [InlineData("Chunking")]
    [InlineData("Embedding")]
    [InlineData("Indexing")]
    public async Task Handle_PersistsFailedState_ForEveryActiveState(string state)
    {
        var dbName = Guid.NewGuid().ToString();
        using var ctx = CreateDbContext(dbName);
        var stale = Pdf($"{state}.pdf", state, Now.UtcDateTime.AddDays(-2));
        ctx.PdfDocuments.Add(stale);
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var result = await CreateHandler(ctx).Handle(new PurgeStaleDocumentsCommand(), default);

        result.PurgedCount.Should().Be(1);

        using var verifyCtx = CreateDbContext(dbName);
        var persisted = await verifyCtx.PdfDocuments.SingleAsync(p => p.Id == stale.Id);
        persisted.ProcessingState.Should().Be("Failed");
        persisted.FailedAtState.Should().Be(state);
    }

    [Fact]
    public async Task Handle_WhenDocumentIsRecent_LeavesItUntouched()
    {
        var dbName = Guid.NewGuid().ToString();
        using var ctx = CreateDbContext(dbName);
        var recent = Pdf("recent.pdf", "Embedding", Now.UtcDateTime.AddHours(-1));
        ctx.PdfDocuments.Add(recent);
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var result = await CreateHandler(ctx).Handle(new PurgeStaleDocumentsCommand(), default);

        result.PurgedCount.Should().Be(0);

        using var verifyCtx = CreateDbContext(dbName);
        var persisted = await verifyCtx.PdfDocuments.SingleAsync(p => p.Id == recent.Id);
        persisted.ProcessingState.Should().Be("Embedding");
    }

    /// <summary>
    /// Issue #3572: the handler now saves one document per iteration and clears the ChangeTracker in
    /// a finally block. This is the regression that refactor can introduce: if the candidates were
    /// still loaded as a tracked list up front, the first Clear() would detach the rest and every
    /// later mutation would be a silent no-op again — only the first document would persist.
    /// </summary>
    [Fact]
    public async Task Handle_PersistsEveryDocumentInTheBatch_NotJustTheFirst()
    {
        var dbName = Guid.NewGuid().ToString();
        using var ctx = CreateDbContext(dbName);
        var stale = Enumerable.Range(0, 5)
            .Select(i => Pdf($"stuck-{i}.pdf", "Embedding", Now.UtcDateTime.AddDays(-10)))
            .ToArray();
        ctx.PdfDocuments.AddRange(stale);
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var result = await CreateHandler(ctx).Handle(new PurgeStaleDocumentsCommand(), default);

        result.PurgedCount.Should().Be(5);

        using var verifyCtx = CreateDbContext(dbName);
        foreach (var doc in stale)
        {
            var persisted = await verifyCtx.PdfDocuments.SingleAsync(p => p.Id == doc.Id);
            persisted.ProcessingState.Should().Be("Failed");
            persisted.FailedAtState.Should().Be("Embedding");
        }
    }

    /// <summary>
    /// Issue #3572: the returned count must reflect what was WRITTEN. Here a document reaches a
    /// terminal state before the handler runs, so it is neither purged nor counted.
    /// NOTE the limit of this test: the transition happens before the call, so the document is
    /// excluded by the candidate query itself — it does NOT exercise the tracked re-check inside the
    /// loop (which covers a transition between the candidate query and that document's iteration).
    /// That window, like the DbUpdateConcurrencyException branch, needs a real database with xmin
    /// concurrency tokens: EF InMemory has no concurrency tokens, so neither is reachable here.
    /// </summary>
    [Fact]
    public async Task Handle_DoesNotCountDocumentsThatLeftTheActiveStates()
    {
        var dbName = Guid.NewGuid().ToString();
        using var ctx = CreateDbContext(dbName);
        var stale = Pdf("stuck.pdf", "Embedding", Now.UtcDateTime.AddDays(-10));
        var alsoStale = Pdf("stuck-2.pdf", "Chunking", Now.UtcDateTime.AddDays(-10));
        ctx.PdfDocuments.AddRange(stale, alsoStale);
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        // Simulate the race: one candidate reaches a terminal state before the handler gets to it.
        await using (var racingCtx = CreateDbContext(dbName))
        {
            var raced = await racingCtx.PdfDocuments.AsTracking().SingleAsync(p => p.Id == alsoStale.Id);
            raced.ProcessingState = "Ready";
            await racingCtx.SaveChangesAsync();
        }

        var result = await CreateHandler(ctx).Handle(new PurgeStaleDocumentsCommand(), default);

        result.PurgedCount.Should().Be(1, "only the document still in an active state was purged");

        using var verifyCtx = CreateDbContext(dbName);
        (await verifyCtx.PdfDocuments.SingleAsync(p => p.Id == stale.Id)).ProcessingState.Should().Be("Failed");
        (await verifyCtx.PdfDocuments.SingleAsync(p => p.Id == alsoStale.Id)).ProcessingState.Should().Be("Ready");
    }

    [Fact]
    public async Task Handle_LeavesTerminalAndPendingStatesUntouched()
    {
        var dbName = Guid.NewGuid().ToString();
        using var ctx = CreateDbContext(dbName);
        var ready = Pdf("ready.pdf", "Ready", Now.UtcDateTime.AddDays(-30));
        var failed = Pdf("failed.pdf", "Failed", Now.UtcDateTime.AddDays(-30));
        var pending = Pdf("pending.pdf", "Pending", Now.UtcDateTime.AddDays(-30));
        ctx.PdfDocuments.AddRange(ready, failed, pending);
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var result = await CreateHandler(ctx).Handle(new PurgeStaleDocumentsCommand(), default);

        result.PurgedCount.Should().Be(0);

        using var verifyCtx = CreateDbContext(dbName);
        (await verifyCtx.PdfDocuments.SingleAsync(p => p.Id == ready.Id)).ProcessingState.Should().Be("Ready");
        (await verifyCtx.PdfDocuments.SingleAsync(p => p.Id == failed.Id)).ProcessingState.Should().Be("Failed");
        (await verifyCtx.PdfDocuments.SingleAsync(p => p.Id == pending.Id)).ProcessingState.Should().Be("Pending");
    }
}
