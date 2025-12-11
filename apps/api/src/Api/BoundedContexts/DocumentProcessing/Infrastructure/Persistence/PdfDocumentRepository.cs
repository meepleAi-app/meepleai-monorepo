using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.Persistence;

public class PdfDocumentRepository : RepositoryBase, IPdfDocumentRepository
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

        var doc = new PdfDocument(
            id: entity.Id,
            gameId: entity.GameId,
            fileName: fileName,
            filePath: entity.FilePath,
            fileSize: fileSize,
            uploadedByUserId: entity.UploadedByUserId
        );

        // Override properties from DB
        var processingStatusProp = typeof(PdfDocument).GetProperty("ProcessingStatus");
        processingStatusProp?.SetValue(doc, entity.ProcessingStatus);

        var processedAtProp = typeof(PdfDocument).GetProperty("ProcessedAt");
        processedAtProp?.SetValue(doc, entity.ProcessedAt);

        var pageCountProp = typeof(PdfDocument).GetProperty("PageCount");
        pageCountProp?.SetValue(doc, entity.PageCount);

        var processingErrorProp = typeof(PdfDocument).GetProperty("ProcessingError");
        processingErrorProp?.SetValue(doc, entity.ProcessingError);

        var uploadedAtProp = typeof(PdfDocument).GetProperty("UploadedAt");
        uploadedAtProp?.SetValue(doc, entity.UploadedAt);

        return doc;
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
            ProcessingError = domain.ProcessingError
        };
    }
}
