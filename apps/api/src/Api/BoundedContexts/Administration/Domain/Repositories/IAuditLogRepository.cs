using Api.BoundedContexts.Administration.Domain.Entities;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.Administration.Domain.Repositories;

internal interface IAuditLogRepository : IRepository<AuditLog, Guid>
{
    Task<IReadOnlyList<AuditLog>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditLog>> GetByResourceAsync(string resource, CancellationToken cancellationToken = default);
}
