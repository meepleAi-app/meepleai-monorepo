using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Queries.AccessRequest;

internal record GetAccessRequestByIdQuery(Guid Id) : IQuery<AccessRequestDto>;

internal class GetAccessRequestByIdQueryHandler : IQueryHandler<GetAccessRequestByIdQuery, AccessRequestDto>
{
    private readonly IAccessRequestRepository _repository;

    public GetAccessRequestByIdQueryHandler(IAccessRequestRepository repository)
    {
        _repository = repository;
    }

    public async Task<AccessRequestDto> Handle(
        GetAccessRequestByIdQuery request, CancellationToken cancellationToken)
    {
        var accessRequest = await _repository.GetByIdAsync(request.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Access request '{request.Id}' not found.");

        return new AccessRequestDto(
            accessRequest.Id,
            accessRequest.Email,
            accessRequest.Status.ToString(),
            accessRequest.RequestedAt,
            accessRequest.ReviewedAt,
            accessRequest.ReviewedBy,
            accessRequest.RejectionReason,
            accessRequest.InvitationId);
    }
}
