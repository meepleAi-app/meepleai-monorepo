using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Application.Services;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SystemConfiguration.Infrastructure.Services;

internal sealed class UserProfileReadService : IUserProfileReadService
{
    private readonly MeepleAiDbContext _dbContext;

    public UserProfileReadService(MeepleAiDbContext dbContext) => _dbContext = dbContext;

    public async Task<UserProfileDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _dbContext.Set<UserProfile>().AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new UserProfileDto(u.Id, u.DisplayName, u.Email, u.Role, u.Tier))
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<UserProfileDto>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.ToList();
        return await _dbContext.Set<UserProfile>().AsNoTracking()
            .Where(u => idList.Contains(u.Id))
            .Select(u => new UserProfileDto(u.Id, u.DisplayName, u.Email, u.Role, u.Tier))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }
}
