using Api.BoundedContexts.Authentication.Application.Commands.Invitation;
using Api.BoundedContexts.Authentication.Domain.Events;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Security;
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
            // Issue #1940 / iso-1 Fix 2: pre-flight idempotency check. If the AccessRequest
            // already has an InvitationId, a prior dispatch already issued the SendInvitationCommand
            // (DB row + invitation email). Re-dispatch on a rolled-back/retried event MUST skip to
            // avoid duplicate InvitationToken row + duplicate invitation email.
            var accessRequest = await _repository.GetByIdAsync(
                domainEvent.AccessRequestId, cancellationToken).ConfigureAwait(false);

            if (accessRequest?.InvitationId is not null)
            {
                Logger.LogDebug(
                    "Skipping SendInvitationCommand for AccessRequest {AccessRequestId}: invitation {InvitationId} already issued (iso-1)",
                    domainEvent.AccessRequestId, accessRequest.InvitationId.Value);
                return;
            }

            var invitationResult = await _mediator.Send(
                new SendInvitationCommand(
                    domainEvent.Email,
                    "User",
                    domainEvent.ApprovedByUserId),
                cancellationToken).ConfigureAwait(false);

            // Set correlation ID linking access request to invitation.
            // Partial update (happy-path #B): write ONLY invitation_id via a direct SQL UPDATE.
            // A full UpdateAsync(aggregate) here reverted the approved status (last-writer-wins),
            // and adding a reentrant SaveChanges tripped the ConcurrencyDetector inside the outbox
            // processor's transaction. SetInvitationIdAsync executes immediately without either hazard.
            if (accessRequest is not null)
            {
                await _repository.SetInvitationIdAsync(
                    domainEvent.AccessRequestId, invitationResult.Id, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex,
                "Failed to create invitation for approved access request {AccessRequestId}, email {Email}",
                domainEvent.AccessRequestId, DataMasking.MaskEmail(domainEvent.Email));
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
