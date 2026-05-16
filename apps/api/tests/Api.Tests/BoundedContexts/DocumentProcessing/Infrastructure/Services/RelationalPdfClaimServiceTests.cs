using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Infrastructure.Services;

/// <summary>
/// Integration tests for <see cref="RelationalPdfClaimService"/>.
/// Issue #892: verifies the atomic SQL UPDATE semantics — observable parity with
/// <c>InMemoryPdfClaimService</c> plus the concurrency invariant that exactly one
/// worker can claim a Pending PDF under contention.
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class RelationalPdfClaimServiceTests : SharedDatabaseTestBase<RelationalPdfClaimService>
{
    private Guid _testUserId;

    public RelationalPdfClaimServiceTests(SharedTestcontainersFixture fixture) : base(fixture)
    {
    }

    protected override RelationalPdfClaimService CreateRepository(MeepleAiDbContext dbContext)
        => new RelationalPdfClaimService(dbContext);

    private async Task SeedTestUserAsync()
    {
        _testUserId = Guid.NewGuid();
        DbContext.Users.Add(new UserEntity
        {
            Id = _testUserId,
            Email = $"claim-test-{_testUserId:N}@example.test",
            CreatedAt = DateTime.UtcNow
        });
        await DbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task TryClaimPendingAsync_WhenPdfIsPending_ReturnsTrueAndTransitionsToExtracting()
    {
        // Arrange
        await ResetDatabaseAsync();
        await SeedTestUserAsync();
        var pdfId = await SeedPdfAsync("Pending");

        // Act
        var claimed = await Repository.TryClaimPendingAsync(pdfId, CancellationToken.None);

        // Assert
        claimed.Should().BeTrue();
        var doc = await ReadFreshAsync(pdfId);
        doc!.ProcessingState.Should().Be("Extracting");
    }

    [Fact]
    public async Task TryClaimPendingAsync_WhenPdfIsPendingWithStaleError_ClearsProcessingError()
    {
        // Arrange
        await ResetDatabaseAsync();
        await SeedTestUserAsync();
        var pdfId = await SeedPdfAsync("Pending", processingError: "stale failure from prior worker");

        // Act
        var claimed = await Repository.TryClaimPendingAsync(pdfId, CancellationToken.None);

        // Assert
        claimed.Should().BeTrue();
        var doc = await ReadFreshAsync(pdfId);
        doc!.ProcessingState.Should().Be("Extracting");
        doc.ProcessingError.Should().BeNull();
    }

    [Theory]
    [InlineData("Uploading")]
    [InlineData("Extracting")]
    [InlineData("Chunking")]
    [InlineData("Embedding")]
    [InlineData("Indexing")]
    [InlineData("Ready")]
    [InlineData("Failed")]
    public async Task TryClaimPendingAsync_WhenPdfIsNotPending_ReturnsFalseAndDoesNotMutateState(string state)
    {
        // Arrange
        await ResetDatabaseAsync();
        await SeedTestUserAsync();
        var pdfId = await SeedPdfAsync(state);

        // Act
        var claimed = await Repository.TryClaimPendingAsync(pdfId, CancellationToken.None);

        // Assert
        claimed.Should().BeFalse();
        var doc = await ReadFreshAsync(pdfId);
        doc!.ProcessingState.Should().Be(state);
    }

    [Fact]
    public async Task TryClaimPendingAsync_WhenPdfDoesNotExist_ReturnsFalse()
    {
        // Arrange
        await ResetDatabaseAsync();
        await SeedTestUserAsync();

        // Act
        var claimed = await Repository.TryClaimPendingAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        claimed.Should().BeFalse();
    }

    [Fact]
    public async Task TryClaimPendingAsync_TwoConcurrentClaimsOnSamePending_OnlyOneWins()
    {
        // Arrange — single Pending PDF, two independent service instances (their own DbContext).
        await ResetDatabaseAsync();
        await SeedTestUserAsync();
        var pdfId = await SeedPdfAsync("Pending");

        // Own the second DbContext explicitly so it's disposed at the end of the test —
        // CreateIndependentRepository() builds a context that the base class does not track
        // for cleanup, and Task.WhenAll otherwise leaves the Npgsql connection in the pool
        // until GC.
        var sutA = Repository;
        await using var independentCtx = CreateIndependentDbContext();
        var sutB = CreateRepository(independentCtx);

        // Act — fire both claims as close to simultaneously as possible.
        var taskA = Task.Run(() => sutA.TryClaimPendingAsync(pdfId, CancellationToken.None));
        var taskB = Task.Run(() => sutB.TryClaimPendingAsync(pdfId, CancellationToken.None));
        await Task.WhenAll(taskA, taskB);

        // Assert — atomic SQL UPDATE guarantees exactly one winner.
        var winners = new[] { taskA.Result, taskB.Result };
        winners.Count(r => r).Should().Be(1, "exactly one concurrent claim must transition the row");
        winners.Count(r => !r).Should().Be(1, "the loser must observe the row already claimed");

        var doc = await ReadFreshAsync(pdfId);
        doc!.ProcessingState.Should().Be("Extracting");
    }

    private async Task<Guid> SeedPdfAsync(string processingState, string? processingError = null)
    {
        var id = Guid.NewGuid();
        DbContext.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = id,
            FileName = "test.pdf",
            FilePath = "/tmp/test.pdf",
            FileSizeBytes = 1024,
            UploadedByUserId = _testUserId,
            ProcessingState = processingState,
            ProcessingError = processingError
        });
        await DbContext.SaveChangesAsync();
        return id;
    }

    /// <summary>
    /// Reads the PDF from a fresh DbContext so we observe the row state written by
    /// the raw SQL UPDATE, bypassing any L1 cache staleness on the test's context.
    /// </summary>
    private async Task<PdfDocumentEntity?> ReadFreshAsync(Guid id)
    {
        await using var fresh = CreateIndependentDbContext();
        return await fresh.PdfDocuments.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
    }
}
