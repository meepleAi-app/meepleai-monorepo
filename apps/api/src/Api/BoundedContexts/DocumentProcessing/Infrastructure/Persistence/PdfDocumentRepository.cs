using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.Persistence;

internal class PdfDocumentRepository : RepositoryBase, IPdfDocumentRepository
{
    public PdfDocumentRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<PdfDocument?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.PdfDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken).ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<IReadOnlyList<PdfDocument>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.PdfDocuments
            .AsNoTracking()
            .OrderByDescending(p => p.UploadedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<PdfDocument>> FindByGameIdAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        // Direct match on pdf_documents.GameId
        var entities = await DbContext.PdfDocuments
            .AsNoTracking()
            .Where(p => p.GameId == gameId)
            .OrderByDescending(p => p.UploadedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        // If no direct match, resolve via games.SharedGameId → games.Id → pdf_documents.GameId
        if (entities.Count == 0)
        {
            var resolvedGameIds = await DbContext.Games
                .AsNoTracking()
                .Where(g => g.SharedGameId == gameId)
                .Select(g => g.Id)
                .ToListAsync(cancellationToken).ConfigureAwait(false);

            if (resolvedGameIds.Count > 0)
            {
                entities = await DbContext.PdfDocuments
                    .AsNoTracking()
                    .Where(p => p.GameId.HasValue && resolvedGameIds.Contains(p.GameId.Value))
                    .OrderByDescending(p => p.UploadedAt)
                    .ToListAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<PdfDocument>> FindByPrivateGameIdAsync(Guid privateGameId, CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.PdfDocuments
            .AsNoTracking()
            .Where(p => p.PrivateGameId == privateGameId)
            .OrderByDescending(p => p.UploadedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<PdfDocument>> FindByStatusAsync(string status, CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.PdfDocuments
            .AsNoTracking()
            .Where(p => p.ProcessingStatus == status)
            .OrderByDescending(p => p.UploadedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<PdfDocument>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.ToList();
        if (idList.Count == 0)
            return Array.Empty<PdfDocument>();

        var entities = await DbContext.PdfDocuments
            .AsNoTracking()
            .Where(p => idList.Contains(p.Id))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<PdfDocument>> FindByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.PdfDocuments
            .AsNoTracking()
            .Where(p => p.UploadedByUserId == userId)
            .OrderByDescending(p => p.UploadedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task AddAsync(PdfDocument document, CancellationToken cancellationToken = default)
    {
        CollectDomainEvents(document);
        var entity = MapToPersistence(document);
        await DbContext.PdfDocuments.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(PdfDocument document, CancellationToken cancellationToken = default)
    {
        CollectDomainEvents(document);
        var entity = MapToPersistence(document);

        // Detach existing tracked entity to avoid conflicts (SPRINT-5 Issue #1142)
        var tracked = DbContext.ChangeTracker.Entries<Api.Infrastructure.Entities.PdfDocumentEntity>()
            .FirstOrDefault(e => e.Entity.Id == entity.Id);

        if (tracked != null)
            tracked.State = EntityState.Detached;

        DbContext.PdfDocuments.Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(PdfDocument document, CancellationToken cancellationToken = default)
    {
        var entity = MapToPersistence(document);
        DbContext.PdfDocuments.Remove(entity);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.PdfDocuments.AnyAsync(p => p.Id == id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> ExistsByContentHashAsync(string contentHash, Guid? gameId, Guid? privateGameId, CancellationToken cancellationToken = default)
    {
        return await DbContext.PdfDocuments.AnyAsync(p =>
            p.ContentHash == contentHash &&
            ((gameId.HasValue && p.GameId == gameId) ||
             (privateGameId.HasValue && p.PrivateGameId == privateGameId)),
            cancellationToken).ConfigureAwait(false);
    }

    private static PdfDocument MapToDomain(Api.Infrastructure.Entities.PdfDocumentEntity entity)
    {
        var fileName = new FileName(entity.FileName);
        var fileSize = new FileSize(entity.FileSizeBytes);

        // Issue #2029: Convert string to LanguageCode Value Object
        var language = string.IsNullOrWhiteSpace(entity.Language)
            ? LanguageCode.English
            : new LanguageCode(entity.Language);

        // Issue #2051: Convert string to DocumentType Value Object
        var documentType = string.IsNullOrWhiteSpace(entity.DocumentType)
            ? DocumentType.Base
            : new DocumentType(entity.DocumentType);

        // Issue #2140: Use Reconstitute factory method instead of reflection
        // Issue #2732: Added SharedGameId, ContributorId, SourceDocumentId
        // Issue #3664: Added PrivateGameId
        // Issue #4216: Added retry tracking fields

        // Parse retry-specific enums (ignoreCase for .ToString() output compatibility)
        var errorCategory = string.IsNullOrWhiteSpace(entity.ErrorCategory)
            ? (ErrorCategory?)null
            : Enum.Parse<ErrorCategory>(entity.ErrorCategory, ignoreCase: true);

        var failedAtState = string.IsNullOrWhiteSpace(entity.FailedAtState)
            ? (PdfProcessingState?)null
            : Enum.Parse<PdfProcessingState>(entity.FailedAtState, ignoreCase: true);

        // Parse ProcessingState from the stored string (Issue #4215)
        var processingState = !string.IsNullOrWhiteSpace(entity.ProcessingState)
            && Enum.TryParse<PdfProcessingState>(entity.ProcessingState, ignoreCase: true, out var parsedState)
                ? parsedState
                : (PdfProcessingState?)null;

        return PdfDocument.Reconstitute(
            id: entity.Id,
            gameId: entity.GameId ?? Guid.Empty,
            fileName: fileName,
            filePath: entity.FilePath,
            fileSize: fileSize,
            uploadedByUserId: entity.UploadedByUserId,
            uploadedAt: entity.UploadedAt,
            processingStatus: entity.ProcessingStatus,
            processedAt: entity.ProcessedAt,
            pageCount: entity.PageCount,
            processingError: entity.ProcessingError,
            language: language,
            collectionId: entity.CollectionId,
            documentType: documentType,
            sortOrder: entity.SortOrder,
            isPublic: entity.IsPublic,
            sharedGameId: entity.SharedGameId,
            contributorId: entity.ContributorId,
            sourceDocumentId: entity.SourceDocumentId,
            privateGameId: entity.PrivateGameId,
            processingState: processingState, // Fix: pass stored enum state to avoid fallback to legacy string
            retryCount: entity.RetryCount,
            errorCategory: errorCategory,
            failedAtState: failedAtState,
            uploadingStartedAt: entity.UploadingStartedAt, // Issue #4219
            extractingStartedAt: entity.ExtractingStartedAt, // Issue #4219
            chunkingStartedAt: entity.ChunkingStartedAt, // Issue #4219
            embeddingStartedAt: entity.EmbeddingStartedAt, // Issue #4219
            indexingStartedAt: entity.IndexingStartedAt, // Issue #4219
            contentHash: entity.ContentHash,
            documentCategory: !string.IsNullOrWhiteSpace(entity.DocumentCategory)
                && Enum.TryParse<DocumentCategory>(entity.DocumentCategory, ignoreCase: true, out var parsedCategory)
                    ? parsedCategory
                    : DocumentCategory.Rulebook, // Issue #5443
            baseDocumentId: entity.BaseDocumentId, // Issue #5444
            copyrightDisclaimerAcceptedAt: entity.CopyrightDisclaimerAcceptedAt, // Issue #5446
            copyrightDisclaimerAcceptedBy: entity.CopyrightDisclaimerAcceptedBy, // Issue #5446
            isActiveForRag: entity.IsActiveForRag, // Issue #5446
            versionLabel: entity.VersionLabel // Issue #5447
        );
    }

    private static Api.Infrastructure.Entities.PdfDocumentEntity MapToPersistence(PdfDocument domain)
    {
        return new Api.Infrastructure.Entities.PdfDocumentEntity
        {
            Id = domain.Id,
            GameId = domain.GameId,
            FileName = domain.FileName.Value,
            FilePath = domain.FilePath,
            FileSizeBytes = domain.FileSize.Bytes,
            ContentType = domain.ContentType,
            UploadedByUserId = domain.UploadedByUserId,
            UploadedAt = domain.UploadedAt,
            ProcessingStatus = domain.ProcessingStatus,
            ProcessingState = domain.ProcessingState.ToString(), // Issue #4215
            ProcessedAt = domain.ProcessedAt,
            PageCount = domain.PageCount,
            ProcessingError = domain.ProcessingError,
            Language = domain.Language.Value, // Issue #2029: Extract string from Value Object
            CollectionId = domain.CollectionId, // Issue #2051
            DocumentType = domain.DocumentType.Value, // Issue #2051: Extract string from Value Object
            SortOrder = domain.SortOrder, // Issue #2051
            IsPublic = domain.IsPublic,
            SharedGameId = domain.SharedGameId, // Issue #2732
            ContributorId = domain.ContributorId, // Issue #2732
            SourceDocumentId = domain.SourceDocumentId, // Issue #2732
            PrivateGameId = domain.PrivateGameId, // Issue #3664
            RetryCount = domain.RetryCount, // Issue #4216
            ErrorCategory = domain.ErrorCategory?.ToString(), // Issue #4216
            FailedAtState = domain.FailedAtState?.ToString(), // Issue #4216
            UploadingStartedAt = domain.UploadingStartedAt, // Issue #4219
            ExtractingStartedAt = domain.ExtractingStartedAt, // Issue #4219
            ChunkingStartedAt = domain.ChunkingStartedAt, // Issue #4219
            EmbeddingStartedAt = domain.EmbeddingStartedAt, // Issue #4219
            IndexingStartedAt = domain.IndexingStartedAt, // Issue #4219
            ContentHash = domain.ContentHash,
            DocumentCategory = domain.DocumentCategory.ToString(), // Issue #5443
            BaseDocumentId = domain.BaseDocumentId, // Issue #5444
            CopyrightDisclaimerAcceptedAt = domain.CopyrightDisclaimerAcceptedAt, // Issue #5446
            CopyrightDisclaimerAcceptedBy = domain.CopyrightDisclaimerAcceptedBy, // Issue #5446
            IsActiveForRag = domain.IsActiveForRag, // Issue #5446
            VersionLabel = domain.VersionLabel // Issue #5447
        };
    }
}
