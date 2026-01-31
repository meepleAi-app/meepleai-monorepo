using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetShareRequestDetails;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for StartReviewCommand.
/// Starts the review process by acquiring an exclusive lock for the admin.
/// </summary>
internal sealed class StartReviewCommandHandler : ICommandHandler<StartReviewCommand, StartReviewResponse>
{
    private readonly IShareRequestRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StartReviewCommandHandler> _logger;
    private readonly IMediator _mediator;
    private readonly IReviewLockConfigService _configService;

    public StartReviewCommandHandler(
        IShareRequestRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<StartReviewCommandHandler> logger,
        IMediator mediator,
        IReviewLockConfigService configService)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
    }

    public async Task<StartReviewResponse> Handle(StartReviewCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "Starting review for share request: {ShareRequestId}, ReviewingAdmin: {AdminId}",
            command.ShareRequestId, command.ReviewingAdminId);

        var shareRequest = await _repository.GetByIdForUpdateAsync(
            command.ShareRequestId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("ShareRequest", command.ShareRequestId.ToString());

        // Get configured lock duration from SystemConfiguration
        var lockDurationMinutes = await _configService
            .GetDefaultLockDurationMinutesAsync(cancellationToken)
            .ConfigureAwait(false);

        // Start review with configured lock duration (domain handles state transition and validation)
        shareRequest.StartReview(command.ReviewingAdminId, lockDurationMinutes);

        _repository.Update(shareRequest);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Review started successfully for share request: {ShareRequestId}",
            command.ShareRequestId);

        // Get request details for response
        var detailsQuery = new GetShareRequestDetailsQuery(command.ShareRequestId, command.ReviewingAdminId);
        var requestDetails = await _mediator.Send(detailsQuery, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("ShareRequest", command.ShareRequestId.ToString());

        return new StartReviewResponse(
            command.ShareRequestId,
            shareRequest.ReviewLockExpiresAt ?? DateTime.UtcNow.AddMinutes(lockDurationMinutes),
            requestDetails);
    }
}
