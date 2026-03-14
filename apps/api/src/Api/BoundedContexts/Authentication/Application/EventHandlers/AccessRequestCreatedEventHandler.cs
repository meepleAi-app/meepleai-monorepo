using Api.BoundedContexts.Authentication.Domain.Events;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Authentication.Application.EventHandlers;

internal sealed class AccessRequestCreatedEventHandler : DomainEventHandlerBase<AccessRequestCreatedEvent>
{
    public AccessRequestCreatedEventHandler(
        MeepleAiDbContext dbContext,
        ILogger<AccessRequestCreatedEventHandler> logger)
        : base(dbContext, logger)
    {
    }

    protected override async Task HandleEventAsync(
        AccessRequestCreatedEvent domainEvent, CancellationToken cancellationToken)
    {
        Logger.LogInformation(
            "New access request {AccessRequestId} created for email {Email}",
            domainEvent.AccessRequestId, domainEvent.Email);

        await Task.CompletedTask.ConfigureAwait(false);
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
