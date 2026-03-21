using Api.BoundedContexts.Authentication.Domain.Events;
using Api.Infrastructure;
using Api.Services;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Authentication.Application.EventHandlers;

internal sealed class AccessRequestCreatedEventHandler : DomainEventHandlerBase<AccessRequestCreatedEvent>
{
    private readonly IAlertingService _alertingService;

    public AccessRequestCreatedEventHandler(
        MeepleAiDbContext dbContext,
        IAlertingService alertingService,
        ILogger<AccessRequestCreatedEventHandler> logger)
        : base(dbContext, logger)
    {
        _alertingService = alertingService;
    }

    protected override async Task HandleEventAsync(
        AccessRequestCreatedEvent domainEvent, CancellationToken cancellationToken)
    {
        Logger.LogInformation(
            "New access request {AccessRequestId} created for email {Email}",
            domainEvent.AccessRequestId, domainEvent.Email);

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
        // FAIL-OPEN PATTERN: Notification failure must not block access request creation
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to send access request notification for {Email}", domainEvent.Email);
        }
#pragma warning restore CA1031
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
