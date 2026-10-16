using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Confirmation;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Turn;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using FluentValidation.TestHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Confirmation;

/// <summary>Spec 2026-10-11 §5.3: il rifiuto, con le stesse verifiche di proprietario e di stato della conferma.</summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4277")]
public sealed class RejectAssistantProposalTests
{
    private static readonly Guid Session = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid Owner = Guid.Parse("00000000-0000-0000-0000-0000000000b1");
    private static readonly Guid Marco = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly Mock<IAssistantProposalRepository> _proposals = new();
    private readonly Mock<IAssistantSessionDirectory> _directory = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 12, 20, 0, 0, TimeSpan.Zero));

    public RejectAssistantProposalTests()
    {
        _directory.Setup(d => d.GetAccessAsync(Session, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssistantSessionAccess(Owner, "InProgress", true));
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    private AssistantProposal Proposal(Guid? version = null)
    {
        var proposal = AssistantProposal.ForScore(
            Session, Guid.NewGuid(), Guid.NewGuid(), version ?? Guid.NewGuid(), Marco, "Marco", "punti", 2, 5, 7, Now);
        _proposals.Setup(p => p.GetAsync(proposal.Id, It.IsAny<CancellationToken>())).ReturnsAsync(proposal);
        return proposal;
    }

    private Task<AssistantProposalDto> RejectAsync(AssistantProposal proposal, Guid? user = null, Guid? session = null) =>
        new RejectAssistantProposalCommandHandler(_proposals.Object, _directory.Object, _unitOfWork.Object, _time)
            .Handle(new RejectAssistantProposalCommand(session ?? Session, proposal.Id, user ?? Owner), Ct);

    [Fact]
    public async Task Reject_ClosesAPendingProposal()
    {
        var proposal = Proposal();

        var dto = await RejectAsync(proposal);

        proposal.Status.Should().Be(AssistantProposalStatus.Rejected);
        proposal.ResolvedBy.Should().Be(Owner);
        proposal.ResolvedAt.Should().Be(Now);
        dto.Status.Should().Be("Rejected");
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Reject_ByAnyoneButTheOwner_IsForbidden()
    {
        var proposal = Proposal();

        var act = () => RejectAsync(proposal, user: Guid.NewGuid());

        await act.Should().ThrowAsync<ForbiddenException>();
        proposal.Status.Should().Be(AssistantProposalStatus.Pending);
    }

    [Fact]
    public async Task Reject_AProposalOfAnotherSession_IsNotFound()
    {
        var other = Guid.NewGuid();
        _directory.Setup(d => d.GetAccessAsync(other, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssistantSessionAccess(Owner, "InProgress", true));
        var proposal = Proposal();

        var act = () => RejectAsync(proposal, session: other);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ASecondTapOnReject_ReturnsTheRejectedProposal()
    {
        var proposal = Proposal();
        proposal.Reject(Owner, Now);

        var dto = await RejectAsync(proposal);

        dto.Status.Should().Be("Rejected");
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Reject_AProposalBeingApplied_IsNotPending()
    {
        // Rifiutare mentre la conferma è in corso non deve annullare una scrittura già avviata.
        var proposal = Proposal();
        proposal.BeginApplying(Owner);

        var act = () => RejectAsync(proposal);

        (await act.Should().ThrowAsync<ConflictException>()).Which.ErrorCode.Should().Be("proposal_not_pending");
        proposal.Status.Should().Be(AssistantProposalStatus.Applying);
    }

    [Fact]
    public async Task Reject_ThatLosesTheRaceToAConfirm_IsNotPending()
    {
        var proposal = Proposal();
        var confirmed = AssistantProposal.ForScore(
            Session, proposal.ChatThreadId, proposal.MessageId, proposal.ProfileVersionId, Marco, "Marco", "punti", 2, 5, 7, Now);
        confirmed.BeginApplying(Owner);
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new DbUpdateConcurrencyException("xmin"));
        _proposals.Setup(p => p.GetReadOnlyAsync(proposal.Id, It.IsAny<CancellationToken>())).ReturnsAsync(confirmed);

        var act = () => RejectAsync(proposal);

        (await act.Should().ThrowAsync<ConflictException>()).Which.ErrorCode.Should().Be("proposal_not_pending");
    }

    [Fact]
    public void TheValidator_WantsTheIds()
    {
        var validator = new RejectAssistantProposalCommandValidator();

        validator.TestValidate(new RejectAssistantProposalCommand(Session, Guid.NewGuid(), Owner)).ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(new RejectAssistantProposalCommand(Session, Guid.Empty, Owner))
            .ShouldHaveValidationErrorFor(c => c.ProposalId);
    }

    // ─── #4453 (A6): il rifiuto nel tasso di accettazione (spec §7.3) ────────────────────────────

    private const string ResolvedMetric = "meepleai.session_assistant.proposals.resolved.total";

    [Fact]
    public async Task ARejectedProposal_IsCountedOnce_WithItsKind()
    {
        var version = Guid.NewGuid();
        using var resolved = new SessionAssistantMetricCapture(ResolvedMetric, version);

        await RejectAsync(Proposal(version));

        var tags = resolved.Measurements.Should().ContainSingle().Subject;
        tags["outcome"].Should().Be("rejected");
        tags["kind"].Should().Be("score");
    }

    [Fact]
    public async Task ASecondTapOnReject_IsNotCountedAgain()
    {
        var version = Guid.NewGuid();
        var proposal = Proposal(version);
        await RejectAsync(proposal);
        using var resolved = new SessionAssistantMetricCapture(ResolvedMetric, version);

        await RejectAsync(proposal);

        resolved.Measurements.Should().BeEmpty();
    }

    [Fact]
    public async Task ARejectThatLosesTheRace_IsNotCounted()
    {
        var version = Guid.NewGuid();
        using var resolved = new SessionAssistantMetricCapture(ResolvedMetric, version);
        var proposal = Proposal(version);
        var rejectedByTheOther = AssistantProposal.ForScore(
            Session, proposal.ChatThreadId, proposal.MessageId, version, Marco, "Marco", "punti", 2, 5, 7, Now);
        rejectedByTheOther.Reject(Owner, Now);
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new DbUpdateConcurrencyException("xmin"));
        _proposals.Setup(p => p.GetReadOnlyAsync(proposal.Id, It.IsAny<CancellationToken>())).ReturnsAsync(rejectedByTheOther);

        var dto = await RejectAsync(proposal);

        dto.Status.Should().Be("Rejected");
        resolved.Measurements.Should().BeEmpty("il rifiuto che ha vinto l'ha contato la richiesta che l'ha salvato");
    }
}
