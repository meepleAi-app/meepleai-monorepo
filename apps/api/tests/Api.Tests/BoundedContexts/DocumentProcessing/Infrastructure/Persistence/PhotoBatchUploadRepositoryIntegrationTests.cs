using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Infrastructure.Persistence;

/// <summary>
/// Integration tests for <see cref="PhotoBatchUploadRepository"/> against a real PostgreSQL
/// database via Testcontainers. Validates the new <c>GetPageTextByParagraphNumberAsync</c>
/// query path introduced by issue #747 — paragraph-number lookup distinct from physical
/// page index.
/// </summary>
/// <remarks>
/// Postgres-backed because the lookup relies on the native <c>integer[]</c> column type and
/// the <c>paragraph_numbers @> ARRAY[N]</c> / <c>= ANY(paragraph_numbers)</c> translation —
/// neither is faithfully reproducible by EF Core InMemory, which silently returns the wrong
/// result for array containment checks.
/// </remarks>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class PhotoBatchUploadRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private MeepleAiDbContext _dbContext = null!;
    private PhotoBatchUploadRepository _repository = null!;
    private string _databaseName = null!;
    private string _connectionString = null!;
    private Guid _userId;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public PhotoBatchUploadRepositoryIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_photobatch_repo_{Guid.NewGuid():N}";
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        _dbContext = _fixture.CreateDbContext(_connectionString);
        await _dbContext.Database.MigrateAsync(Token);

        _userId = Guid.NewGuid();

        // Seed the user row required by FK_photo_batch_uploads_users_user_id (Restrict delete).
        _dbContext.Users.Add(new Api.Infrastructure.Entities.UserEntity
        {
            Id = _userId,
            Email = $"photobatch-{_userId:N}@test.local",
            DisplayName = "PhotoBatch Test User",
            Role = "User",
            CreatedAt = DateTime.UtcNow,
        });
        await _dbContext.SaveChangesAsync(Token);

        var mockCollector = new Mock<IDomainEventCollector>();
        mockCollector
            .Setup(e => e.GetAndClearEvents())
            .Returns(new List<Api.SharedKernel.Domain.Interfaces.IDomainEvent>().AsReadOnly());

        _repository = new PhotoBatchUploadRepository(_dbContext, mockCollector.Object);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    // -----------------------------------------------------------------------
    // GetPageTextByParagraphNumberAsync — issue #747 PR-A
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetPageTextByParagraphNumber_MatchingPage_ReturnsPageNumberAndExtractedText()
    {
        // Arrange — single batch with one page tagged with paragraph 42.
        var batchId = await SeedBatchWithPagesAsync(
            (PageNumber: 3, Text: "Il pedone si sposta di una casella.", ParagraphNumbers: new[] { 42 }));

        // Act
        var match = await _repository.GetPageTextByParagraphNumberAsync(batchId, 42, Token);

        // Assert — issue #747 PR-B: signature now returns (PageNumber, Text) tuple.
        match.Should().NotBeNull();
        match!.Value.PageNumber.Should().Be(3);
        match!.Value.Text.Should().Be("Il pedone si sposta di una casella.");
    }

    [Fact]
    public async Task GetPageTextByParagraphNumber_NoMatch_ReturnsNull()
    {
        // Arrange — page exists but carries different paragraph numbers.
        var batchId = await SeedBatchWithPagesAsync(
            (PageNumber: 1, Text: "Pagina 1", ParagraphNumbers: new[] { 10, 11 }));

        // Act
        var match = await _repository.GetPageTextByParagraphNumberAsync(batchId, 42, Token);

        // Assert
        match.Should().BeNull();
    }

    [Fact]
    public async Task GetPageTextByParagraphNumber_DifferentBatch_ReturnsNull()
    {
        // Arrange — paragraph 42 lives in OTHER_BATCH, not in the queried batch.
        await SeedBatchWithPagesAsync(
            (PageNumber: 1, Text: "Wrong batch text", ParagraphNumbers: new[] { 42 }));

        var queriedBatchId = await SeedBatchWithPagesAsync(
            (PageNumber: 1, Text: "Queried batch text", ParagraphNumbers: new[] { 7 }));

        // Act
        var match = await _repository.GetPageTextByParagraphNumberAsync(queriedBatchId, 42, Token);

        // Assert — repository must filter by PhotoBatchUploadId.
        match.Should().BeNull();
    }

    [Fact]
    public async Task GetPageTextByParagraphNumber_MultiplePagesWithSameParagraph_ReturnsFirstByPageNumber()
    {
        // Arrange — paragraph 7 appears on physical pages 5 AND 2; expect page 2's text.
        // Insertion order intentionally reversed to prove the ORDER BY is applied at query
        // time, not at insertion time.
        var batchId = await SeedBatchWithPagesAsync(
            (PageNumber: 5, Text: "Pagina 5 contiene paragrafo 7 (duplicato)", ParagraphNumbers: new[] { 7 }),
            (PageNumber: 2, Text: "Pagina 2 con paragrafo 7", ParagraphNumbers: new[] { 7 }));

        // Act
        var match = await _repository.GetPageTextByParagraphNumberAsync(batchId, 7, Token);

        // Assert
        match.Should().NotBeNull();
        match!.Value.PageNumber.Should().Be(2, "lowest page_number wins the deterministic tie-break");
        match!.Value.Text.Should().Be("Pagina 2 con paragrafo 7");
    }

    [Fact]
    public async Task GetPageTextByParagraphNumber_EmptyParagraphsArray_ReturnsNull()
    {
        // Arrange — legacy upload without paragraph extraction. paragraph_numbers = '{}'.
        var batchId = await SeedBatchWithPagesAsync(
            (PageNumber: 1, Text: "Legacy page (no paragraphs)", ParagraphNumbers: Array.Empty<int>()));

        // Act
        var match = await _repository.GetPageTextByParagraphNumberAsync(batchId, 42, Token);

        // Assert
        match.Should().BeNull();
    }

    [Fact]
    public async Task GetPageTextByParagraphNumber_PageWithMultipleParagraphs_MatchesAnyOfThem()
    {
        // Arrange — one page carries paragraphs 10, 11, 12 (spread covers 3 paragraphs).
        var batchId = await SeedBatchWithPagesAsync(
            (PageNumber: 4, Text: "Spread con paragrafi 10-12", ParagraphNumbers: new[] { 10, 11, 12 }));

        // Act + Assert — every paragraph number in the array must return the same page tuple.
        foreach (var paragraph in new[] { 10, 11, 12 })
        {
            var match = await _repository.GetPageTextByParagraphNumberAsync(batchId, paragraph, Token);
            match.Should().NotBeNull($"paragraph {paragraph} is on page 4");
            match!.Value.PageNumber.Should().Be(4);
            match!.Value.Text.Should().Be("Spread con paragrafi 10-12");
        }
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private async Task<Guid> SeedBatchWithPagesAsync(
        params (int PageNumber, string Text, int[] ParagraphNumbers)[] pages)
    {
        var batch = PhotoBatchUpload.Create(_userId, Guid.NewGuid(), "it", pages.Length);
        _dbContext.PhotoBatchUploads.Add(batch);

        foreach (var (pageNumber, text, paragraphNumbers) in pages)
        {
            var page = PhotoBatchPage.Create(
                batchId: batch.Id,
                pageNumber: pageNumber,
                blobKey: $"blob://test/{batch.Id}/page-{pageNumber}",
                confidence: 0.95,
                orientation: PageOrientation.Portrait,
                isBlank: false,
                warnings: Array.Empty<string>(),
                extractedText: text,
                paragraphNumbers: paragraphNumbers);
            _dbContext.PhotoBatchPages.Add(page);
        }

        await _dbContext.SaveChangesAsync(Token);
        _dbContext.ChangeTracker.Clear();
        return batch.Id;
    }
}
