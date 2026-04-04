using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Domain.Enums;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.Authentication.Application.Commands.AccessRequest;

internal class BulkApproveAccessRequestsCommandHandler
    : ICommandHandler<BulkApproveAccessRequestsCommand, BulkApproveResultDto>
{
    private readonly IAccessRequestRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public BulkApproveAccessRequestsCommandHandler(IAccessRequestRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<BulkApproveResultDto> Handle(
        BulkApproveAccessRequestsCommand request, CancellationToken cancellationToken)
    {
        if (request.Ids.Count > 25)
            throw new ArgumentException("Batch size cannot exceed 25 items.", nameof(request));

        var results = new List<BulkApproveItemResult>();
        var succeeded = 0;
        var failed = 0;

        foreach (var id in request.Ids)
        {
            var accessRequest = await _repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

            if (accessRequest is null)
            {
                results.Add(new BulkApproveItemResult(id, "Failed", "Not found"));
                failed++;
                continue;
            }

            if (accessRequest.Status == AccessRequestStatus.Approved)
            {
                results.Add(new BulkApproveItemResult(id, "AlreadyApproved"));
                succeeded++;
                continue;
            }

            try
            {
                accessRequest.Approve(request.AdminId);
                await _repository.UpdateAsync(accessRequest, cancellationToken).ConfigureAwait(false);
                results.Add(new BulkApproveItemResult(id, "Approved"));
                succeeded++;
            }
            catch (InvalidOperationException ex)
            {
                results.Add(new BulkApproveItemResult(id, "Failed", ex.Message));
                failed++;
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new BulkApproveResultDto(request.Ids.Count, succeeded, failed, results);
    }
}
