using Api.BoundedContexts.Authentication.Application.Commands.Invitation;
using Api.BoundedContexts.Authentication.Domain.Events;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Authentication.Application.EventHandlers;

internal sealed class AccessRequestApprovedEventHandler : DomainEventHandlerBase<AccessRequestApprovedEvent>
{
    private readonly IMediator _mediator;
    private readonly IAccessRequestRepository _repository;

    public AccessRequestApprovedEventHandler(
        MeepleAiDbContext dbContext,
        IMediator mediator,
        IAccessRequestRepository repository,
        ILogger<AccessRequestApprovedEventHandler> logger)
        : base(dbContext, logger)
    {
        _mediator = mediator;
        _repository = repository;
    }

    protected override async Task HandleEventAsync(
        AccessRequestApprovedEvent domainEvent, CancellationToken cancellationToken)
    {
        try
        {
            var invitationResult = await _mediator.Send(
                new SendInvitationCommand(
                    domainEvent.Email,
                    "User",
                    domainEvent.ApprovedByUserId),
                cancellationToken).ConfigureAwait(false);

            // Set correlation ID linking access request to invitation
            var accessRequest = await _repository.GetByIdAsync(
                domainEvent.AccessRequestId, cancellationToken).ConfigureAwait(false);
            if (accessRequest is not null)
            {
                accessRequest.SetInvitationId(invitationResult.Id);
                await _repository.UpdateAsync(accessRequest, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex,
                "Failed to create invitation for approved access request {AccessRequestId}, email {Email}",
                domainEvent.AccessRequestId, domainEvent.Email);
            // Approval stands. Admin can resend via invitation UI.
        }
    }

    protected override Guid? GetUserId(AccessRequestApprovedEvent domainEvent)
        => domainEvent.ApprovedByUserId;

    protected override Dictionary<string, object?>? GetAuditMetadata(
        AccessRequestApprovedEvent domainEvent)
        => new(StringComparer.Ordinal)
        {
            ["AccessRequestId"] = domainEvent.AccessRequestId,
            ["Email"] = domainEvent.Email
        };
}
