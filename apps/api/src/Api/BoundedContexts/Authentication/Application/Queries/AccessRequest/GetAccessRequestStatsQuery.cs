using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Domain.Enums;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Queries.AccessRequest;

internal record GetAccessRequestStatsQuery : IQuery<AccessRequestStatsDto>;

internal class GetAccessRequestStatsQueryHandler : IQueryHandler<GetAccessRequestStatsQuery, AccessRequestStatsDto>
{
    private readonly IAccessRequestRepository _repository;

    public GetAccessRequestStatsQueryHandler(IAccessRequestRepository repository)
    {
        _repository = repository;
    }

    public async Task<AccessRequestStatsDto> Handle(
        GetAccessRequestStatsQuery request, CancellationToken cancellationToken)
    {
        var pending = await _repository.CountByStatusAsync(AccessRequestStatus.Pending, cancellationToken).ConfigureAwait(false);
        var approved = await _repository.CountByStatusAsync(AccessRequestStatus.Approved, cancellationToken).ConfigureAwait(false);
        var rejected = await _repository.CountByStatusAsync(AccessRequestStatus.Rejected, cancellationToken).ConfigureAwait(false);
        var total = await _repository.CountAllAsync(cancellationToken).ConfigureAwait(false);

        return new AccessRequestStatsDto(pending, approved, rejected, total);
    }
}
