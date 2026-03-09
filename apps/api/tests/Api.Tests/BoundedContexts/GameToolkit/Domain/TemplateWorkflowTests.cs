using Api.BoundedContexts.GameToolkit.Domain.Enums;
using Api.Middleware.Exceptions;
using FluentAssertions;
using Xunit;

using GameToolkitEntity = Api.BoundedContexts.GameToolkit.Domain.Entities.GameToolkit;

namespace Api.Tests.BoundedContexts.GameToolkit.Domain;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "GameToolkit")]
public class TemplateWorkflowTests
{
    private static GameToolkitEntity CreateToolkit() =>
        new GameToolkitEntity(Guid.NewGuid(), Guid.NewGuid(), "Test Toolkit", Guid.NewGuid());

    [Fact]
    public void SubmitForReview_FromDraft_SetsStatusToPendingReview()
    {
        var toolkit = CreateToolkit();

        toolkit.SubmitForReview();

        toolkit.TemplateStatus.Should().Be(TemplateStatus.PendingReview);
        toolkit.IsTemplate.Should().BeTrue();
    }

    [Fact]
    public void SubmitForReview_FromRejected_SetsStatusToPendingReview()
    {
        var toolkit = CreateToolkit();
        var adminId = Guid.NewGuid();
        toolkit.SubmitForReview();
        toolkit.RejectTemplate(adminId, "Needs improvement");

        toolkit.SubmitForReview();

        toolkit.TemplateStatus.Should().Be(TemplateStatus.PendingReview);
    }

    [Fact]
    public void SubmitForReview_FromApproved_ThrowsConflictException()
    {
        var toolkit = CreateToolkit();
        var adminId = Guid.NewGuid();
        toolkit.SubmitForReview();
        toolkit.ApproveTemplate(adminId);

        var act = () => toolkit.SubmitForReview();

        act.Should().Throw<ConflictException>();
    }

    [Fact]
    public void SubmitForReview_FromPendingReview_ThrowsConflictException()
    {
        var toolkit = CreateToolkit();
        toolkit.SubmitForReview();

        var act = () => toolkit.SubmitForReview();

        act.Should().Throw<ConflictException>();
    }

    [Fact]
    public void ApproveTemplate_FromPendingReview_SetsStatusToApproved()
    {
        var toolkit = CreateToolkit();
        var adminId = Guid.NewGuid();
        toolkit.SubmitForReview();

        toolkit.ApproveTemplate(adminId, "Looks great");

        toolkit.TemplateStatus.Should().Be(TemplateStatus.Approved);
        toolkit.ReviewedByUserId.Should().Be(adminId);
        toolkit.ReviewedAt.Should().NotBeNull();
        toolkit.ReviewNotes.Should().Be("Looks great");
    }

    [Fact]
    public void ApproveTemplate_FromDraft_ThrowsConflictException()
    {
        var toolkit = CreateToolkit();

        var act = () => toolkit.ApproveTemplate(Guid.NewGuid());

        act.Should().Throw<ConflictException>();
    }

    [Fact]
    public void RejectTemplate_FromPendingReview_SetsStatusToRejected()
    {
        var toolkit = CreateToolkit();
        var adminId = Guid.NewGuid();
        toolkit.SubmitForReview();

        toolkit.RejectTemplate(adminId, "Missing dice configuration");

        toolkit.TemplateStatus.Should().Be(TemplateStatus.Rejected);
        toolkit.ReviewedByUserId.Should().Be(adminId);
        toolkit.ReviewedAt.Should().NotBeNull();
        toolkit.ReviewNotes.Should().Be("Missing dice configuration");
    }

    [Fact]
    public void RejectTemplate_FromDraft_ThrowsConflictException()
    {
        var toolkit = CreateToolkit();

        var act = () => toolkit.RejectTemplate(Guid.NewGuid(), "Some reason");

        act.Should().Throw<ConflictException>();
    }

    [Fact]
    public void RejectTemplate_WithEmptyNotes_ThrowsArgumentException()
    {
        var toolkit = CreateToolkit();
        toolkit.SubmitForReview();

        var act = () => toolkit.RejectTemplate(Guid.NewGuid(), "");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkAsTemplate_SetsIsTemplateToTrue()
    {
        var toolkit = CreateToolkit();

        toolkit.MarkAsTemplate();

        toolkit.IsTemplate.Should().BeTrue();
    }
}
