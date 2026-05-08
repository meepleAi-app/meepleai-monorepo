using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetKbDocumentById;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Wave 3 Phase 3 (Issue #805 / PR #732 §6.3.1) — spec-conformant rewrite tests
/// for <see cref="GetKbDocumentByIdHandler"/>. Replaces the prior #730 baseline
/// suite (admin-gated diagnostic fields) with the new contract: 423 Locked
/// when processingStatus != 'ready', 4-value docType + processingStatus enums,
/// gameName + uploaderName joins, tags Gate B v1 stub.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class GetKbDocumentByIdHandlerTests
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GetKbDocumentByIdHandler> _logger;
    private readonly ISharedGameRepository _sharedGameRepository;
    private readonly IHybridCacheService _cache;
    private readonly GetKbDocumentByIdHandler _handler;

    public GetKbDocumentByIdHandlerTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _logger = Substitute.For<ILogger<GetKbDocumentByIdHandler>>();
        _sharedGameRepository = Substitute.For<ISharedGameRepository>();
        _sharedGameRepository
            .GetNamesByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(_ => new Dictionary<Guid, string>());
        _cache = new TestHybridCacheService();
        _handler = new GetKbDocumentByIdHandler(_dbContext, _sharedGameRepository, _cache, _logger);
    }

    [Fact]
    public async Task Handle_ReadyDoc_ReturnsFullDtoWithReadyStatus()
    {
        var docId = Guid.NewGuid();
        var sharedGameId = Guid.NewGuid();
        var pdf = SeedPdf(
            id: docId,
            fileName: "Catan rulebook.pdf",
            processingState: "Ready",
            documentCategory: "Rulebook",
            sharedGameId: sharedGameId,
            pageCount: 32);
        _dbContext.PdfDocuments.Add(pdf);
        _dbContext.VectorDocuments.Add(new VectorDocumentEntity
        {
            Id = Guid.NewGuid(),
            PdfDocumentId = docId,
            GameId = sharedGameId,
            ChunkCount = 120,
            IndexedAt = DateTime.UtcNow,
            IndexingStatus = "Completed"
        });
        SeedUploader(pdf.UploadedByUserId, "Marco");
        await _dbContext.SaveChangesAsync();

        _sharedGameRepository
            .GetNamesByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(_ => new Dictionary<Guid, string> { [sharedGameId] = "Catan" });

        var query = new GetKbDocumentByIdQuery(docId, RequestingUserId: pdf.UploadedByUserId, UserIsAdmin: false);

        var dto = await _handler.Handle(query, CancellationToken.None);

        dto.Should().NotBeNull();
        dto.Id.Should().Be(docId);
        dto.Title.Should().Be("Catan rulebook"); // .pdf trimmed
        dto.DocType.Should().Be("rulebook");
        dto.ProcessingStatus.Should().Be("ready");
        dto.GameId.Should().Be(sharedGameId);
        dto.GameName.Should().Be("Catan");
        dto.UploaderName.Should().Be("Marco");
        dto.ChunkCount.Should().Be(120);
        dto.PageCount.Should().Be(32);
        dto.Tags.Should().BeEmpty(); // Gate B v1 carryover
    }

    [Theory]
    [InlineData("Pending", "queued")]
    [InlineData("Uploading", "queued")]
    [InlineData("Extracting", "processing")]
    [InlineData("Chunking", "processing")]
    [InlineData("Embedding", "processing")]
    [InlineData("Indexing", "processing")]
    public async Task Handle_NonReadyDoc_Throws423Locked(string state, string expectedStatus)
    {
        var docId = Guid.NewGuid();
        var pdf = SeedPdf(id: docId, processingState: state);
        _dbContext.PdfDocuments.Add(pdf);
        SeedUploader(pdf.UploadedByUserId);
        await _dbContext.SaveChangesAsync();

        var query = new GetKbDocumentByIdQuery(docId, RequestingUserId: pdf.UploadedByUserId, UserIsAdmin: false);

        var act = () => _handler.Handle(query, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<LockedException>();
        ex.And.Message.Should().Contain(expectedStatus);
    }

    [Fact]
    public async Task Handle_FailedDoc_Throws423LockedWithFailedStatus()
    {
        var docId = Guid.NewGuid();
        var pdf = SeedPdf(id: docId, processingState: "Failed");
        _dbContext.PdfDocuments.Add(pdf);
        SeedUploader(pdf.UploadedByUserId);
        await _dbContext.SaveChangesAsync();

        var query = new GetKbDocumentByIdQuery(docId, RequestingUserId: pdf.UploadedByUserId, UserIsAdmin: false);

        var act = () => _handler.Handle(query, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<LockedException>();
        ex.And.Message.Should().Contain("failed");
    }

    [Theory]
    [InlineData("Rulebook", "rulebook")]
    [InlineData("Errata", "errata")]
    [InlineData("Expansion", "guide")]
    [InlineData("QuickStart", "guide")]
    [InlineData("Reference", "guide")]
    [InlineData("PlayerAid", "guide")]
    [InlineData("Other", "guide")]
    public async Task Handle_DocTypeMapping_CollapsesTo4Values(string raw, string expected)
    {
        var docId = Guid.NewGuid();
        var pdf = SeedPdf(id: docId, processingState: "Ready", documentCategory: raw);
        _dbContext.PdfDocuments.Add(pdf);
        SeedUploader(pdf.UploadedByUserId);
        await _dbContext.SaveChangesAsync();

        var dto = await _handler.Handle(
            new GetKbDocumentByIdQuery(docId, pdf.UploadedByUserId, UserIsAdmin: false),
            CancellationToken.None);

        dto.DocType.Should().Be(expected);
    }

    [Fact]
    public async Task Handle_NoSharedGame_GameNameIsNull()
    {
        var docId = Guid.NewGuid();
        var pdf = SeedPdf(id: docId, processingState: "Ready", sharedGameId: null);
        _dbContext.PdfDocuments.Add(pdf);
        SeedUploader(pdf.UploadedByUserId);
        await _dbContext.SaveChangesAsync();

        var dto = await _handler.Handle(
            new GetKbDocumentByIdQuery(docId, pdf.UploadedByUserId, UserIsAdmin: false),
            CancellationToken.None);

        dto.GameId.Should().BeNull();
        dto.GameName.Should().BeNull();
    }

    [Fact]
    public async Task Handle_UploaderMissingDisplayName_FallsBackToEmail()
    {
        var docId = Guid.NewGuid();
        var uploaderId = Guid.NewGuid();
        var pdf = SeedPdf(id: docId, processingState: "Ready");
        pdf.UploadedByUserId = uploaderId;
        _dbContext.PdfDocuments.Add(pdf);
        _dbContext.Users.Add(new UserEntity
        {
            Id = uploaderId,
            Email = "marco@example.test",
            DisplayName = null,
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        var dto = await _handler.Handle(
            new GetKbDocumentByIdQuery(docId, uploaderId, UserIsAdmin: false),
            CancellationToken.None);

        dto.UploaderName.Should().Be("marco@example.test");
    }

    [Fact]
    public async Task Handle_DocNotFound_Throws404()
    {
        var query = new GetKbDocumentByIdQuery(Guid.NewGuid(), RequestingUserId: Guid.NewGuid(), UserIsAdmin: false);

        await _handler.Invoking(h => h.Handle(query, CancellationToken.None))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_PrivateDocOtherUser_Throws403()
    {
        var docId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var pdf = SeedPdf(id: docId, processingState: "Ready", isPublic: false);
        pdf.UploadedByUserId = ownerId;
        _dbContext.PdfDocuments.Add(pdf);
        SeedUploader(ownerId);
        await _dbContext.SaveChangesAsync();

        var query = new GetKbDocumentByIdQuery(docId, RequestingUserId: Guid.NewGuid(), UserIsAdmin: false);

        await _handler.Invoking(h => h.Handle(query, CancellationToken.None))
            .Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_LastIngestedAtFallback_UsesUploadedAtWhenNoVectorDoc()
    {
        var docId = Guid.NewGuid();
        var uploadedAt = DateTime.UtcNow.AddDays(-1);
        var pdf = SeedPdf(id: docId, processingState: "Ready");
        pdf.UploadedAt = uploadedAt;
        pdf.ProcessedAt = null;
        _dbContext.PdfDocuments.Add(pdf);
        SeedUploader(pdf.UploadedByUserId);
        await _dbContext.SaveChangesAsync();

        var dto = await _handler.Handle(
            new GetKbDocumentByIdQuery(docId, pdf.UploadedByUserId, UserIsAdmin: false),
            CancellationToken.None);

        dto.LastIngestedAt.Should().BeCloseTo(uploadedAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Handle_LanguageEmpty_DefaultsToIt()
    {
        var docId = Guid.NewGuid();
        var pdf = SeedPdf(id: docId, processingState: "Ready", language: string.Empty);
        _dbContext.PdfDocuments.Add(pdf);
        SeedUploader(pdf.UploadedByUserId);
        await _dbContext.SaveChangesAsync();

        var dto = await _handler.Handle(
            new GetKbDocumentByIdQuery(docId, pdf.UploadedByUserId, UserIsAdmin: false),
            CancellationToken.None);

        dto.Language.Should().Be("it");
    }

    private static PdfDocumentEntity SeedPdf(
        Guid id,
        string fileName = "test-doc.pdf",
        string processingState = "Ready",
        string documentCategory = "Rulebook",
        Guid? sharedGameId = null,
        int? pageCount = null,
        bool isPublic = true,
        string language = "it") => new()
        {
            Id = id,
            FileName = fileName,
            ProcessingState = processingState,
            DocumentCategory = documentCategory,
            SharedGameId = sharedGameId,
            PageCount = pageCount,
            UploadedAt = DateTime.UtcNow.AddHours(-1),
            ProcessedAt = processingState == "Ready" ? DateTime.UtcNow : null,
            Language = language,
            UploadedByUserId = Guid.NewGuid(),
            FilePath = "/tmp/test.pdf",
            IsPublic = isPublic
        };

    private void SeedUploader(Guid userId, string? displayName = "Test User")
    {
        if (_dbContext.Users.Any(u => u.Id == userId)) return;
        _dbContext.Users.Add(new UserEntity
        {
            Id = userId,
            Email = $"user-{userId:N}@example.test",
            DisplayName = displayName,
            CreatedAt = DateTime.UtcNow
        });
    }
}
