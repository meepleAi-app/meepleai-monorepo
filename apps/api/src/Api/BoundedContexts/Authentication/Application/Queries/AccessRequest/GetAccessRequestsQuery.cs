using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Domain.Enums;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Queries.AccessRequest;

public record GetAccessRequestsResponse(
    IReadOnlyList<AccessRequestDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

internal record GetAccessRequestsQuery(
    string? Status = null,
    int Page = 1,
    int PageSize = 20) : IQuery<GetAccessRequestsResponse>;

internal class GetAccessRequestsQueryHandler : IQueryHandler<GetAccessRequestsQuery, GetAccessRequestsResponse>
{
    private readonly IAccessRequestRepository _repository;

    public GetAccessRequestsQueryHandler(IAccessRequestRepository repository)
    {
        _repository = repository;
    }

    public async Task<GetAccessRequestsResponse> Handle(
        GetAccessRequestsQuery request, CancellationToken cancellationToken)
    {
        var pageSize = Math.Min(request.PageSize, 100);

        AccessRequestStatus? statusFilter = null;
        if (!string.IsNullOrEmpty(request.Status) &&
            Enum.TryParse<AccessRequestStatus>(request.Status, true, out var parsed))
        {
            statusFilter = parsed;
        }

        var items = await _repository.GetByStatusAsync(
            statusFilter, request.Page, pageSize, cancellationToken).ConfigureAwait(false);

        var totalCount = statusFilter.HasValue
            ? await _repository.CountByStatusAsync(statusFilter.Value, cancellationToken).ConfigureAwait(false)
            : await _repository.CountAllAsync(cancellationToken).ConfigureAwait(false);

        var dtos = items.Select(r => new AccessRequestDto(
            r.Id,
            r.Email,
            r.Status.ToString(),
            r.RequestedAt,
            r.ReviewedAt,
            r.ReviewedBy,
            r.RejectionReason,
            r.InvitationId)).ToList();

        return new GetAccessRequestsResponse(dtos, totalCount, request.Page, pageSize);
    }
}
