using Api.BoundedContexts.Authentication.Domain.Events;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Security;
using Api.Services;
using Api.SharedKernel.Application.EventHandlers;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Authentication.Application.EventHandlers;

internal sealed class AccessRequestCreatedEventHandler : DomainEventHandlerBase<AccessRequestCreatedEvent>
{
    private readonly IAlertingService _alertingService;
    private readonly IAccessRequestRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public AccessRequestCreatedEventHandler(
        MeepleAiDbContext dbContext,
        IAlertingService alertingService,
        IAccessRequestRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<AccessRequestCreatedEventHandler> logger)
        : base(dbContext, logger)
    {
        _alertingService = alertingService;
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    protected override async Task HandleEventAsync(
        AccessRequestCreatedEvent domainEvent, CancellationToken cancellationToken)
    {
        Logger.LogInformation(
            "New access request {AccessRequestId} created for email {Email}",
            domainEvent.AccessRequestId, DataMasking.MaskEmail(domainEvent.Email));

        // Issue #1940 / iso-1 Fix 1: pre-flight idempotency check. If the AccessRequest already
        // carries this event's LastNotifiedEventId, a prior dispatch already fired the Slack
        // alert. Re-dispatch on a rolled-back/retried event MUST skip to avoid double-pinging
        // oncall — Slack has no remote dedup.
        var accessRequest = await _repository.GetByIdAsync(
            domainEvent.AccessRequestId, cancellationToken).ConfigureAwait(false);

        if (accessRequest is null)
        {
            Logger.LogWarning(
                "AccessRequest {AccessRequestId} disappeared between event raise and handler — skipping Slack alert",
                domainEvent.AccessRequestId);
            return;
        }

        if (accessRequest.LastNotifiedEventId == domainEvent.EventId)
        {
            Logger.LogDebug(
                "Skipping Slack alert for AccessRequest {AccessRequestId}: event {EventId} already notified (iso-1)",
                domainEvent.AccessRequestId, domainEvent.EventId);
            return;
        }

        try
        {
            await _alertingService.SendAlertAsync(
                "access_request",
                "info",
                $"New access request from {domainEvent.Email}",
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["email"] = domainEvent.Email,
                    ["requestId"] = domainEvent.AccessRequestId,
                    ["_slack_category"] = "access_request"
                },
                cancellationToken).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // FAIL-OPEN PATTERN: Notification failure must not block access request creation.
        // Return WITHOUT persisting the guard so the next outbox retry can attempt the
        // alert again — silently swallowing the guard write here would mark this event as
        // "notified" even though no Slack message went out.
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to send access request notification for {Email}", DataMasking.MaskEmail(domainEvent.Email));
            return;
        }
#pragma warning restore CA1031

        // Persist the guard OUTSIDE the fail-open catch: only run when SendAlertAsync
        // succeeded. If SaveChangesAsync now throws, the exception propagates to MediatR
        // and the outbox dispatcher will retry the handler. Risk: at-least-once delivery
        // may resend the Slack alert on the retry — accepted trade-off vs the alternative
        // where a DB failure after a successful Slack send silently swallows the guard,
        // which would have the same effect plus no operator signal.
        accessRequest.MarkNotified(domainEvent.EventId);
        await _repository.UpdateAsync(accessRequest, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    protected override Guid? GetUserId(AccessRequestCreatedEvent domainEvent)
        => null; // System action, no authenticated user

    protected override Dictionary<string, object?>? GetAuditMetadata(
        AccessRequestCreatedEvent domainEvent)
        => new(StringComparer.Ordinal)
        {
            ["AccessRequestId"] = domainEvent.AccessRequestId,
            ["Email"] = domainEvent.Email
        };
}
