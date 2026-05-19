using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.Persistence;

/// <summary>
/// EF Core repository for the <see cref="PhotoBatchUpload"/> aggregate.
/// Maps directly to the domain entity (no separate persistence entity layer).
/// Libro Game AI Assistant MVP Phase 1.
/// </summary>
internal sealed class PhotoBatchUploadRepository : RepositoryBase, IPhotoBatchUploadRepository
{
    public PhotoBatchUploadRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<PhotoBatchUpload?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<PhotoBatchUpload>()
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<PhotoBatchUpload>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var results = await DbContext.Set<PhotoBatchUpload>()
            .AsNoTracking()
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return results;
    }

    public async Task<IReadOnlyList<PhotoBatchUpload>> FindByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        var results = await DbContext.Set<PhotoBatchUpload>()
            .AsNoTracking()
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(ct).ConfigureAwait(false);

        return results;
    }

    public async Task<PhotoBatchUpload?> FindByIdWithPagesAsync(Guid id, CancellationToken ct = default)
    {
        return await DbContext.Set<PhotoBatchUpload>()
            .AsNoTracking()
            .Include(b => b.Pages)
            .FirstOrDefaultAsync(b => b.Id == id, ct).ConfigureAwait(false);
    }

    public async Task AddAsync(PhotoBatchUpload entity, CancellationToken cancellationToken = default)
    {
        CollectDomainEvents(entity);
        await DbContext.Set<PhotoBatchUpload>().AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(PhotoBatchUpload entity, CancellationToken cancellationToken = default)
    {
        CollectDomainEvents(entity);

        // Detach any existing tracked instance to avoid EF change-tracking conflicts
        var tracked = DbContext.ChangeTracker.Entries<PhotoBatchUpload>()
            .FirstOrDefault(e => e.Entity.Id == entity.Id);

        if (tracked != null)
            tracked.State = EntityState.Detached;

        DbContext.Set<PhotoBatchUpload>().Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(PhotoBatchUpload entity, CancellationToken cancellationToken = default)
    {
        DbContext.Set<PhotoBatchUpload>().Remove(entity);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<PhotoBatchUpload>()
            .AnyAsync(b => b.Id == id, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<string?> GetPageTextAsync(Guid uploadId, int pageNumber, CancellationToken ct = default)
    {
        return await DbContext.PhotoBatchPages
            .AsNoTracking()
            .Where(p => p.PhotoBatchUploadId == uploadId && p.PageNumber == pageNumber)
            .Select(p => p.ExtractedText)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<string?> GetPageTextByParagraphNumberAsync(
        Guid uploadId,
        int paragraphNumber,
        CancellationToken ct = default)
    {
        // Npgsql translates Array.Contains to PostgreSQL `paragraphNumber = ANY(paragraph_numbers)`,
        // which uses the GIN index defined in PhotoBatchPageEntityConfiguration when the array
        // operator is `@>` and falls back to a seq-scan otherwise. EF Core's `Contains` keeps the
        // query LINQ-translatable across providers (in-memory test uses the same predicate).
        return await DbContext.PhotoBatchPages
            .AsNoTracking()
            .Where(p => p.PhotoBatchUploadId == uploadId && p.ParagraphNumbers.Contains(paragraphNumber))
            .OrderBy(p => p.PageNumber)
            .Select(p => p.ExtractedText)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<bool> BelongsToUserAsync(Guid uploadId, Guid userId, CancellationToken ct = default)
    {
        return await DbContext.PhotoBatchUploads
            .AsNoTracking()
            .AnyAsync(b => b.Id == uploadId && b.UserId == userId, ct)
            .ConfigureAwait(false);
    }
}
