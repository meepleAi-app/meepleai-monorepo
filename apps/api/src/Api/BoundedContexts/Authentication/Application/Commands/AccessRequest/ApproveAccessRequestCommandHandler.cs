using Api.BoundedContexts.Authentication.Domain.Enums;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;

namespace Api.BoundedContexts.Authentication.Application.Commands.AccessRequest;

internal class ApproveAccessRequestCommandHandler : ICommandHandler<ApproveAccessRequestCommand, Unit>
{
    private readonly IAccessRequestRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ApproveAccessRequestCommandHandler(IAccessRequestRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(ApproveAccessRequestCommand request, CancellationToken cancellationToken)
    {
        var accessRequest = await _repository.GetByIdAsync(request.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Access request '{request.Id}' not found.");

        if (accessRequest.Status == AccessRequestStatus.Approved)
            return Unit.Value; // Idempotent no-op

        try
        {
            accessRequest.Approve(request.AdminId);
        }
        catch (InvalidOperationException)
        {
            throw new ConflictException(
                $"Cannot approve access request in '{accessRequest.Status}' status.");
        }

        await _repository.UpdateAsync(accessRequest, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}
