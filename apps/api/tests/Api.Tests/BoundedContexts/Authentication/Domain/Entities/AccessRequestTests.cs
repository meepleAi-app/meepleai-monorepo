using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Enums;
using Api.BoundedContexts.Authentication.Domain.Events;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Domain.Entities;

public class AccessRequestTests
{
    [Fact]
    public void Create_WithValidEmail_ReturnsPendingAccessRequest()
    {
        var request = AccessRequest.Create("test@example.com");

        Assert.NotEqual(Guid.Empty, request.Id);
        Assert.Equal("test@example.com", request.Email);
        Assert.Equal(AccessRequestStatus.Pending, request.Status);
        Assert.True(request.RequestedAt <= DateTime.UtcNow);
        Assert.Null(request.ReviewedAt);
        Assert.Null(request.ReviewedBy);
        Assert.Null(request.RejectionReason);
        Assert.Null(request.InvitationId);
    }

    [Fact]
    public void Create_NormalizesEmailToLowercase()
    {
        var request = AccessRequest.Create("Test@EXAMPLE.com");
        Assert.Equal("test@example.com", request.Email);
    }

    [Fact]
    public void Approve_FromPending_SetsApprovedState()
    {
        var request = AccessRequest.Create("test@example.com");
        var adminId = Guid.NewGuid();

        request.Approve(adminId);

        Assert.Equal(AccessRequestStatus.Approved, request.Status);
        Assert.NotNull(request.ReviewedAt);
        Assert.Equal(adminId, request.ReviewedBy);
    }

    [Fact]
    public void Reject_FromPending_SetsRejectedState()
    {
        var request = AccessRequest.Create("test@example.com");
        var adminId = Guid.NewGuid();

        request.Reject(adminId, "Not in beta group");

        Assert.Equal(AccessRequestStatus.Rejected, request.Status);
        Assert.NotNull(request.ReviewedAt);
        Assert.Equal(adminId, request.ReviewedBy);
        Assert.Equal("Not in beta group", request.RejectionReason);
    }

    [Fact]
    public void Reject_WithoutReason_SetsNullReason()
    {
        var request = AccessRequest.Create("test@example.com");
        request.Reject(Guid.NewGuid());
        Assert.Null(request.RejectionReason);
    }

    [Fact]
    public void Approve_WhenAlreadyApproved_ThrowsInvalidOperationException()
    {
        var request = AccessRequest.Create("test@example.com");
        request.Approve(Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() => request.Approve(Guid.NewGuid()));
    }

    [Fact]
    public void Approve_WhenRejected_ThrowsInvalidOperationException()
    {
        var request = AccessRequest.Create("test@example.com");
        request.Reject(Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() => request.Approve(Guid.NewGuid()));
    }

    [Fact]
    public void Reject_WhenAlreadyRejected_ThrowsInvalidOperationException()
    {
        var request = AccessRequest.Create("test@example.com");
        request.Reject(Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() => request.Reject(Guid.NewGuid()));
    }

    [Fact]
    public void Reject_WhenApproved_ThrowsInvalidOperationException()
    {
        var request = AccessRequest.Create("test@example.com");
        request.Approve(Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() => request.Reject(Guid.NewGuid()));
    }

    [Fact]
    public void Approve_PublishesAccessRequestApprovedEvent()
    {
        var request = AccessRequest.Create("test@example.com");
        var adminId = Guid.NewGuid();

        request.Approve(adminId);

        var domainEvent = request.DomainEvents
            .OfType<AccessRequestApprovedEvent>()
            .SingleOrDefault();
        Assert.NotNull(domainEvent);
        Assert.Equal(request.Id, domainEvent.AccessRequestId);
        Assert.Equal("test@example.com", domainEvent.Email);
        Assert.Equal(adminId, domainEvent.ApprovedByUserId);
    }

    [Fact]
    public void Create_PublishesAccessRequestCreatedEvent()
    {
        var request = AccessRequest.Create("test@example.com");

        var domainEvent = request.DomainEvents
            .OfType<AccessRequestCreatedEvent>()
            .SingleOrDefault();
        Assert.NotNull(domainEvent);
        Assert.Equal(request.Id, domainEvent.AccessRequestId);
        Assert.Equal("test@example.com", domainEvent.Email);
    }

    [Fact]
    public void SetInvitationId_StoresCorrelationId()
    {
        var request = AccessRequest.Create("test@example.com");
        request.Approve(Guid.NewGuid());
        var invitationId = Guid.NewGuid();

        request.SetInvitationId(invitationId);

        Assert.Equal(invitationId, request.InvitationId);
    }

    [Fact]
    public void Reject_WithReasonExceeding500Chars_ThrowsArgumentException()
    {
        var request = AccessRequest.Create("test@example.com");
        var longReason = new string('a', 501);

        Assert.Throws<ArgumentException>(() => request.Reject(Guid.NewGuid(), longReason));
    }
}
