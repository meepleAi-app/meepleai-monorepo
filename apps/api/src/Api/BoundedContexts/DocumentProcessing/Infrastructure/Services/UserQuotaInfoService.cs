using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;

internal sealed class UserQuotaInfoService : IUserQuotaInfoService
{
    private readonly MeepleAiDbContext _dbContext;
    public UserQuotaInfoService(MeepleAiDbContext dbContext) => _dbContext = dbContext;

    public async Task<UserQuotaInfoDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _dbContext.Set<UserProfile>().AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new UserQuotaInfoDto(u.Id, u.Tier, u.Role))
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
}
