using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.Authentication.Domain.Events;

internal sealed class AccessRequestApprovedEvent : DomainEventBase
{
    public Guid AccessRequestId { get; }
    public string Email { get; }
    public Guid ApprovedByUserId { get; }

    public AccessRequestApprovedEvent(Guid accessRequestId, string email, Guid approvedByUserId)
    {
        AccessRequestId = accessRequestId;
        Email = email;
        ApprovedByUserId = approvedByUserId;
    }
}
