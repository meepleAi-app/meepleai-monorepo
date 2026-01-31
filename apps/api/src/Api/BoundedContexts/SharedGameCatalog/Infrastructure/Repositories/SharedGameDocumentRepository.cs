using System.Text.Json;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for SharedGameDocument entities.
/// </summary>
internal sealed class SharedGameDocumentRepository : ISharedGameDocumentRepository
{
    private readonly MeepleAiDbContext _context;

    public SharedGameDocumentRepository(MeepleAiDbContext context)
    {
        _context = context;
    }

    public async Task<SharedGameDocument?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.SharedGameDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entity == null ? null : MapToDomain(entity);
    }

    public async Task<IReadOnlyList<SharedGameDocument>> GetBySharedGameIdAsync(
        Guid sharedGameId,
        CancellationToken cancellationToken = default)
    {
        var entities = await _context.SharedGameDocuments
            .AsNoTracking()
            .Where(d => d.SharedGameId == sharedGameId)
            .OrderBy(d => d.DocumentType)
            .ThenByDescending(d => d.IsActive)
            .ThenBy(d => d.Version)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<SharedGameDocument>> GetBySharedGameIdAndTypeAsync(
        Guid sharedGameId,
        SharedGameDocumentType documentType,
        CancellationToken cancellationToken = default)
    {
        var entities = await _context.SharedGameDocuments
            .AsNoTracking()
            .Where(d => d.SharedGameId == sharedGameId && d.DocumentType == (int)documentType)
            .OrderByDescending(d => d.IsActive)
            .ThenBy(d => d.Version)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<SharedGameDocument?> GetActiveDocumentAsync(
        Guid sharedGameId,
        SharedGameDocumentType documentType,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context.SharedGameDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(
                d => d.SharedGameId == sharedGameId
                    && d.DocumentType == (int)documentType
                    && d.IsActive,
                cancellationToken)
            .ConfigureAwait(false);

        return entity == null ? null : MapToDomain(entity);
    }

    public async Task<IReadOnlyList<SharedGameDocument>> GetActiveDocumentsAsync(
        Guid sharedGameId,
        CancellationToken cancellationToken = default)
    {
        var entities = await _context.SharedGameDocuments
            .AsNoTracking()
            .Where(d => d.SharedGameId == sharedGameId && d.IsActive)
            .OrderBy(d => d.DocumentType)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<SharedGameDocument>> SearchByTagsAsync(
        IEnumerable<string> tags,
        CancellationToken cancellationToken = default)
    {
        var normalizedTags = tags.Select(t => t.ToLowerInvariant()).ToList();

        if (normalizedTags.Count == 0)
        {
            return [];
        }

        // Use PostgreSQL JSONB @> operator for efficient array containment check
        // This leverages the GIN index on tags_json column for O(log n) performance
        // The query finds documents where tags_json array contains ANY of the provided tags
        var sql = """
            SELECT
                id,
                shared_game_id,
                pdf_document_id,
                document_type,
                version,
                is_active,
                tags_json,
                created_at,
                created_by
            FROM shared_game_documents
            WHERE document_type = @documentType
              AND tags_json IS NOT NULL
              AND tags_json ?| @searchTags
            ORDER BY created_at DESC
            """;

        var documentTypeParam = new NpgsqlParameter("@documentType", (int)SharedGameDocumentType.Homerule);

        // S3265: NpgsqlDbType uses bitwise combination for array types despite not having [Flags]
        // This is the official Npgsql API pattern for array parameters
#pragma warning disable S3265
        var searchTagsParam = new NpgsqlParameter("@searchTags", NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Text)
        {
            Value = normalizedTags.ToArray()
        };
#pragma warning restore S3265

        var entities = await _context.SharedGameDocuments
            .FromSqlRaw(sql, documentTypeParam, searchTagsParam)
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<bool> VersionExistsAsync(
        Guid sharedGameId,
        SharedGameDocumentType documentType,
        string version,
        CancellationToken cancellationToken = default)
    {
        return await _context.SharedGameDocuments
            .AsNoTracking()
            .AnyAsync(
                d => d.SharedGameId == sharedGameId
                    && d.DocumentType == (int)documentType
                    && d.Version == version,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(SharedGameDocument document, CancellationToken cancellationToken = default)
    {
        var entity = MapToEntity(document);
        await _context.SharedGameDocuments.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public void Update(SharedGameDocument document)
    {
        var entity = MapToEntity(document);
        _context.SharedGameDocuments.Update(entity);
    }

    public void Remove(SharedGameDocument document)
    {
        var entity = MapToEntity(document);
        _context.SharedGameDocuments.Remove(entity);
    }

    public async Task DeactivateOtherVersionsAsync(
        Guid sharedGameId,
        SharedGameDocumentType documentType,
        Guid exceptDocumentId,
        CancellationToken cancellationToken = default)
    {
        await _context.SharedGameDocuments
            .Where(d => d.SharedGameId == sharedGameId
                && d.DocumentType == (int)documentType
                && d.Id != exceptDocumentId
                && d.IsActive)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(d => d.IsActive, false),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> CountByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.SharedGameDocuments
            .AsNoTracking()
            .CountAsync(d => d.CreatedBy == userId, cancellationToken)
            .ConfigureAwait(false);
    }

    // Mapping methods

    private static SharedGameDocument MapToDomain(SharedGameDocumentEntity entity)
    {
        var tags = string.IsNullOrEmpty(entity.TagsJson)
            ? null
            : JsonSerializer.Deserialize<List<string>>(entity.TagsJson);

        return new SharedGameDocument(
            entity.Id,
            entity.SharedGameId,
            entity.PdfDocumentId,
            (SharedGameDocumentType)entity.DocumentType,
            entity.Version,
            entity.IsActive,
            tags,
            entity.CreatedAt,
            entity.CreatedBy);
    }

    private static SharedGameDocumentEntity MapToEntity(SharedGameDocument document)
    {
        var tagsJson = document.Tags.Any()
            ? JsonSerializer.Serialize(document.Tags)
            : null;

        return new SharedGameDocumentEntity
        {
            Id = document.Id,
            SharedGameId = document.SharedGameId,
            PdfDocumentId = document.PdfDocumentId,
            DocumentType = (int)document.DocumentType,
            Version = document.Version,
            IsActive = document.IsActive,
            TagsJson = tagsJson,
            CreatedAt = document.CreatedAt,
            CreatedBy = document.CreatedBy
        };
    }
}
