using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Infrastructure;
using Api.Services;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// Issue #2947 — builds a <see cref="PdfProcessingPipelineService"/> with only the
/// collaborators the cover path touches wired to real/mocked instances and all
/// other required constructor dependencies stubbed with permissive mocks. Keeps
/// the cover-focused test independent of the large main fixture.
/// </summary>
internal static class PdfProcessingPipelineServiceCoverTestFactory
{
    public static PdfProcessingPipelineService Create(
        MeepleAiDbContext db,
        IBlobStorageService blob,
        IPdfCoverExtractor coverExtractor,
        IPdfCoverUploadPipeline? coverUploadPipeline,
        IDomainEventCollector eventCollector)
    {
        return new PdfProcessingPipelineService(
            db: db,
            pdfClaimService: Mock.Of<IPdfClaimService>(),
            pdfTextExtractor: Mock.Of<IPdfTextExtractor>(),
            tableExtractor: Mock.Of<IPdfTableExtractor>(),
            chunkingService: Mock.Of<ITextChunkingService>(),
            embeddingService: Mock.Of<IEmbeddingService>(),
            blobStorageService: blob,
            timeProvider: TimeProvider.System,
            logger: NullLogger<PdfProcessingPipelineService>.Instance,
            languageDetector: Mock.Of<ILanguageDetector>(),
            chunkTranslationService: Mock.Of<IChunkTranslationService>(),
            indexingPipeline: Mock.Of<IPdfIndexingPipeline>(),
            raptorIndexer: null,
            entityExtractor: null,
            vectorStore: null,
            featureFlagService: null,
            roleClassifier: null,
            pdfCoverExtractor: coverExtractor,
            eventCollector: eventCollector,
            pdfCoverUploadPipeline: coverUploadPipeline);
    }
}
