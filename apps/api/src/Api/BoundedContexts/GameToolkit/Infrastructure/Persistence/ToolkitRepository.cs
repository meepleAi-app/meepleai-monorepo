using Api.BoundedContexts.GameToolkit.Domain.Entities;
using Api.BoundedContexts.GameToolkit.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameToolkit.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of IToolkitRepository.
/// Issue #5145 — Epic B2.
/// </summary>
internal sealed class ToolkitRepository : RepositoryBase, IToolkitRepository
{

    public ToolkitRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<Toolkit?> GetActiveAsync(
        Guid gameId, Guid? userId, CancellationToken cancellationToken = default)
    {
        // User-specific override takes precedence over the default
        if (userId.HasValue)
        {
            var userToolkit = await DbContext.Toolkits
                .Include(t => t.Widgets)
                .FirstOrDefaultAsync(t => t.GameId == gameId && t.OwnerUserId == userId.Value, cancellationToken)
                .ConfigureAwait(false);

            if (userToolkit is not null)
                return userToolkit;
        }

        // Fall back to the default (game-level) toolkit
        return await DbContext.Toolkits
            .Include(t => t.Widgets)
            .FirstOrDefaultAsync(t => t.GameId == gameId && t.IsDefault, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Toolkit?> GetDefaultAsync(
        Guid gameId, CancellationToken cancellationToken = default)
    {
        return await DbContext.Toolkits
            .Include(t => t.Widgets)
            .FirstOrDefaultAsync(t => t.GameId == gameId && t.IsDefault, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(Toolkit toolkit, CancellationToken cancellationToken = default)
    {
        await DbContext.Toolkits.AddAsync(toolkit, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> ExistsDefaultAsync(
        Guid gameId, CancellationToken cancellationToken = default)
    {
        return await DbContext.Toolkits
            .AnyAsync(t => t.GameId == gameId && t.IsDefault, cancellationToken)
            .ConfigureAwait(false);
    }
}
