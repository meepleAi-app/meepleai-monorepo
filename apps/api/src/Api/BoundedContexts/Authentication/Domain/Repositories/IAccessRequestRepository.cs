using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Enums;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.Authentication.Domain.Repositories;

internal interface IAccessRequestRepository : IRepository<AccessRequest, Guid>
{
    Task<AccessRequest?> GetPendingByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AccessRequest>> GetByStatusAsync(
        AccessRequestStatus? status, int page, int pageSize,
        CancellationToken cancellationToken = default);
    Task<int> CountByStatusAsync(AccessRequestStatus status, CancellationToken cancellationToken = default);
    Task<int> CountAllAsync(CancellationToken cancellationToken = default);
}
