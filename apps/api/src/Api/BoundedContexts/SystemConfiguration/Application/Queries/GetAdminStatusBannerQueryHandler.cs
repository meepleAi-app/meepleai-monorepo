using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Queries;

internal sealed class GetAdminStatusBannerQueryHandler
    : IQueryHandler<GetAdminStatusBannerQuery, AdminStatusBannerResponse>
{
    private readonly IIncidentBannerRepository _repository;

    public GetAdminStatusBannerQueryHandler(IIncidentBannerRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<AdminStatusBannerResponse> Handle(
        GetAdminStatusBannerQuery request,
        CancellationToken cancellationToken)
    {
        var state = await _repository.GetAsync(cancellationToken).ConfigureAwait(false);

        return new AdminStatusBannerResponse(
            Message: state.Message,
            Severity: state.Severity.ToString(),
            IsActive: state.IsActive,
            StartsAt: state.StartsAt,
            EndsAt: state.EndsAt,
            UpdatedAt: state.UpdatedAt,
            UpdatedBy: state.UpdatedBy);
    }
}
