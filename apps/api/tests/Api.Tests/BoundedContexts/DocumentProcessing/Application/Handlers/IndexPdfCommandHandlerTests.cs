using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Application.Services.Chunking;
using Api.BoundedContexts.KnowledgeBase.Domain.Chunking;
using Api.Configuration;
using Api.Constants;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Services;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Handlers;

/// <summary>
/// Tests for IndexPdfCommandHandler.
/// ISSUE-1818: Migrated to FluentAssertions for improved readability.
/// Tests PDF text indexing workflow (chunking, embedding, pgvector indexing).
/// NOTE: Complex orchestrator with many dependencies - focused on construction and validation.
/// RESOLVED: Issue #1690 - Integration tests added in IndexPdfIntegrationTests.cs.
/// ISSUE-1500: TEST-002 - Fixed test isolation (fresh context per test)
/// ISSUE-1818: Migrated to FluentAssertions for improved readability.
/// Slice D (Issue #730): chunking source migrated from ITextChunkingService.ChunkText to
/// IAdvancedChunkingService.ChunkDocumentAsync (via HeadingAwareChunker), so chunk-count-driven
/// tests mock IAdvancedChunkingService returning flat parent-level HierarchicalChunks.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class IndexPdfCommandHandlerTests
{
    /// <summary>
    /// Creates a fresh DbContext for each test to ensure complete isolation
    /// </summary>
    private static MeepleAiDbContext CreateFreshDbContext()
    {
        return TestDbContextFactory.CreateInMemoryDbContext();
    }

    /// <summary>
    /// Creates a fresh set of mocks for each test
    /// </summary>
    private static (Mock<IAdvancedChunkingService>, Mock<IEmbeddingService>, Mock<ILogger<IndexPdfCommandHandler>>, Mock<IOptions<IndexingSettings>>) CreateMocks()
    {
        var advancedChunkingServiceMock = new Mock<IAdvancedChunkingService>();
        var embeddingServiceMock = new Mock<IEmbeddingService>();
        var loggerMock = new Mock<ILogger<IndexPdfCommandHandler>>();
        var indexingSettingsMock = new Mock<IOptions<IndexingSettings>>();

        // Configure default batch size
        var settings = new IndexingSettings { EmbeddingBatchSize = 100 };
        indexingSettingsMock.Setup(x => x.Value).Returns(settings);

        return (advancedChunkingServiceMock, embeddingServiceMock, loggerMock, indexingSettingsMock);
    }
    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Arrange - fresh resources per test
        using var context = CreateFreshDbContext();
        var (advancedChunkingServiceMock, embeddingServiceMock, loggerMock, indexingSettingsMock) = CreateMocks();

        // Act
        var handler = new IndexPdfCommandHandler(
            context,
            advancedChunkingServiceMock.Object,
            embeddingServiceMock.Object,
            loggerMock.Object,
            indexingSettingsMock.Object,
            Mock.Of<ISemanticResponseCache>(),
            Mock.Of<IPdfIndexingPipeline>());

        // Assert
        handler.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithTimeProvider_CreatesInstance()
    {
        // Arrange - fresh resources per test
        using var context = CreateFreshDbContext();
        var (advancedChunkingServiceMock, embeddingServiceMock, loggerMock, indexingSettingsMock) = CreateMocks();
        var timeProvider = TimeProvider.System;

        // Act
        var handler = new IndexPdfCommandHandler(
            context,
            advancedChunkingServiceMock.Object,
            embeddingServiceMock.Object,
            loggerMock.Object,
            indexingSettingsMock.Object,
            Mock.Of<ISemanticResponseCache>(),
            Mock.Of<IPdfIndexingPipeline>(),
            timeProvider);

        // Assert
        handler.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullTimeProvider_UsesSystemTimeProvider()
    {
        // Arrange - fresh resources per test
        using var context = CreateFreshDbContext();
        var (advancedChunkingServiceMock, embeddingServiceMock, loggerMock, indexingSettingsMock) = CreateMocks();

        // Act
        var handler = new IndexPdfCommandHandler(
            context,
            advancedChunkingServiceMock.Object,
            embeddingServiceMock.Object,
            loggerMock.Object,
            indexingSettingsMock.Object,
            Mock.Of<ISemanticResponseCache>(),
            Mock.Of<IPdfIndexingPipeline>(),
            null);

        // Assert
        handler.Should().NotBeNull();
    }
    [Fact]
    public void IndexPdfCommand_ConstructsCorrectly()
    {
        // Arrange
        var pdfId = Guid.NewGuid().ToString();

        // Act
        var command = new IndexPdfCommand(pdfId);

        // Assert
        command.PdfId.Should().Be(pdfId);
    }
    [Fact]
    public void IndexingResultDto_CreateSuccess_ConstructsCorrectly()
    {
        // Arrange
        var chunkCount = 42;
        var vectorCount = 42;

        // Act
        var result = IndexingResultDto.CreateSuccess("vector-doc-id", chunkCount, DateTime.UtcNow);

        // Assert
        result.Success.Should().BeTrue();
        result.ChunkCount.Should().Be(chunkCount);
        result.ErrorMessage.Should().BeNull();
        result.ErrorCode.Should().BeNull();
    }

    [Fact]
    public void IndexingResultDto_CreateFailure_ConstructsCorrectly()
    {
        // Arrange
        var errorMessage = "PDF not found";
        var errorCode = PdfIndexingErrorCode.PdfNotFound;

        // Act
        var result = IndexingResultDto.CreateFailure(errorMessage, errorCode);

        // Assert
        result.Success.Should().BeFalse();
        result.ChunkCount.Should().Be(0);
        result.ErrorMessage.Should().Be(errorMessage);
        result.ErrorCode.Should().Be(errorCode);
    }

    [Fact]
    public void IndexingResultDto_CreateFailure_WithTextExtractionRequired()
    {
        // Act
        var result = IndexingResultDto.CreateFailure(
            "Text extraction required",
            PdfIndexingErrorCode.TextExtractionRequired);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(PdfIndexingErrorCode.TextExtractionRequired);
    }

    [Fact]
    public void IndexingResultDto_CreateFailure_WithChunkingFailure()
    {
        // Act
        var result = IndexingResultDto.CreateFailure(
            "Chunking failed",
            PdfIndexingErrorCode.ChunkingFailed);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(PdfIndexingErrorCode.ChunkingFailed);
    }

    [Fact]
    public void IndexingResultDto_CreateFailure_WithEmbeddingFailure()
    {
        // Act
        var result = IndexingResultDto.CreateFailure(
            "Embedding generation failed",
            PdfIndexingErrorCode.EmbeddingFailed);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(PdfIndexingErrorCode.EmbeddingFailed);
    }

    [Fact]
    public void IndexingResultDto_CreateFailure_WithVectorIndexingFailure()
    {
        // Act
        var result = IndexingResultDto.CreateFailure(
            "Vector indexing failed",
            PdfIndexingErrorCode.VectorIndexingFailed);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(PdfIndexingErrorCode.VectorIndexingFailed);
    }
    [Theory]
    [InlineData(PdfIndexingErrorCode.PdfNotFound)]
    [InlineData(PdfIndexingErrorCode.TextExtractionRequired)]
    [InlineData(PdfIndexingErrorCode.ChunkingFailed)]
    [InlineData(PdfIndexingErrorCode.EmbeddingFailed)]
    [InlineData(PdfIndexingErrorCode.VectorIndexingFailed)]
    [InlineData(PdfIndexingErrorCode.UnexpectedError)]
    public void PdfIndexingErrorCode_AllValuesAreValid(PdfIndexingErrorCode errorCode)
    {
        // Assert
        Enum.IsDefined(typeof(PdfIndexingErrorCode), errorCode).Should().BeTrue();
    }

    // role_tags index-population (review #1555): handler-driven test that the classified role
    // reaches BOTH persisted sinks — pgvector_embeddings.role_tags AND text_chunks.role_tags — in
    // sync, per chunk. Without a classifier this is invisible (roles default to None), so this is
    // the only test that actually observes the fix.
    [Fact]
    public async Task Handle_WithRoleClassifier_PopulatesRoleTagsOnBothSinksInSync()
    {
        // Arrange
        using var context = CreateFreshDbContext();
        var (advancedChunkingServiceMock, embeddingServiceMock, loggerMock, indexingSettingsMock) = CreateMocks();

        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();
        var pdf = CreatePdfDocument(pdfId, gameId, "completed", GenerateExtractedText(120));
        await context.PdfDocuments.AddAsync(pdf);
        await context.SaveChangesAsync();

        var hierarchicalChunks = GenerateFlatHierarchicalChunks(4);
        advancedChunkingServiceMock
            .Setup(x => x.ChunkDocumentAsync(It.IsAny<ExtractedDocument>(), It.IsAny<ChunkingConfiguration?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(hierarchicalChunks);
        embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<string> texts, CancellationToken ct) =>
                new EmbeddingResult { Success = true, Embeddings = texts.Select(_ => GenerateRandomEmbedding(3072)).ToList() });
        embeddingServiceMock.Setup(x => x.GetEmbeddingDimensions()).Returns(3072);
        embeddingServiceMock.Setup(x => x.GetModelName()).Returns("text-embedding-3-large");

        // Distinct role per chunk proves per-chunk alignment (not a uniform value).
        var expectedRoles = new[] { GameBookRole.Setup, GameBookRole.RulesReference, GameBookRole.Lore, GameBookRole.Setup };
        var classifierMock = new Mock<IRoleClassifierService>();
        classifierMock
            .Setup(x => x.ClassifyAsync(It.IsAny<IReadOnlyList<ChunkInput>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedRoles);

        var handler = new IndexPdfCommandHandler(
            context,
            advancedChunkingServiceMock.Object,
            embeddingServiceMock.Object,
            loggerMock.Object,
            indexingSettingsMock.Object,
            Mock.Of<ISemanticResponseCache>(),
            Mock.Of<IPdfIndexingPipeline>(),
            timeProvider: null,
            roleClassifier: classifierMock.Object);

        // Act
        var result = await handler.Handle(new IndexPdfCommand(pdfId.ToString()), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();

        var vectorRoleTags = await context.PgVectorEmbeddings
            .OrderBy(e => e.ChunkIndex)
            .Select(e => e.RoleTags)
            .ToListAsync();
        var textChunkRoleTags = await context.TextChunks
            .Where(tc => tc.PdfDocumentId == pdfId)
            .OrderBy(tc => tc.ChunkIndex)
            .Select(tc => tc.RoleTags)
            .ToListAsync();

        var expectedInts = expectedRoles.Select(r => (int)r).ToList();
        // pgvector_embeddings.role_tags is now populated (was always 0) and matches per chunk...
        vectorRoleTags.Should().Equal(expectedInts);
        // ...and text_chunks.role_tags is the SAME per chunk (single classification, two sinks).
        textChunkRoleTags.Should().Equal(expectedRoles);
    }

    // Slice D (Issue #730), task 5: handler-driven test that heading-aware hierarchy (parent +
    // children built by IAdvancedChunkingService via HeadingAwareChunker) survives the re-index
    // path into BOTH persisted sinks. Also covers the Task-1 binding that a supplied
    // DocumentChunk.Id (here, the parent's HierarchicalChunk.Id parsed from "N" format) is
    // honored at the text_chunks save site rather than being overwritten with a fresh Guid.
    [Fact]
    [Trait("Category", TestCategories.Unit)]
    [Trait("BoundedContext", "DocumentProcessing")]
    public async Task Handle_WithStructuredElements_PersistsHeadingAwareHierarchy()
    {
        // Arrange
        using var context = CreateFreshDbContext();
        var (advancedChunkingServiceMock, embeddingServiceMock, loggerMock, indexingSettingsMock) = CreateMocks();

        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();
        var structuredElements = new List<ExtractedElement>
        {
            new("Setup", 1, "Title"),
            new("Place the board in the middle of the table.", 1, "NarrativeText")
        };
        var pdf = CreatePdfDocument(pdfId, gameId, "completed", "Setup\n\nPlace the board in the middle of the table.");
        pdf.StructuredElementsJson = System.Text.Json.JsonSerializer.Serialize(structuredElements);
        await context.PdfDocuments.AddAsync(pdf);
        await context.SaveChangesAsync();

        // One parent (section-level, Heading "Setup", Level 0) + two children (Level 2) linked
        // via the parent's auto-generated "N"-format Id — mirrors what AdvancedChunkingService
        // actually produces (HierarchicalChunk.Create always self-assigns its own Id).
        var parentMetadata = new ChunkMetadata { Heading = "Setup", Page = 1, ElementType = "Title" };
        var parent = HierarchicalChunk.CreateParent("Setup section", parentMetadata);

        var childMetadata = new ChunkMetadata { Heading = "Setup", Page = 1, ElementType = "NarrativeText" };
        var child1 = HierarchicalChunk.CreateChild("Place the board in the middle of the table.", 2, childMetadata, parent.Id);
        var child2 = HierarchicalChunk.CreateChild("Each player takes a set of pieces.", 2, childMetadata, parent.Id);

        var hierarchicalChunks = new List<HierarchicalChunk> { parent, child1, child2 };
        advancedChunkingServiceMock
            .Setup(x => x.ChunkDocumentAsync(It.IsAny<ExtractedDocument>(), It.IsAny<ChunkingConfiguration?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(hierarchicalChunks);

        embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<string> texts, CancellationToken ct) =>
                new EmbeddingResult { Success = true, Embeddings = texts.Select(_ => GenerateRandomEmbedding(3072)).ToList() });
        embeddingServiceMock.Setup(x => x.GetEmbeddingDimensions()).Returns(3072);
        embeddingServiceMock.Setup(x => x.GetModelName()).Returns("text-embedding-3-large");

        var handler = new IndexPdfCommandHandler(
            context,
            advancedChunkingServiceMock.Object,
            embeddingServiceMock.Object,
            loggerMock.Object,
            indexingSettingsMock.Object,
            Mock.Of<ISemanticResponseCache>(),
            Mock.Of<IPdfIndexingPipeline>());

        // Act
        var result = await handler.Handle(new IndexPdfCommand(pdfId.ToString()), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();

        var savedChunks = await context.TextChunks
            .Where(tc => tc.PdfDocumentId == pdfId)
            .OrderBy(tc => tc.ChunkIndex)
            .ToListAsync();
        savedChunks.Should().HaveCount(3);

        var parentEntity = savedChunks[0];
        parentEntity.Heading.Should().Be("Setup");
        parentEntity.Level.Should().Be((short)0);
        parentEntity.ParentChunkId.Should().BeNull();
        // [BINDING] the supplied HierarchicalChunk.Id must be honored at the text_chunks save
        // site (TextChunkEntity.Id), not silently overwritten with a fresh Guid.
        parentEntity.Id.Should().Be(Guid.ParseExact(parent.Id, "N"));

        var childEntities = savedChunks.Skip(1).ToList();
        childEntities.Should().AllSatisfy(child =>
        {
            child.Level.Should().Be((short)2);
            child.ParentChunkId.Should().Be(parentEntity.Id);
        });

        // Both parent and child levels are embedded and persisted to pgvector.
        var vectorCount = await context.PgVectorEmbeddings.CountAsync();
        vectorCount.Should().Be(savedChunks.Count);
    }

    // Slice D robustness guard: HeadingAwareChunker can emit a Level-0 parent chunk whose Text is
    // a full document section (no ~512-char cap like the old flat chunker). EmbeddingService does
    // not truncate, so an oversized chunk risks failing the whole re-index if the embedding
    // provider rejects long input. The handler must cap ONLY the text sent to the embedding
    // service, while persisting the FULL text to text_chunks/pgvector for retrieval.
    [Fact]
    [Trait("Category", TestCategories.Unit)]
    [Trait("BoundedContext", "DocumentProcessing")]
    public async Task Handle_WithOversizedChunk_CapsEmbeddingInputButPersistsFullText()
    {
        // Arrange
        using var context = CreateFreshDbContext();
        var (advancedChunkingServiceMock, embeddingServiceMock, loggerMock, indexingSettingsMock) = CreateMocks();

        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();
        var oversizedText = new string('a', 2500);
        var pdf = CreatePdfDocument(pdfId, gameId, "completed", oversizedText);
        await context.PdfDocuments.AddAsync(pdf);
        await context.SaveChangesAsync();

        var parentMetadata = new ChunkMetadata { Heading = "Setup", Page = 1, ElementType = "NarrativeText" };
        var oversizedParent = HierarchicalChunk.CreateParent(oversizedText, parentMetadata);

        var hierarchicalChunks = new List<HierarchicalChunk> { oversizedParent };
        advancedChunkingServiceMock
            .Setup(x => x.ChunkDocumentAsync(It.IsAny<ExtractedDocument>(), It.IsAny<ChunkingConfiguration?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(hierarchicalChunks);

        List<string>? capturedTexts = null;
        embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<string> texts, CancellationToken ct) =>
            {
                capturedTexts = texts;
                return new EmbeddingResult { Success = true, Embeddings = texts.Select(_ => GenerateRandomEmbedding(3072)).ToList() };
            });
        embeddingServiceMock.Setup(x => x.GetEmbeddingDimensions()).Returns(3072);
        embeddingServiceMock.Setup(x => x.GetModelName()).Returns("text-embedding-3-large");

        var handler = new IndexPdfCommandHandler(
            context,
            advancedChunkingServiceMock.Object,
            embeddingServiceMock.Object,
            loggerMock.Object,
            indexingSettingsMock.Object,
            Mock.Of<ISemanticResponseCache>(),
            Mock.Of<IPdfIndexingPipeline>());

        // Act
        var result = await handler.Handle(new IndexPdfCommand(pdfId.ToString()), CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();

        // 1) The embedding service received the CAPPED text, not the full 2500-char text.
        capturedTexts.Should().NotBeNull();
        capturedTexts.Should().ContainSingle();
        capturedTexts![0].Length.Should().Be(ChunkingConstants.MaxEmbeddingChars);

        // 2) The persisted text_chunks row keeps the FULL, uncapped text for retrieval.
        var savedChunk = await context.TextChunks
            .Where(tc => tc.PdfDocumentId == pdfId)
            .SingleAsync();
        savedChunk.Content.Length.Should().Be(2500);
        savedChunk.Content.Should().Be(oversizedText);
    }

    // ISSUE-3197: Batch processing tests for memory optimization
    [Fact]
    public async Task Handle_WithLargeInput_ProcessesEmbeddingsInBatches()
    {
        // Arrange: Create 250 chunks (should trigger 3 batches with size 100)
        using var context = CreateFreshDbContext();
        var (advancedChunkingServiceMock, embeddingServiceMock, loggerMock, indexingSettingsMock) = CreateMocks();

        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();
        var pdf = CreatePdfDocument(pdfId, gameId, "completed", GenerateExtractedText(250));
        await context.PdfDocuments.AddAsync(pdf);
        await context.SaveChangesAsync();

        var hierarchicalChunks = GenerateFlatHierarchicalChunks(250);
        advancedChunkingServiceMock
            .Setup(x => x.ChunkDocumentAsync(It.IsAny<ExtractedDocument>(), It.IsAny<ChunkingConfiguration?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(hierarchicalChunks);

        var embeddingCallCount = 0;
        embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<string> texts, CancellationToken ct) =>
            {
                embeddingCallCount++;
                var embeddings = texts.Select(_ => GenerateRandomEmbedding(3072)).ToList();
                return new EmbeddingResult { Success = true, Embeddings = embeddings };
            });

        embeddingServiceMock.Setup(x => x.GetEmbeddingDimensions()).Returns(3072);
        embeddingServiceMock.Setup(x => x.GetModelName()).Returns("text-embedding-3-large");

        var handler = new IndexPdfCommandHandler(
            context,
            advancedChunkingServiceMock.Object,
            embeddingServiceMock.Object,
            loggerMock.Object,
            indexingSettingsMock.Object,
            Mock.Of<ISemanticResponseCache>(),
            Mock.Of<IPdfIndexingPipeline>());

        var command = new IndexPdfCommand(pdfId.ToString());

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();

        // Verify: 3 embedding calls (100 + 100 + 50)
        embeddingServiceMock.Verify(
            x => x.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3)
        );

        // Verify batch sizes
        embeddingServiceMock.Verify(
            x => x.GenerateEmbeddingsAsync(It.Is<List<string>>(l => l.Count == 100), It.IsAny<CancellationToken>()),
            Times.Exactly(2)
        );
        embeddingServiceMock.Verify(
            x => x.GenerateEmbeddingsAsync(It.Is<List<string>>(l => l.Count == 50), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Handle_WithBatchSize100_Makes12ApiCallsFor1200Chunks()
    {
        // Arrange
        using var context = CreateFreshDbContext();
        var (advancedChunkingServiceMock, embeddingServiceMock, loggerMock, indexingSettingsMock) = CreateMocks();

        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();
        var pdf = CreatePdfDocument(pdfId, gameId, "completed", GenerateExtractedText(1200));
        await context.PdfDocuments.AddAsync(pdf);
        await context.SaveChangesAsync();

        var hierarchicalChunks = GenerateFlatHierarchicalChunks(1200);
        advancedChunkingServiceMock
            .Setup(x => x.ChunkDocumentAsync(It.IsAny<ExtractedDocument>(), It.IsAny<ChunkingConfiguration?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(hierarchicalChunks);

        embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<string> texts, CancellationToken ct) =>
            {
                var embeddings = texts.Select(_ => GenerateRandomEmbedding(3072)).ToList();
                return new EmbeddingResult { Success = true, Embeddings = embeddings };
            });

        embeddingServiceMock.Setup(x => x.GetEmbeddingDimensions()).Returns(3072);
        embeddingServiceMock.Setup(x => x.GetModelName()).Returns("text-embedding-3-large");

        var handler = new IndexPdfCommandHandler(
            context,
            advancedChunkingServiceMock.Object,
            embeddingServiceMock.Object,
            loggerMock.Object,
            indexingSettingsMock.Object,
            Mock.Of<ISemanticResponseCache>(),
            Mock.Of<IPdfIndexingPipeline>());

        var command = new IndexPdfCommand(pdfId.ToString());

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();

        // Verify: 12 embedding calls (1200 / 100 = 12)
        embeddingServiceMock.Verify(
            x => x.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()),
            Times.Exactly(12)
        );
    }

    [Fact]
    public async Task Handle_WithFailedBatch_PropagatesException()
    {
        // Arrange
        using var context = CreateFreshDbContext();
        var (advancedChunkingServiceMock, embeddingServiceMock, loggerMock, indexingSettingsMock) = CreateMocks();

        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();
        var pdf = CreatePdfDocument(pdfId, gameId, "completed", GenerateExtractedText(200));
        await context.PdfDocuments.AddAsync(pdf);
        await context.SaveChangesAsync();

        var hierarchicalChunks = GenerateFlatHierarchicalChunks(200);
        advancedChunkingServiceMock
            .Setup(x => x.ChunkDocumentAsync(It.IsAny<ExtractedDocument>(), It.IsAny<ChunkingConfiguration?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(hierarchicalChunks);

        var callCount = 0;
        embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<string> texts, CancellationToken ct) =>
            {
                callCount++;
                if (callCount == 2)
                {
                    // Fail on second batch
                    return new EmbeddingResult { Success = false, ErrorMessage = "Batch processing failed" };
                }
                var embeddings = texts.Select(_ => GenerateRandomEmbedding(3072)).ToList();
                return new EmbeddingResult { Success = true, Embeddings = embeddings };
            });

        embeddingServiceMock.Setup(x => x.GetEmbeddingDimensions()).Returns(3072);
        embeddingServiceMock.Setup(x => x.GetModelName()).Returns("text-embedding-3-large");

        var handler = new IndexPdfCommandHandler(
            context,
            advancedChunkingServiceMock.Object,
            embeddingServiceMock.Object,
            loggerMock.Object,
            indexingSettingsMock.Object,
            Mock.Of<ISemanticResponseCache>(),
            Mock.Of<IPdfIndexingPipeline>());

        var command = new IndexPdfCommand(pdfId.ToString());

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Embedding generation failed");
        result.ErrorCode.Should().Be(PdfIndexingErrorCode.EmbeddingFailed);

        // Verify: Only 2 calls (first succeeds, second fails)
        embeddingServiceMock.Verify(
            x => x.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2)
        );
    }

    [Fact]
    public async Task Handle_WithEmptyExtractedText_ReturnsTextExtractionRequired()
    {
        // Arrange
        using var context = CreateFreshDbContext();
        var (advancedChunkingServiceMock, embeddingServiceMock, loggerMock, indexingSettingsMock) = CreateMocks();

        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();
        var pdf = CreatePdfDocument(pdfId, gameId, "completed", "");
        await context.PdfDocuments.AddAsync(pdf);
        await context.SaveChangesAsync();

        embeddingServiceMock.Setup(x => x.GetEmbeddingDimensions()).Returns(3072);
        embeddingServiceMock.Setup(x => x.GetModelName()).Returns("text-embedding-3-large");

        var handler = new IndexPdfCommandHandler(
            context,
            advancedChunkingServiceMock.Object,
            embeddingServiceMock.Object,
            loggerMock.Object,
            indexingSettingsMock.Object,
            Mock.Of<ISemanticResponseCache>(),
            Mock.Of<IPdfIndexingPipeline>());

        var command = new IndexPdfCommand(pdfId.ToString());

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(PdfIndexingErrorCode.TextExtractionRequired);
        result.ErrorMessage.Should().Contain("extraction required");

        // Verify: No chunking or embedding calls
        advancedChunkingServiceMock.Verify(
            x => x.ChunkDocumentAsync(It.IsAny<ExtractedDocument>(), It.IsAny<ChunkingConfiguration?>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        embeddingServiceMock.Verify(
            x => x.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Handle_WithExtractedText_SetsProcessingStateToReady()
    {
        // Arrange: PDF has extracted text but ProcessingState is Extracting (not Ready)
        using var context = CreateFreshDbContext();
        var (advancedChunkingServiceMock, embeddingServiceMock, loggerMock, indexingSettingsMock) = CreateMocks();

        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();
        var pdf = CreatePdfDocument(pdfId, gameId, "completed", GenerateExtractedText(10));
        pdf.ProcessingState = "Extracting"; // Simulate extract handler completed but state not yet Ready
        await context.PdfDocuments.AddAsync(pdf);
        await context.SaveChangesAsync();

        var hierarchicalChunks = GenerateFlatHierarchicalChunks(10);
        advancedChunkingServiceMock
            .Setup(x => x.ChunkDocumentAsync(It.IsAny<ExtractedDocument>(), It.IsAny<ChunkingConfiguration?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(hierarchicalChunks);

        embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<string> texts, CancellationToken ct) =>
            {
                var embeddings = texts.Select(_ => GenerateRandomEmbedding(3072)).ToList();
                return new EmbeddingResult { Success = true, Embeddings = embeddings };
            });

        embeddingServiceMock.Setup(x => x.GetEmbeddingDimensions()).Returns(3072);
        embeddingServiceMock.Setup(x => x.GetModelName()).Returns("text-embedding-3-large");

        var handler = new IndexPdfCommandHandler(
            context, advancedChunkingServiceMock.Object, embeddingServiceMock.Object,
            loggerMock.Object, indexingSettingsMock.Object,
            Mock.Of<ISemanticResponseCache>(),
            Mock.Of<IPdfIndexingPipeline>());

        // Act
        var result = await handler.Handle(new IndexPdfCommand(pdfId.ToString()), CancellationToken.None);

        // Assert: indexing succeeds and state is Ready
        result.Success.Should().BeTrue();
        var updatedPdf = await context.PdfDocuments.FindAsync(pdfId);
        updatedPdf!.ProcessingState.Should().Be("Ready");
        updatedPdf.ProcessingState.Should().Be("Ready");
    }

    [Fact]
    public async Task Handle_WhenChunkingFails_SetsProcessingStateToFailed()
    {
        // Arrange
        using var context = CreateFreshDbContext();
        var (advancedChunkingServiceMock, embeddingServiceMock, loggerMock, indexingSettingsMock) = CreateMocks();

        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();
        var pdf = CreatePdfDocument(pdfId, gameId, "completed", GenerateExtractedText(10));
        await context.PdfDocuments.AddAsync(pdf);
        await context.SaveChangesAsync();

        // Chunking returns empty list → triggers embedding failure path
        advancedChunkingServiceMock
            .Setup(x => x.ChunkDocumentAsync(It.IsAny<ExtractedDocument>(), It.IsAny<ChunkingConfiguration?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HierarchicalChunk>());

        embeddingServiceMock.Setup(x => x.GetEmbeddingDimensions()).Returns(3072);
        embeddingServiceMock.Setup(x => x.GetModelName()).Returns("text-embedding-3-large");

        var handler = new IndexPdfCommandHandler(
            context, advancedChunkingServiceMock.Object, embeddingServiceMock.Object,
            loggerMock.Object, indexingSettingsMock.Object,
            Mock.Of<ISemanticResponseCache>(),
            Mock.Of<IPdfIndexingPipeline>());

        // Act
        var result = await handler.Handle(new IndexPdfCommand(pdfId.ToString()), CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        var updatedPdf = await context.PdfDocuments.FindAsync(pdfId);
        updatedPdf!.ProcessingState.Should().Be("Failed");
    }

    [Fact]
    public async Task Handle_WhenUnexpectedExceptionOccurs_PersistsFailedState()
    {
        // Arrange
        using var context = CreateFreshDbContext();
        var (advancedChunkingServiceMock, embeddingServiceMock, loggerMock, indexingSettingsMock) = CreateMocks();

        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();
        var pdf = CreatePdfDocument(pdfId, gameId, "completed", GenerateExtractedText(10));
        pdf.ProcessingState = "Extracting"; // simulate mid-pipeline state
        await context.PdfDocuments.AddAsync(pdf);
        await context.SaveChangesAsync();

        // Chunking throws an unexpected exception (not a handled failure result)
        advancedChunkingServiceMock
            .Setup(x => x.ChunkDocumentAsync(It.IsAny<ExtractedDocument>(), It.IsAny<ChunkingConfiguration?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Unexpected chunking crash"));

        embeddingServiceMock.Setup(x => x.GetEmbeddingDimensions()).Returns(3072);
        embeddingServiceMock.Setup(x => x.GetModelName()).Returns("text-embedding-3-large");

        var handler = new IndexPdfCommandHandler(
            context, advancedChunkingServiceMock.Object, embeddingServiceMock.Object,
            loggerMock.Object, indexingSettingsMock.Object,
            Mock.Of<ISemanticResponseCache>(),
            Mock.Of<IPdfIndexingPipeline>());

        // Act
        var result = await handler.Handle(new IndexPdfCommand(pdfId.ToString()), CancellationToken.None);

        // Assert - failure DTO returned AND state persisted in DB
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(PdfIndexingErrorCode.UnexpectedError);
        var updatedPdf = await context.PdfDocuments.FindAsync(pdfId);
        updatedPdf!.ProcessingState.Should().Be("Failed");
        updatedPdf.ProcessingState.Should().Be("Failed");
        updatedPdf.ProcessingError.Should().Contain("Unexpected chunking crash");
    }

    // Helper methods
    private static PdfDocumentEntity CreatePdfDocument(Guid id, Guid gameId, string status, string extractedText)
    {
        return new PdfDocumentEntity
        {
            Id = id,
            FileName = "test.pdf",
            FilePath = "/uploads/test.pdf",
            FileSizeBytes = 1024,
            UploadedByUserId = Guid.NewGuid(),
            UploadedAt = DateTime.UtcNow,
            ProcessingState = status == "completed" ? "Ready" : "Pending",
            ExtractedText = extractedText
        };
    }

    private static string GenerateExtractedText(int chunkCount)
    {
        // Generate text that will produce roughly chunkCount chunks
        // Assume ~512 chars per chunk
        var text = string.Join(" ", Enumerable.Range(1, chunkCount * 50).Select(i => $"Word{i}"));
        return text;
    }

    // Slice D (Issue #730): flat parent-level (Level 0) HierarchicalChunks standing in for what
    // IAdvancedChunkingService.ChunkDocumentAsync would return, used by tests that only care about
    // chunk COUNT (batching, failure propagation) rather than heading hierarchy.
    private static List<HierarchicalChunk> GenerateFlatHierarchicalChunks(int count)
    {
        return Enumerable.Range(1, count)
            .Select(i => HierarchicalChunk.CreateParent(
                $"Chunk {i} content with sufficient text to simulate real chunks",
                new ChunkMetadata
                {
                    Page = (i / 10) + 1,
                    CharStart = (i - 1) * 512,
                    CharEnd = i * 512
                }))
            .ToList();
    }

    private static float[] GenerateRandomEmbedding(int dimensions)
    {
#pragma warning disable CA5394 // Random is sufficient for test data generation
        var random = new Random();
        return Enumerable.Range(0, dimensions).Select(_ => (float)random.NextDouble()).ToArray();
#pragma warning restore CA5394
    }

    [Fact]
    public async Task Handle_OnSuccessfulIndexing_SetsIsActiveForRagToTrue()
    {
        // Arrange: PDF with IsActiveForRag explicitly set to false (e.g. manually disabled)
        using var context = CreateFreshDbContext();
        var (advancedChunkingServiceMock, embeddingServiceMock, loggerMock, indexingSettingsMock) = CreateMocks();

        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();
        var pdf = CreatePdfDocument(pdfId, gameId, "completed", GenerateExtractedText(10));
        pdf.IsActiveForRag = false; // Explicitly disabled before indexing
        await context.PdfDocuments.AddAsync(pdf);
        await context.SaveChangesAsync();

        var hierarchicalChunks = GenerateFlatHierarchicalChunks(10);
        advancedChunkingServiceMock
            .Setup(x => x.ChunkDocumentAsync(It.IsAny<ExtractedDocument>(), It.IsAny<ChunkingConfiguration?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(hierarchicalChunks);

        embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<string> texts, CancellationToken ct) =>
            {
                var embeddings = texts.Select(_ => GenerateRandomEmbedding(3072)).ToList();
                return new EmbeddingResult { Success = true, Embeddings = embeddings };
            });

        embeddingServiceMock.Setup(x => x.GetEmbeddingDimensions()).Returns(3072);
        embeddingServiceMock.Setup(x => x.GetModelName()).Returns("text-embedding-3-large");

        // ADR-063: Verify mandatory pipeline invocation on happy path.
        // Mock.Of<>() without Verify masks silent regression where handler
        // stops calling pipeline (the very anti-pattern #2244 closed).
        var pipelineMock = new Mock<IPdfIndexingPipeline>();
        var handler = new IndexPdfCommandHandler(
            context, advancedChunkingServiceMock.Object, embeddingServiceMock.Object,
            loggerMock.Object, indexingSettingsMock.Object,
            Mock.Of<ISemanticResponseCache>(),
            pipelineMock.Object);

        // Act
        var result = await handler.Handle(new IndexPdfCommand(pdfId.ToString()), CancellationToken.None);

        // Assert: indexing succeeds and IsActiveForRag is enabled
        result.Success.Should().BeTrue();
        var updatedPdf = await context.PdfDocuments.FindAsync(pdfId);
        updatedPdf!.ProcessingState.Should().Be("Ready");
        updatedPdf.IsActiveForRag.Should().BeTrue("vectors are indexed and must be searchable via RAG");

        // Assert: handler delegated to IPdfIndexingPipeline (ADR-063 canonical example).
        // If a future refactor accidentally bypasses the pipeline, this assertion fails
        // before the silent VectorDocumentIndexedEvent bypass surfaces in production.
        pipelineMock.Verify(
            p => p.IndexAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "successful indexing MUST delegate to IPdfIndexingPipeline so VectorDocumentIndexedEvent fires structurally");
    }

    [Fact]
    [Trait("Category", TestCategories.Unit)]
    [Trait("BoundedContext", "DocumentProcessing")]
    public async Task Handle_SharedGamePdf_PropagatesSharedGameIdToChunks()
    {
        // Arrange — SharedGame PDF has SharedGameId set (PrivateGameId null).
        // Post-Phase2d (#1345): text_chunks.GameId IS shared_games.id directly
        // (legacy games table removed; PdfGameIdResolver returns SharedGameId).
        using var context = CreateFreshDbContext();
        var (advancedChunkingServiceMock, embeddingServiceMock, loggerMock, indexingSettingsMock) = CreateMocks();

        var sharedGameId = Guid.NewGuid();
        await context.SharedGames.AddAsync(new SharedGameEntity
        {
            Id = sharedGameId,
            Title = "Test SharedGame",
            CreatedAt = DateTime.UtcNow
        });

        var pdfId = Guid.NewGuid();
        var pdf = new PdfDocumentEntity
        {
            Id = pdfId,
            PrivateGameId = null,
            SharedGameId = sharedGameId,
            FileName = "shared-game-rules.pdf",
            FilePath = "/uploads/shared-game-rules.pdf",
            FileSizeBytes = 2048,
            UploadedByUserId = Guid.NewGuid(),
            UploadedAt = DateTime.UtcNow,
            ProcessingState = "Ready",
            ExtractedText = GenerateExtractedText(5)
        };
        await context.PdfDocuments.AddAsync(pdf);
        await context.SaveChangesAsync();

        var hierarchicalChunks = GenerateFlatHierarchicalChunks(5);
        advancedChunkingServiceMock
            .Setup(x => x.ChunkDocumentAsync(It.IsAny<ExtractedDocument>(), It.IsAny<ChunkingConfiguration?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(hierarchicalChunks);

        embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<string> texts, CancellationToken ct) =>
            {
                var embeddings = texts.Select(_ => GenerateRandomEmbedding(3072)).ToList();
                return new EmbeddingResult { Success = true, Embeddings = embeddings };
            });

        embeddingServiceMock.Setup(x => x.GetEmbeddingDimensions()).Returns(3072);
        embeddingServiceMock.Setup(x => x.GetModelName()).Returns("text-embedding-3-large");

        // ADR-063: Verify pipeline invocation + assert that SharedGameId flows through
        // the pipeline call. Without explicit Verify, a future refactor that wired the
        // wrong gameId would pass this test (text_chunks assertion is independent).
        var pipelineMock = new Mock<IPdfIndexingPipeline>();
        var handler = new IndexPdfCommandHandler(
            context, advancedChunkingServiceMock.Object, embeddingServiceMock.Object,
            loggerMock.Object, indexingSettingsMock.Object,
            Mock.Of<ISemanticResponseCache>(),
            pipelineMock.Object);

        // Act
        var result = await handler.Handle(new IndexPdfCommand(pdfId.ToString()), CancellationToken.None);

        // Assert — indexing succeeds
        result.Success.Should().BeTrue();

        // Assert — text chunks have SharedGameId propagated AND GameId == SharedGameId (post-Phase2d)
        var savedChunks = await context.TextChunks
            .Where(tc => tc.PdfDocumentId == pdfId)
            .ToListAsync();

        savedChunks.Should().HaveCount(5);
        savedChunks.Should().AllSatisfy(chunk =>
        {
            chunk.SharedGameId.Should().Be(sharedGameId, "SharedGameId must propagate from PDF to text chunks");
            chunk.GameId.Should().Be(sharedGameId, "post-Phase2d: text_chunks.GameId IS shared_games.id (no more legacy games table)");
        });

        // Assert: pipeline invoked with the SharedGame's gameId (post-Phase2d resolution).
        // PdfGameIdResolver returns SharedGameId for SharedGame PDFs; the pipeline call
        // must propagate that through so the VectorDocument carries the correct GameId.
        pipelineMock.Verify(
            p => p.IndexAsync(
                pdfId,
                It.Is<Guid?>(g => g == sharedGameId),
                It.Is<Guid?>(s => s == sharedGameId),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "SharedGame PDF indexing MUST pass sharedGameId and the resolved gameId through to the pipeline");
    }

    // NOTE: Full workflow tests (text chunking, embedding generation, pgvector indexing)
    // should be in integration test suite due to DbContext and multi-service complexity.
    // See integration-tests.yml workflow.
}
