using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.Authentication.Domain.Events;

internal sealed class AccessRequestCreatedEvent : DomainEventBase
{
    public Guid AccessRequestId { get; }
    public string Email { get; }

    public AccessRequestCreatedEvent(Guid accessRequestId, string email)
    {
        AccessRequestId = accessRequestId;
        Email = email;
    }
}
