using Api.BoundedContexts.KnowledgeBase.Domain.Projections;
using Api.Infrastructure;
using Api.SharedKernel.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Projections;

/// <summary>
/// Infrastructure implementation of ICopyrightDataProjection.
/// Queries PdfDocuments and UserLibraryEntries tables directly via DbContext
/// to resolve copyright tier without coupling to DocumentProcessing or UserLibrary BCs.
/// </summary>
internal sealed class CopyrightDataProjection : ICopyrightDataProjection
{
    private readonly MeepleAiDbContext _db;

    public CopyrightDataProjection(MeepleAiDbContext db) => _db = db;

    public async Task<IReadOnlyDictionary<string, PdfCopyrightInfo>> GetPdfCopyrightInfoAsync(
        IReadOnlyList<string> documentIds,
        CancellationToken ct)
    {
        if (documentIds.Count == 0)
            return new Dictionary<string, PdfCopyrightInfo>(StringComparer.Ordinal);

        // Parse string IDs back to Guids for the DB query
        var guidIds = documentIds
            .Select(id => Guid.TryParse(id, out var g) ? g : (Guid?)null)
            .Where(g => g.HasValue)
            .Select(g => g!.Value)
            .ToList();

        if (guidIds.Count == 0)
            return new Dictionary<string, PdfCopyrightInfo>(StringComparer.Ordinal);

        var pdfs = await _db.PdfDocuments
            .AsNoTracking()
            .Where(p => guidIds.Contains(p.Id))
            .Select(p => new
            {
                p.Id,
                p.LicenseType,
                Category = p.DocumentCategory,
                p.UploadedByUserId,
                p.GameId,
                p.PrivateGameId,
                p.IsPublic
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return pdfs.ToDictionary(
            p => p.Id.ToString(),
            p => new PdfCopyrightInfo(
                p.Id.ToString(),
                // LicenseType is stored as int; cast with bounds check
                Enum.IsDefined(typeof(LicenseType), p.LicenseType)
                    ? (LicenseType)p.LicenseType
                    : LicenseType.Copyrighted,
                // DocumentCategory is stored as string; parse with fallback
                Enum.TryParse<DocumentCategory>(p.Category, out var dc)
                    ? dc
                    : DocumentCategory.Other,
                p.UploadedByUserId,
                p.GameId,
                p.PrivateGameId,
                p.IsPublic),
            StringComparer.Ordinal);
    }

    public async Task<IReadOnlyDictionary<Guid, bool>> CheckOwnershipAsync(
        Guid userId,
        IReadOnlyList<Guid> gameIds,
        CancellationToken ct)
    {
        if (userId == Guid.Empty || gameIds.Count == 0)
            return gameIds.ToDictionary(id => id, _ => false);

        // Query UserLibraryEntries — OwnershipDeclaredAt != null means ownership is declared
        var ownedGameIds = await _db.UserLibraryEntries
            .AsNoTracking()
            .Where(e => e.UserId == userId
                && e.OwnershipDeclaredAt != null
                && (
                    (e.SharedGameId != null && gameIds.Contains(e.SharedGameId.Value))
                    || (e.PrivateGameId != null && gameIds.Contains(e.PrivateGameId.Value))
                ))
            .Select(e => e.SharedGameId ?? e.PrivateGameId!.Value)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var ownedSet = ownedGameIds.ToHashSet();
        return gameIds.ToDictionary(id => id, id => ownedSet.Contains(id));
    }
}
