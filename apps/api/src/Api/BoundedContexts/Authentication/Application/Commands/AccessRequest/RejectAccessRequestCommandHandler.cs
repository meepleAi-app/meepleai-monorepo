using Api.BoundedContexts.Authentication.Domain.Enums;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.Authentication.Application.Commands.AccessRequest;

internal class RejectAccessRequestCommandHandler : ICommandHandler<RejectAccessRequestCommand, Unit>
{
    private readonly IAccessRequestRepository _repository;

    public RejectAccessRequestCommandHandler(IAccessRequestRepository repository)
    {
        _repository = repository;
    }

    public async Task<Unit> Handle(RejectAccessRequestCommand request, CancellationToken cancellationToken)
    {
        var accessRequest = await _repository.GetByIdAsync(request.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Access request '{request.Id}' not found.");

        if (accessRequest.Status == AccessRequestStatus.Rejected)
            return Unit.Value; // Idempotent no-op

        try
        {
            accessRequest.Reject(request.AdminId, request.Reason);
        }
        catch (InvalidOperationException)
        {
            throw new ConflictException(
                $"Cannot reject access request in '{accessRequest.Status}' status.");
        }

        await _repository.UpdateAsync(accessRequest, cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}
