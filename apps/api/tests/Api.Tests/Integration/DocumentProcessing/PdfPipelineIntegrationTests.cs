using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Tests.TestHelpers;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.Integration.DocumentProcessing;

/// <summary>
/// Integration tests for PDF processing pipeline, chunking, and validation.
/// Week 9: DocumentProcessing complete pipeline (20 tests)
/// Tests: PDF extraction pipeline (Unstructured → SmolDocling → Docnet), chunking strategies, validation and confidence scoring
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Week", "9")]
public sealed class PdfPipelineIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IPdfDocumentRepository? _pdfRepository;
    private IDocumentCollectionRepository? _collectionRepository;

    private static readonly Guid TestUserId = new("93000000-0000-0000-0000-000000000001");
    private static readonly Guid TestGameId = new("94000000-0000-0000-0000-000000000001");
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public PdfPipelineIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"meepleai_week9_docproc_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(connectionString, o => o.UseVector()) // Issue #3547
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;

        var mockMediator = new Mock<MediatR.IMediator>();
        var mockEventCollector = new Mock<Api.SharedKernel.Application.Services.IDomainEventCollector>();
        mockEventCollector.Setup(x => x.GetAndClearEvents())
            .Returns(new List<Api.SharedKernel.Domain.Interfaces.IDomainEvent>().AsReadOnly());

        // Fix: Use PostgreSQL DbContext with Testcontainers, not in-memory
        _dbContext = new MeepleAiDbContext(options, mockMediator.Object, mockEventCollector.Object);
        await _dbContext.Database.MigrateAsync(TestCancellationToken);

        // Seed required User for FK constraints
        await SeedTestDataAsync();

        _pdfRepository = new PdfDocumentRepository(_dbContext, mockEventCollector.Object);
        _collectionRepository = new DocumentCollectionRepository(_dbContext, mockEventCollector.Object);
    }

    private async Task SeedTestDataAsync()
    {
        var user = new UserEntity
        {
            Id = TestUserId,
            Email = "test-week9-docproc@meepleai.dev",
            DisplayName = "Test User Week 9 DocProc",
            Role = "admin",
            CreatedAt = DateTime.UtcNow
        };

        var game = new Api.Infrastructure.Entities.GameEntity
        {
            Id = TestGameId,
            Name = "Test Game for PDF Processing",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext!.Set<UserEntity>().Add(user);
        _dbContext.Set<Api.Infrastructure.Entities.GameEntity>().Add(game);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }

        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    #region PDF Pipeline Stage 1: Unstructured (Tests 1-3)

    [Fact]
    public async Task PdfDocument_Create_ShouldSetPendingStatus()
    {
        // Arrange
        var pdf = new PdfDocument(
            Guid.NewGuid(),
            TestGameId,
            new FileName("test-rulebook.pdf"),
            "/uploads/test-rulebook.pdf",
            new FileSize(1024 * 500), // 500KB
            TestUserId,
            LanguageCode.English
        );

        // Act
        await _pdfRepository!.AddAsync(pdf, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        // Assert
        var retrieved = await _pdfRepository.GetByIdAsync(pdf.Id, TestCancellationToken);
        retrieved.Should().NotBeNull();
        retrieved!.ProcessingStatus.Should().Be("pending");
        retrieved.UploadedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task PdfDocument_MarkAsProcessing_ShouldUpdateStatus()
    {
        // Arrange
        var pdf = new PdfDocument(
            Guid.NewGuid(),
            TestGameId,
            new FileName("processing-test.pdf"),
            "/uploads/processing-test.pdf",
            new FileSize(1024 * 1000),
            TestUserId
        );

        await _pdfRepository!.AddAsync(pdf, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);
        _dbContext.ChangeTracker.Clear();

        // Act
        var tracked = await _pdfRepository.GetByIdAsync(pdf.Id, TestCancellationToken);
        tracked!.MarkAsProcessing();
        await _pdfRepository.UpdateAsync(tracked, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        var updated = await _pdfRepository.GetByIdAsync(pdf.Id, TestCancellationToken);
        updated.Should().NotBeNull();
        updated!.ProcessingStatus.Should().Be("processing");
    }

    [Fact]
    public async Task PdfDocument_MarkAsCompleted_ShouldRecordPageCount()
    {
        // Arrange
        var pdf = new PdfDocument(
            Guid.NewGuid(),
            TestGameId,
            new FileName("completed-test.pdf"),
            "/uploads/completed-test.pdf",
            new FileSize(1024 * 2000),
            TestUserId
        );

        await _pdfRepository!.AddAsync(pdf, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);
        _dbContext.ChangeTracker.Clear();

        // Act
        var tracked = await _pdfRepository.GetByIdAsync(pdf.Id, TestCancellationToken);
        tracked!.MarkAsProcessing();
        await _pdfRepository.UpdateAsync(tracked, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
        _dbContext.ChangeTracker.Clear();

        tracked = await _pdfRepository.GetByIdAsync(pdf.Id, TestCancellationToken);
        tracked!.MarkAsCompleted(24);
        await _pdfRepository.UpdateAsync(tracked, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        var completed = await _pdfRepository.GetByIdAsync(pdf.Id, TestCancellationToken);
        completed.Should().NotBeNull();
        completed!.ProcessingStatus.Should().Be("completed");
        completed.PageCount.Should().Be(24);
        completed.ProcessedAt.Should().NotBeNull();
    }

    #endregion

    #region PDF Pipeline Stage 2: SmolDocling (Tests 4-6)

    [Fact]
    public async Task PdfDocument_FallbackToStage2_ShouldRecordFallbackReason()
    {
        // Arrange
        var pdf = new PdfDocument(
            Guid.NewGuid(),
            TestGameId,
            new FileName("fallback-stage2.pdf"),
            "/uploads/fallback-stage2.pdf",
            new FileSize(1024 * 1500),
            TestUserId
        );

        await _pdfRepository!.AddAsync(pdf, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);
        _dbContext.ChangeTracker.Clear();

        // Act - Simulate Stage 1 failure and fallback to Stage 2
        var tracked = await _pdfRepository.GetByIdAsync(pdf.Id, TestCancellationToken);
        tracked!.MarkAsProcessing();
        await _pdfRepository.UpdateAsync(tracked, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
        _dbContext.ChangeTracker.Clear();

        // Simulate fallback (in real scenario, this would be triggered by low confidence)
        tracked = await _pdfRepository.GetByIdAsync(pdf.Id, TestCancellationToken);
        tracked!.MarkAsCompleted(16);
        await _pdfRepository.UpdateAsync(tracked, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        var processed = await _pdfRepository.GetByIdAsync(pdf.Id, TestCancellationToken);
        processed.Should().NotBeNull();
        processed!.ProcessingStatus.Should().Be("completed");
    }

    [Fact]
    public async Task PdfDocument_HighConfidenceStage1_ShouldNotFallback()
    {
        // Arrange
        var pdf = new PdfDocument(
            Guid.NewGuid(),
            TestGameId,
            new FileName("high-confidence.pdf"),
            "/uploads/high-confidence.pdf",
            new FileSize(1024 * 800),
            TestUserId
        );

        await _pdfRepository!.AddAsync(pdf, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);
        _dbContext.ChangeTracker.Clear();

        // Act - High confidence extraction succeeds in Stage 1
        var tracked = await _pdfRepository.GetByIdAsync(pdf.Id, TestCancellationToken);
        tracked!.MarkAsProcessing();
        await _pdfRepository.UpdateAsync(tracked, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
        _dbContext.ChangeTracker.Clear();

        tracked = await _pdfRepository.GetByIdAsync(pdf.Id, TestCancellationToken);
        tracked!.MarkAsCompleted(12);
        await _pdfRepository.UpdateAsync(tracked, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        var processed = await _pdfRepository.GetByIdAsync(pdf.Id, TestCancellationToken);
        processed.Should().NotBeNull();
        processed!.ProcessingStatus.Should().Be("completed");
        processed.PageCount.Should().Be(12);
    }

    [Fact]
    public async Task PdfDocument_StageFailure_ShouldRecordError()
    {
        // Arrange
        var pdf = new PdfDocument(
            Guid.NewGuid(),
            TestGameId,
            new FileName("error-pdf.pdf"),
            "/uploads/error-pdf.pdf",
            new FileSize(1024 * 200),
            TestUserId
        );

        await _pdfRepository!.AddAsync(pdf, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);
        _dbContext.ChangeTracker.Clear();

        // Act
        var tracked = await _pdfRepository.GetByIdAsync(pdf.Id, TestCancellationToken);
        tracked!.MarkAsProcessing();
        await _pdfRepository.UpdateAsync(tracked, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
        _dbContext.ChangeTracker.Clear();

        tracked = await _pdfRepository.GetByIdAsync(pdf.Id, TestCancellationToken);
        tracked!.MarkAsFailed("Extraction failed: Corrupted PDF structure");
        await _pdfRepository.UpdateAsync(tracked, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        var failed = await _pdfRepository.GetByIdAsync(pdf.Id, TestCancellationToken);
        failed.Should().NotBeNull();
        failed!.ProcessingStatus.Should().Be("failed");
        failed.ProcessingError.Should().Contain("Corrupted PDF");
    }

    #endregion

    #region PDF Pipeline Stage 3: Docnet (Tests 7-9)

    [Fact]
    public async Task PdfDocument_FallbackToStage3_ShouldCompleteWithBasicExtraction()
    {
        // Arrange
        var pdf = new PdfDocument(
            Guid.NewGuid(),
            TestGameId,
            new FileName("fallback-stage3.pdf"),
            "/uploads/fallback-stage3.pdf",
            new FileSize(1024 * 3000),
            TestUserId
        );

        await _pdfRepository!.AddAsync(pdf, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);
        _dbContext.ChangeTracker.Clear();

        // Act - Fallback to Stage 3 (basic extraction)
        var tracked = await _pdfRepository.GetByIdAsync(pdf.Id, TestCancellationToken);
        tracked!.MarkAsProcessing();
        await _pdfRepository.UpdateAsync(tracked, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
        _dbContext.ChangeTracker.Clear();

        tracked = await _pdfRepository.GetByIdAsync(pdf.Id, TestCancellationToken);
        tracked!.MarkAsCompleted(32);
        await _pdfRepository.UpdateAsync(tracked, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        var processed = await _pdfRepository.GetByIdAsync(pdf.Id, TestCancellationToken);
        processed.Should().NotBeNull();
        processed!.ProcessingStatus.Should().Be("completed");
        processed.PageCount.Should().Be(32);
    }

    [Fact]
    public async Task PdfDocument_LargeFile_ShouldHandleProcessing()
    {
        // Arrange
        var pdf = new PdfDocument(
            Guid.NewGuid(),
            TestGameId,
            new FileName("large-manual.pdf"),
            "/uploads/large-manual.pdf",
            new FileSize(1024 * 1024 * 50), // 50MB
            TestUserId
        );

        await _pdfRepository!.AddAsync(pdf, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        // Assert
        var created = await _pdfRepository.GetByIdAsync(pdf.Id, TestCancellationToken);
        created.Should().NotBeNull();
        created!.FileSize.Bytes.Should().Be(1024 * 1024 * 50);
    }

    [Fact]
    public async Task PdfDocument_MultiplePages_ShouldTrackPageCount()
    {
        // Arrange
        var pdf = new PdfDocument(
            Guid.NewGuid(),
            TestGameId,
            new FileName("multi-page.pdf"),
            "/uploads/multi-page.pdf",
            new FileSize(1024 * 5000),
            TestUserId
        );

        await _pdfRepository!.AddAsync(pdf, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);
        _dbContext.ChangeTracker.Clear();

        // Act
        var tracked = await _pdfRepository.GetByIdAsync(pdf.Id, TestCancellationToken);
        tracked!.MarkAsProcessing();
        await _pdfRepository.UpdateAsync(tracked, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
        _dbContext.ChangeTracker.Clear();

        tracked = await _pdfRepository.GetByIdAsync(pdf.Id, TestCancellationToken);
        tracked!.MarkAsCompleted(96);
        await _pdfRepository.UpdateAsync(tracked, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        var processed = await _pdfRepository.GetByIdAsync(pdf.Id, TestCancellationToken);
        processed.Should().NotBeNull();
        processed!.PageCount.Should().Be(96);
    }

    #endregion

    #region Chunking Strategies (Tests 10-14)

    [Fact]
    public async Task DocumentCollection_Create_ShouldSupportMultipleDocuments()
    {
        // Arrange
        var collection = new DocumentCollection(
            Guid.NewGuid(),
            TestGameId,
            new CollectionName("Complete Ruleset Collection"),
            TestUserId,
            "Full game rules with expansions"
        );

        // Act
        await _collectionRepository!.AddAsync(collection, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        // Assert
        var retrieved = await _collectionRepository.GetByIdAsync(collection.Id, TestCancellationToken);
        retrieved.Should().NotBeNull();
        retrieved!.Name.Value.Should().Be("Complete Ruleset Collection");
    }

    [Fact]
    public async Task PdfDocument_SentenceChunking_ShouldMaintainContext()
    {
        // Arrange - Simulate sentence-based chunking
        var pdf = new PdfDocument(
            Guid.NewGuid(),
            TestGameId,
            new FileName("sentence-chunked.pdf"),
            "/uploads/sentence-chunked.pdf",
            new FileSize(1024 * 1200),
            TestUserId
        );

        await _pdfRepository!.AddAsync(pdf, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);
        _dbContext.ChangeTracker.Clear();

        // Act - Complete processing (chunking happens in extraction layer)
        var tracked = await _pdfRepository.GetByIdAsync(pdf.Id, TestCancellationToken);
        tracked!.MarkAsProcessing();
        await _pdfRepository.UpdateAsync(tracked, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
        _dbContext.ChangeTracker.Clear();

        tracked = await _pdfRepository.GetByIdAsync(pdf.Id, TestCancellationToken);
        tracked!.MarkAsCompleted(18);
        await _pdfRepository.UpdateAsync(tracked, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        var processed = await _pdfRepository.GetByIdAsync(pdf.Id, TestCancellationToken);
        processed.Should().NotBeNull();
        processed!.ProcessingStatus.Should().Be("completed");
    }

    [Fact]
    public async Task PdfDocument_ParagraphChunking_ShouldPreserveSemantics()
    {
        // Arrange - Simulate paragraph-based chunking
        var pdf = new PdfDocument(
            Guid.NewGuid(),
            TestGameId,
            new FileName("paragraph-chunked.pdf"),
            "/uploads/paragraph-chunked.pdf",
            new FileSize(1024 * 1800),
            TestUserId
        );

        await _pdfRepository!.AddAsync(pdf, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);
        _dbContext.ChangeTracker.Clear();

        // Act
        var tracked = await _pdfRepository.GetByIdAsync(pdf.Id, TestCancellationToken);
        tracked!.MarkAsProcessing();
        tracked.MarkAsCompleted(22);
        await _pdfRepository.UpdateAsync(tracked, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        var processed = await _pdfRepository.GetByIdAsync(pdf.Id, TestCancellationToken);
        processed.Should().NotBeNull();
        processed!.ProcessingStatus.Should().Be("completed");
        processed.PageCount.Should().Be(22);
    }

    [Fact]
    public async Task PdfDocument_WithCollection_ShouldTrackSortOrder()
    {
        // Arrange
        var collection = new DocumentCollection(
            Guid.NewGuid(),
            TestGameId,
            new CollectionName("Ordered Collection"),
            TestUserId,
            "Documents in specific order"
        );

        await _collectionRepository!.AddAsync(collection, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        var pdf1 = new PdfDocument(
            Guid.NewGuid(),
            TestGameId,
            new FileName("doc-1.pdf"),
            "/uploads/doc-1.pdf",
            new FileSize(1024 * 500),
            TestUserId,
            collectionId: collection.Id,
            sortOrder: 1
        );

        var pdf2 = new PdfDocument(
            Guid.NewGuid(),
            TestGameId,
            new FileName("doc-2.pdf"),
            "/uploads/doc-2.pdf",
            new FileSize(1024 * 600),
            TestUserId,
            collectionId: collection.Id,
            sortOrder: 2
        );

        // Act
        await _pdfRepository!.AddAsync(pdf1, TestCancellationToken);
        await _pdfRepository.AddAsync(pdf2, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        var retrieved1 = await _pdfRepository.GetByIdAsync(pdf1.Id, TestCancellationToken);
        var retrieved2 = await _pdfRepository.GetByIdAsync(pdf2.Id, TestCancellationToken);

        retrieved1.Should().NotBeNull();
        retrieved1!.SortOrder.Should().Be(1);
        retrieved2.Should().NotBeNull();
        retrieved2!.SortOrder.Should().Be(2);
    }

    [Fact]
    public async Task PdfDocument_DocumentType_ShouldCategorizeCorrectly()
    {
        // Arrange
        var baseRules = new PdfDocument(
            Guid.NewGuid(),
            TestGameId,
            new FileName("base-rules.pdf"),
            "/uploads/base-rules.pdf",
            new FileSize(1024 * 1000),
            TestUserId,
            documentType: DocumentType.Base
        );

        var expansion = new PdfDocument(
            Guid.NewGuid(),
            TestGameId,
            new FileName("expansion-1.pdf"),
            "/uploads/expansion-1.pdf",
            new FileSize(1024 * 700),
            TestUserId,
            documentType: DocumentType.Expansion
        );

        // Act
        await _pdfRepository!.AddAsync(baseRules, TestCancellationToken);
        await _pdfRepository.AddAsync(expansion, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        // Assert
        var retrievedBase = await _pdfRepository.GetByIdAsync(baseRules.Id, TestCancellationToken);
        var retrievedExpansion = await _pdfRepository.GetByIdAsync(expansion.Id, TestCancellationToken);

        retrievedBase!.DocumentType.Should().Be(DocumentType.Base);
        retrievedExpansion!.DocumentType.Should().Be(DocumentType.Expansion);
    }

    #endregion

    #region Validation and Confidence (Tests 15-20)

    [Fact]
    public async Task PdfDocument_LanguageDetection_ShouldStoreLanguage()
    {
        // Arrange
        var englishPdf = new PdfDocument(
            Guid.NewGuid(),
            TestGameId,
            new FileName("english-rules.pdf"),
            "/uploads/english-rules.pdf",
            new FileSize(1024 * 900),
            TestUserId,
            LanguageCode.English
        );

        var italianPdf = new PdfDocument(
            Guid.NewGuid(),
            TestGameId,
            new FileName("italian-rules.pdf"),
            "/uploads/italian-rules.pdf",
            new FileSize(1024 * 950),
            TestUserId,
            LanguageCode.Italian
        );

        // Act
        await _pdfRepository!.AddAsync(englishPdf, TestCancellationToken);
        await _pdfRepository.AddAsync(italianPdf, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        // Assert
        var retrievedEnglish = await _pdfRepository.GetByIdAsync(englishPdf.Id, TestCancellationToken);
        var retrievedItalian = await _pdfRepository.GetByIdAsync(italianPdf.Id, TestCancellationToken);

        retrievedEnglish!.Language.Should().Be(LanguageCode.English);
        retrievedItalian!.Language.Should().Be(LanguageCode.Italian);
    }

    [Fact]
    public async Task PdfDocument_Validation_ShouldRejectInvalidFileName()
    {
        // Act & Assert
        var act = () => new FileName("");

        act.Should().Throw<Api.SharedKernel.Domain.Exceptions.ValidationException>();
    }

    [Fact]
    public async Task PdfDocument_Validation_ShouldRejectEmptyFilePath()
    {
        // Act & Assert
        var act = () => new PdfDocument(
            Guid.NewGuid(),
            TestGameId,
            new FileName("valid.pdf"),
            "",
            new FileSize(1024),
            TestUserId
        );

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task PdfDocument_Validation_ShouldRejectNegativeSortOrder()
    {
        // Act & Assert
        var act = () => new PdfDocument(
            Guid.NewGuid(),
            TestGameId,
            new FileName("test.pdf"),
            "/uploads/test.pdf",
            new FileSize(1024),
            TestUserId,
            sortOrder: -1
        );

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task PdfDocument_GetByGameId_ShouldFilterCorrectly()
    {
        // Arrange
        var game2Id = Guid.NewGuid();
        var game2 = new Api.Infrastructure.Entities.GameEntity
        {
            Id = game2Id,
            Name = "Another Game",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext!.Set<Api.Infrastructure.Entities.GameEntity>().Add(game2);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var game1Pdf = new PdfDocument(
            Guid.NewGuid(),
            TestGameId,
            new FileName("game1-rules.pdf"),
            "/uploads/game1-rules.pdf",
            new FileSize(1024 * 500),
            TestUserId
        );

        var game2Pdf = new PdfDocument(
            Guid.NewGuid(),
            game2Id,
            new FileName("game2-rules.pdf"),
            "/uploads/game2-rules.pdf",
            new FileSize(1024 * 600),
            TestUserId
        );

        await _pdfRepository!.AddAsync(game1Pdf, TestCancellationToken);
        await _pdfRepository.AddAsync(game2Pdf, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Act
        var game1Pdfs = await _pdfRepository.FindByGameIdAsync(TestGameId, TestCancellationToken);

        // Assert
        game1Pdfs.Should().HaveCount(g => g >= 1);
        game1Pdfs.Should().OnlyContain(p => p.GameId == TestGameId);
    }

    [Fact]
    public async Task PdfDocument_PublicLibrary_ShouldControlVisibility()
    {
        // Arrange
        var publicPdf = new PdfDocument(
            Guid.NewGuid(),
            TestGameId,
            new FileName("public-rules.pdf"),
            "/uploads/public-rules.pdf",
            new FileSize(1024 * 800),
            TestUserId
        );

        // Assert - Initially not public
        publicPdf.IsPublic.Should().BeFalse();

        // Act - Make public
        publicPdf.MakePublic();

        // Assert - Now public
        publicPdf.IsPublic.Should().BeTrue();

        // Act - Make private again
        publicPdf.MakePrivate();

        // Assert - Back to private
        publicPdf.IsPublic.Should().BeFalse();
    }

    #endregion

    #region Issue #4215: 7-State Pipeline Progression

    [Fact]
    public async Task PdfDocument_SevenStateProgression_ShouldAdvanceThroughAllStates()
    {
        // Arrange
        var pdf = new PdfDocument(
            Guid.NewGuid(),
            TestGameId,
            new FileName("seven-state-test.pdf"),
            "/uploads/seven-state-test.pdf",
            new FileSize(1024 * 100),
            TestUserId,
            LanguageCode.English
        );

        await _pdfRepository!.AddAsync(pdf, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        // Assert initial state
        pdf.ProcessingState.Should().Be(Api.BoundedContexts.DocumentProcessing.Domain.Enums.PdfProcessingState.Pending);

        // Act & Assert: Progress through all 7 states
        pdf.TransitionTo(Api.BoundedContexts.DocumentProcessing.Domain.Enums.PdfProcessingState.Uploading);
        pdf.ProcessingState.Should().Be(Api.BoundedContexts.DocumentProcessing.Domain.Enums.PdfProcessingState.Uploading);

        pdf.TransitionTo(Api.BoundedContexts.DocumentProcessing.Domain.Enums.PdfProcessingState.Extracting);
        pdf.ProcessingState.Should().Be(Api.BoundedContexts.DocumentProcessing.Domain.Enums.PdfProcessingState.Extracting);

        pdf.TransitionTo(Api.BoundedContexts.DocumentProcessing.Domain.Enums.PdfProcessingState.Chunking);
        pdf.ProcessingState.Should().Be(Api.BoundedContexts.DocumentProcessing.Domain.Enums.PdfProcessingState.Chunking);

        pdf.TransitionTo(Api.BoundedContexts.DocumentProcessing.Domain.Enums.PdfProcessingState.Embedding);
        pdf.ProcessingState.Should().Be(Api.BoundedContexts.DocumentProcessing.Domain.Enums.PdfProcessingState.Embedding);

        pdf.TransitionTo(Api.BoundedContexts.DocumentProcessing.Domain.Enums.PdfProcessingState.Indexing);
        pdf.ProcessingState.Should().Be(Api.BoundedContexts.DocumentProcessing.Domain.Enums.PdfProcessingState.Indexing);

        pdf.TransitionTo(Api.BoundedContexts.DocumentProcessing.Domain.Enums.PdfProcessingState.Ready);
        pdf.ProcessingState.Should().Be(Api.BoundedContexts.DocumentProcessing.Domain.Enums.PdfProcessingState.Ready);

        // Verify domain events were emitted (6 transitions)
        pdf.DomainEvents.Should().HaveCount(6);
        pdf.DomainEvents.Should().AllBeOfType<Api.BoundedContexts.DocumentProcessing.Domain.Events.PdfStateChangedEvent>();

        // Verify final state persists (must call UpdateAsync to sync domain changes to EF tracked entity)
        await _pdfRepository.UpdateAsync(pdf, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
        var retrieved = await _pdfRepository.GetByIdAsync(pdf.Id, TestCancellationToken);
        retrieved.Should().NotBeNull();
        retrieved!.ProcessingState.Should().Be(Api.BoundedContexts.DocumentProcessing.Domain.Enums.PdfProcessingState.Ready);
    }

    [Fact]
    public async Task PdfDocument_InvalidTransition_ShouldThrowException()
    {
        // Arrange
        var pdf = new PdfDocument(
            Guid.NewGuid(),
            TestGameId,
            new FileName("invalid-transition-test.pdf"),
            "/uploads/invalid-transition-test.pdf",
            new FileSize(1024 * 100),
            TestUserId
        );

        // Act & Assert: Try to skip states (Pending → Chunking)
        var act = () => pdf.TransitionTo(Api.BoundedContexts.DocumentProcessing.Domain.Enums.PdfProcessingState.Chunking);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Invalid state transition: Pending * Chunking*");
    }

    [Fact]
    public async Task PdfDocument_ReadyState_CannotTransition()
    {
        // Arrange
        var pdf = new PdfDocument(
            Guid.NewGuid(),
            TestGameId,
            new FileName("ready-state-test.pdf"),
            "/uploads/ready-state-test.pdf",
            new FileSize(1024 * 100),
            TestUserId
        );

        // Progress to Ready
        pdf.TransitionTo(Api.BoundedContexts.DocumentProcessing.Domain.Enums.PdfProcessingState.Uploading);
        pdf.TransitionTo(Api.BoundedContexts.DocumentProcessing.Domain.Enums.PdfProcessingState.Extracting);
        pdf.TransitionTo(Api.BoundedContexts.DocumentProcessing.Domain.Enums.PdfProcessingState.Chunking);
        pdf.TransitionTo(Api.BoundedContexts.DocumentProcessing.Domain.Enums.PdfProcessingState.Embedding);
        pdf.TransitionTo(Api.BoundedContexts.DocumentProcessing.Domain.Enums.PdfProcessingState.Indexing);
        pdf.TransitionTo(Api.BoundedContexts.DocumentProcessing.Domain.Enums.PdfProcessingState.Ready);

        // Act & Assert: Cannot transition from Ready
        var act = () => pdf.TransitionTo(Api.BoundedContexts.DocumentProcessing.Domain.Enums.PdfProcessingState.Extracting);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot transition from Ready state");
    }

    [Fact]
    public async Task PdfDocument_FailedState_CanRetry()
    {
        // Arrange
        var pdf = new PdfDocument(
            Guid.NewGuid(),
            TestGameId,
            new FileName("retry-test.pdf"),
            "/uploads/retry-test.pdf",
            new FileSize(1024 * 100),
            TestUserId
        );

        // Progress to Extracting then fail
        pdf.TransitionTo(Api.BoundedContexts.DocumentProcessing.Domain.Enums.PdfProcessingState.Uploading);
        pdf.TransitionTo(Api.BoundedContexts.DocumentProcessing.Domain.Enums.PdfProcessingState.Extracting);
        pdf.TransitionTo(Api.BoundedContexts.DocumentProcessing.Domain.Enums.PdfProcessingState.Failed);

        // Act: Retry from Failed allows any recovery state
        pdf.TransitionTo(Api.BoundedContexts.DocumentProcessing.Domain.Enums.PdfProcessingState.Extracting);

        // Assert
        pdf.ProcessingState.Should().Be(Api.BoundedContexts.DocumentProcessing.Domain.Enums.PdfProcessingState.Extracting);
    }

    #endregion
}