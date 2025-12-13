using System.Text.Json;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.Persistence;

/// <summary>
/// Repository implementation for DocumentCollection aggregate.
/// Issue #2051: Persistence for multi-document collections
/// </summary>
public class DocumentCollectionRepository : RepositoryBase, IDocumentCollectionRepository
{
    public DocumentCollectionRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<DocumentCollection?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.DocumentCollections
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken).ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<IReadOnlyList<DocumentCollection>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.DocumentCollections
            .AsNoTracking()
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<DocumentCollection?> FindByGameIdAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.DocumentCollections
            .AsNoTracking()
            .Where(c => c.GameId == gameId)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<IReadOnlyList<DocumentCollection>> FindByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.DocumentCollections
            .AsNoTracking()
            .Where(c => c.CreatedByUserId == userId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<bool> ExistsForGameAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        return await DbContext.DocumentCollections
            .AnyAsync(c => c.GameId == gameId, cancellationToken).ConfigureAwait(false);
    }

    public async Task AddAsync(DocumentCollection collection, CancellationToken cancellationToken = default)
    {
        var entity = MapToPersistence(collection);
        await DbContext.DocumentCollections.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(DocumentCollection collection, CancellationToken cancellationToken = default)
    {
        var entity = MapToPersistence(collection);

        // Check if already tracked
        var tracked = DbContext.ChangeTracker.Entries<Api.Infrastructure.Entities.DocumentCollectionEntity>()
            .FirstOrDefault(e => e.Entity.Id == collection.Id);

        if (tracked == null)
            DbContext.DocumentCollections.Update(entity);

        return Task.CompletedTask;
    }

    public Task DeleteAsync(DocumentCollection collection, CancellationToken cancellationToken = default)
    {
        var entity = MapToPersistence(collection);
        DbContext.DocumentCollections.Remove(entity);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.DocumentCollections.AnyAsync(c => c.Id == id, cancellationToken).ConfigureAwait(false);
    }

    private static DocumentCollection MapToDomain(Api.Infrastructure.Entities.DocumentCollectionEntity entity)
    {
        var collectionName = new CollectionName(entity.Name);

        // Issue #2140: Deserialize documents from JSON to CollectionDocument domain entities
        var documents = new List<CollectionDocument>();
        if (!string.IsNullOrWhiteSpace(entity.DocumentsJson) && entity.DocumentsJson != "[]")
        {
            var documentDtos = JsonSerializer.Deserialize<List<CollectionDocumentDto>>(entity.DocumentsJson);
            if (documentDtos != null)
            {
                documents = documentDtos.Select(dto =>
                {
                    var docType = new DocumentType(dto.Type);
                    return new CollectionDocument(
                        dto.PdfDocumentId,
                        docType,
                        dto.SortOrder,
                        dto.AddedAt);
                }).ToList();
            }
        }

        // Issue #2140: Use Reconstitute factory method instead of reflection
        return DocumentCollection.Reconstitute(
            id: entity.Id,
            gameId: entity.GameId,
            name: collectionName,
            description: entity.Description,
            createdByUserId: entity.CreatedByUserId,
            createdAt: entity.CreatedAt,
            updatedAt: entity.UpdatedAt,
            documents: documents
        );
    }

    private static Api.Infrastructure.Entities.DocumentCollectionEntity MapToPersistence(DocumentCollection domain)
    {
        // Serialize documents to JSON
        var documentsDto = domain.Documents.Select(d => new CollectionDocumentDto
        {
            PdfDocumentId = d.PdfDocumentId,
            Type = d.Type.Value,
            SortOrder = d.SortOrder,
            AddedAt = d.AddedAt
        }).ToList();

        var documentsJson = JsonSerializer.Serialize(documentsDto);

        return new Api.Infrastructure.Entities.DocumentCollectionEntity
        {
            Id = domain.Id,
            GameId = domain.GameId,
            Name = domain.Name.Value,
            Description = domain.Description,
            CreatedByUserId = domain.CreatedByUserId,
            CreatedAt = domain.CreatedAt,
            UpdatedAt = domain.UpdatedAt,
            DocumentsJson = documentsJson
        };
    }
}

/// <summary>
/// DTO for JSON serialization of collection documents.
/// </summary>
internal record CollectionDocumentDto
{
    public Guid PdfDocumentId { get; init; }
    public string Type { get; init; } = "base";
    public int SortOrder { get; init; }
    public DateTime AddedAt { get; init; }
}
