using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for requesting changes on share requests.
/// </summary>
internal sealed class RequestShareRequestChangesCommandHandler : ICommandHandler<RequestShareRequestChangesCommand, RequestShareRequestChangesResponse>
{
    private readonly IShareRequestRepository _shareRequestRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RequestShareRequestChangesCommandHandler> _logger;

    public RequestShareRequestChangesCommandHandler(
        IShareRequestRepository shareRequestRepository,
        IUnitOfWork unitOfWork,
        ILogger<RequestShareRequestChangesCommandHandler> logger)
    {
        _shareRequestRepository = shareRequestRepository ?? throw new ArgumentNullException(nameof(shareRequestRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<RequestShareRequestChangesResponse> Handle(
        RequestShareRequestChangesCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "Requesting changes on share request {ShareRequestId} by admin {AdminId}",
            command.ShareRequestId, command.AdminId);

        // 1. Get share request for update (with tracking)
        var shareRequest = await _shareRequestRepository.GetByIdForUpdateAsync(
            command.ShareRequestId,
            cancellationToken).ConfigureAwait(false);

        if (shareRequest == null)
        {
            throw new NotFoundException("ShareRequest", command.ShareRequestId.ToString());
        }

        // 2. Request changes (domain validates state and raises events)
        shareRequest.RequestChanges(command.AdminId, command.Feedback);

        // 3. Update repository
        _shareRequestRepository.Update(shareRequest);

        // 4. Persist changes
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Changes requested on share request {ShareRequestId}. User will be notified.",
            shareRequest.Id);

        // Domain event ShareRequestChangesRequestedEvent is dispatched automatically

        return new RequestShareRequestChangesResponse(
            shareRequest.Id,
            shareRequest.Status,
            shareRequest.ModifiedAt!.Value);
    }
}
