using Api.BoundedContexts.Authentication.Domain.Enums;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.Infrastructure.Security;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Authentication.Application.Commands.AccessRequest;

internal class RejectAccessRequestCommandHandler : ICommandHandler<RejectAccessRequestCommand, Unit>
{
    private readonly IAccessRequestRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailService _emailService;
    private readonly ILogger<RejectAccessRequestCommandHandler> _logger;

    public RejectAccessRequestCommandHandler(
        IAccessRequestRepository repository,
        IUnitOfWork unitOfWork,
        IEmailService emailService,
        ILogger<RejectAccessRequestCommandHandler> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _emailService = emailService;
        _logger = logger;
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
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Best-effort rejection email — don't fail the rejection if email delivery fails
        try
        {
            await _emailService.SendAccessRequestRejectedEmailAsync(
                accessRequest.Email,
                request.Reason,
                cancellationToken).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send rejection email to {Email}", DataMasking.MaskEmail(accessRequest.Email));
        }
#pragma warning restore CA1031

        return Unit.Value;
    }
}
