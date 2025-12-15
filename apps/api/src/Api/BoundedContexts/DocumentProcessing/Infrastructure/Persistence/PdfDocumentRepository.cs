using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
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
        var entities = await DbContext.PdfDocuments
            .AsNoTracking()
            .Where(p => p.GameId == gameId)
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
        return PdfDocument.Reconstitute(
            id: entity.Id,
            gameId: entity.GameId,
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
            sortOrder: entity.SortOrder
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
            ProcessedAt = domain.ProcessedAt,
            PageCount = domain.PageCount,
            ProcessingError = domain.ProcessingError,
            Language = domain.Language.Value, // Issue #2029: Extract string from Value Object
            CollectionId = domain.CollectionId, // Issue #2051
            DocumentType = domain.DocumentType.Value, // Issue #2051: Extract string from Value Object
            SortOrder = domain.SortOrder // Issue #2051
        };
    }
}
