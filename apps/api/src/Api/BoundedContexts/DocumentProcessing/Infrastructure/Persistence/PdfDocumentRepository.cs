using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.Persistence;

public class PdfDocumentRepository : IPdfDocumentRepository
{
    private readonly MeepleAiDbContext _dbContext;

    public PdfDocumentRepository(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PdfDocument?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.PdfDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<List<PdfDocument>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.PdfDocuments
            .AsNoTracking()
            .OrderByDescending(p => p.UploadedAt)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<PdfDocument>> FindByGameIdAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.PdfDocuments
            .AsNoTracking()
            .Where(p => p.GameId == gameId)
            .OrderByDescending(p => p.UploadedAt)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<PdfDocument>> FindByStatusAsync(string status, CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.PdfDocuments
            .AsNoTracking()
            .Where(p => p.ProcessingStatus == status)
            .OrderByDescending(p => p.UploadedAt)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task AddAsync(PdfDocument document, CancellationToken cancellationToken = default)
    {
        var entity = MapToPersistence(document);
        await _dbContext.PdfDocuments.AddAsync(entity, cancellationToken);
    }

    public Task UpdateAsync(PdfDocument document, CancellationToken cancellationToken = default)
    {
        var entity = MapToPersistence(document);
        _dbContext.PdfDocuments.Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(PdfDocument document, CancellationToken cancellationToken = default)
    {
        var entity = MapToPersistence(document);
        _dbContext.PdfDocuments.Remove(entity);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.PdfDocuments.AnyAsync(p => p.Id == id, cancellationToken);
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
