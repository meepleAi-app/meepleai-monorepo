using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for ReleaseReviewCommand.
/// Manually releases the review lock and returns to the previous state.
/// </summary>
internal sealed class ReleaseReviewCommandHandler : ICommandHandler<ReleaseReviewCommand, Unit>
{
    private readonly IShareRequestRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ReleaseReviewCommandHandler> _logger;

    public ReleaseReviewCommandHandler(
        IShareRequestRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<ReleaseReviewCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Unit> Handle(ReleaseReviewCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "Releasing review for share request: {ShareRequestId}, ReviewingAdmin: {AdminId}",
            command.ShareRequestId, command.ReviewingAdminId);

        var shareRequest = await _repository.GetByIdForUpdateAsync(
            command.ShareRequestId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("ShareRequest", command.ShareRequestId.ToString());

        // Release review (domain validates admin ownership)
        shareRequest.ReleaseReview();

        _repository.Update(shareRequest);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Review released successfully for share request: {ShareRequestId}",
            command.ShareRequestId);

        return Unit.Value;
    }
}
