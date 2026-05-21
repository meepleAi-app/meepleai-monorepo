using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Infrastructure.Repositories;

internal class GameBookRepository : IGameBookRepository
{
    private readonly MeepleAiDbContext _db;

    public GameBookRepository(MeepleAiDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<GameBook?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return await _db.GameBooks
            .FirstOrDefaultAsync(b => b.Id == id, ct)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<GameBook>> ListByGameRefAsync(
        GameRef gameRef, Guid? ownerUserId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(gameRef);
        var query = _db.GameBooks
            .Where(b => b.GameRef.Id == gameRef.Id && b.GameRef.Kind == gameRef.Kind);

        // Community books always visible. Personal books only to owner.
        if (ownerUserId.HasValue)
            query = query.Where(b => b.OwnerUserId == null || b.OwnerUserId == ownerUserId.Value);
        else
            query = query.Where(b => b.OwnerUserId == null);

        return await query.ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<GameBook?> FindCommunityByKbSourceAsync(Guid pdfDocId, CancellationToken ct)
    {
        return await _db.GameBooks
            .FirstOrDefaultAsync(b => b.KbSourceDocId == pdfDocId && b.OwnerUserId == null, ct)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(GameBook book, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(book);
        await _db.GameBooks.AddAsync(book, ct).ConfigureAwait(false);
    }

    public Task UpdateAsync(GameBook book, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(book);
        _db.GameBooks.Update(book);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<GameBook>> ListByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ids);
        // Materialize once to keep the IN-clause stable and avoid double enumeration.
        var idList = ids as IList<Guid> ?? ids.ToList();
        if (idList.Count == 0) return Array.Empty<GameBook>();

        return await _db.GameBooks
            .Where(b => idList.Contains(b.Id))
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }
}
