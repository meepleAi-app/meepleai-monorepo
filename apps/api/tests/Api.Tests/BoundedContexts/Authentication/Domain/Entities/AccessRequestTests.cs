using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Enums;
using Api.BoundedContexts.Authentication.Domain.Events;
using Xunit;
using Api.Tests.Constants;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.Authentication.Domain.Entities;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
public class AccessRequestTests
{
    [Fact]
    public void Create_WithValidEmail_ReturnsPendingAccessRequest()
    {
        var request = AccessRequest.Create("test@example.com");

        request.Id.Should().NotBe(Guid.Empty);
        request.Email.Should().Be("test@example.com");
        request.Status.Should().Be(AccessRequestStatus.Pending);
        (request.RequestedAt <= DateTime.UtcNow).Should().BeTrue();
        request.ReviewedAt.Should().BeNull();
        request.ReviewedBy.Should().BeNull();
        request.RejectionReason.Should().BeNull();
        request.InvitationId.Should().BeNull();
    }

    [Fact]
    public void Create_NormalizesEmailToLowercase()
    {
        var request = AccessRequest.Create("Test@EXAMPLE.com");
        request.Email.Should().Be("test@example.com");
    }

    [Fact]
    public void Approve_FromPending_SetsApprovedState()
    {
        var request = AccessRequest.Create("test@example.com");
        var adminId = Guid.NewGuid();

        request.Approve(adminId);

        request.Status.Should().Be(AccessRequestStatus.Approved);
        request.ReviewedAt.Should().NotBeNull();
        request.ReviewedBy.Should().Be(adminId);
    }

    [Fact]
    public void Reject_FromPending_SetsRejectedState()
    {
        var request = AccessRequest.Create("test@example.com");
        var adminId = Guid.NewGuid();

        request.Reject(adminId, "Not in beta group");

        request.Status.Should().Be(AccessRequestStatus.Rejected);
        request.ReviewedAt.Should().NotBeNull();
        request.ReviewedBy.Should().Be(adminId);
        request.RejectionReason.Should().Be("Not in beta group");
    }

    [Fact]
    public void Reject_WithoutReason_SetsNullReason()
    {
        var request = AccessRequest.Create("test@example.com");
        request.Reject(Guid.NewGuid());
        request.RejectionReason.Should().BeNull();
    }

    [Fact]
    public void Approve_WhenAlreadyApproved_ThrowsInvalidOperationException()
    {
        var request = AccessRequest.Create("test@example.com");
        request.Approve(Guid.NewGuid());

        var act = () => request.Approve(Guid.NewGuid());
act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Approve_WhenRejected_ThrowsInvalidOperationException()
    {
        var request = AccessRequest.Create("test@example.com");
        request.Reject(Guid.NewGuid());

        var act = () => request.Approve(Guid.NewGuid());
act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Reject_WhenAlreadyRejected_ThrowsInvalidOperationException()
    {
        var request = AccessRequest.Create("test@example.com");
        request.Reject(Guid.NewGuid());

        var act = () => request.Reject(Guid.NewGuid());
act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Reject_WhenApproved_ThrowsInvalidOperationException()
    {
        var request = AccessRequest.Create("test@example.com");
        request.Approve(Guid.NewGuid());

        var act = () => request.Reject(Guid.NewGuid());
act.Should().Throw<InvalidOperationException>();
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
        domainEvent.Should().NotBeNull();
        domainEvent.AccessRequestId.Should().Be(request.Id);
        domainEvent.Email.Should().Be("test@example.com");
        domainEvent.ApprovedByUserId.Should().Be(adminId);
    }

    [Fact]
    public void Create_PublishesAccessRequestCreatedEvent()
    {
        var request = AccessRequest.Create("test@example.com");

        var domainEvent = request.DomainEvents
            .OfType<AccessRequestCreatedEvent>()
            .SingleOrDefault();
        domainEvent.Should().NotBeNull();
        domainEvent.AccessRequestId.Should().Be(request.Id);
        domainEvent.Email.Should().Be("test@example.com");
    }

    [Fact]
    public void SetInvitationId_StoresCorrelationId()
    {
        var request = AccessRequest.Create("test@example.com");
        request.Approve(Guid.NewGuid());
        var invitationId = Guid.NewGuid();

        request.SetInvitationId(invitationId);

        request.InvitationId.Should().Be(invitationId);
    }

    [Fact]
    public void Reject_WithReasonExceeding500Chars_ThrowsArgumentException()
    {
        var request = AccessRequest.Create("test@example.com");
        var longReason = new string('a', 501);

        var act = () => request.Reject(Guid.NewGuid(), longReason);
act.Should().Throw<ArgumentException>();
    }
}