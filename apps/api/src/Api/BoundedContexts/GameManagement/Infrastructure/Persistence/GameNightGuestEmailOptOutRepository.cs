using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IGameNightGuestEmailOptOutRepository"/> (#4846). The list is only ever
/// asked «is this address on it?» and added to, so there is no mapping back to the domain type.
/// </summary>
internal sealed class GameNightGuestEmailOptOutRepository : IGameNightGuestEmailOptOutRepository
{
    private readonly MeepleAiDbContext _dbContext;

    public GameNightGuestEmailOptOutRepository(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public Task<bool> ExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var normalized = GameNightGuestEmailOptOut.Normalize(email);
        return _dbContext.GameNightGuestEmailOptOuts
            .AsNoTracking()
            .AnyAsync(o => o.Email == normalized, cancellationToken);
    }

    public Task AddAsync(GameNightGuestEmailOptOut optOut, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(optOut);

        _dbContext.GameNightGuestEmailOptOuts.Add(new GameNightGuestEmailOptOutEntity
        {
            Id = optOut.Id,
            Email = optOut.Email,
            OptedOutAt = optOut.OptedOutAt,
            SourceInvitationId = optOut.SourceInvitationId,
        });
        return Task.CompletedTask;
    }
}
