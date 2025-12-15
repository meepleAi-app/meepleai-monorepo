using Api.BoundedContexts.Administration.Domain.Entities;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.Administration.Domain.Repositories;

internal interface IAlertRepository : IRepository<Alert, Guid>
{
    Task<IReadOnlyList<Alert>> GetActiveAlertsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Alert>> GetAlertsByTypeAsync(string alertType, CancellationToken cancellationToken = default);
}
