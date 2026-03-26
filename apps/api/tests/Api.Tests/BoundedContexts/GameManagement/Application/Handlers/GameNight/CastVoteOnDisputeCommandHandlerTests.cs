using Api.BoundedContexts.GameManagement.Application.Commands.GameNight;
using Api.BoundedContexts.GameManagement.Application.Commands.GameNight;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers.GameNight;

/// <summary>
/// Unit tests for CastVoteOnDisputeCommandHandler.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class CastVoteOnDisputeCommandHandlerTests
{
    private readonly Mock<IRuleDisputeRepository> _disputeRepositoryMock;
    private readonly Mock<IFeatureFlagService> _featureFlagServiceMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly CastVoteOnDisputeCommandHandler _handler;

    private static readonly Guid DefaultSessionId = Guid.NewGuid();
    private static readonly Guid DefaultGameId = Guid.NewGuid();
    private static readonly Guid DefaultInitiatorPlayerId = Guid.NewGuid();
    private static readonly Guid DefaultVoterPlayerId = Guid.NewGuid();

    public CastVoteOnDisputeCommandHandlerTests()
    {
        _disputeRepositoryMock = new Mock<IRuleDisputeRepository>();
        _featureFlagServiceMock = new Mock<IFeatureFlagService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _handler = new CastVoteOnDisputeCommandHandler(
            _disputeRepositoryMock.Object,
            _featureFlagServiceMock.Object,
            _unitOfWorkMock.Object);
    }

    // === Helpers ===

    private static RuleDispute CreateDisputeWithVerdict()
    {
        var dispute = RuleDispute.Open(
            DefaultSessionId, DefaultGameId, DefaultInitiatorPlayerId, "Test claim");

        dispute.SetVerdict(new DisputeVerdict(
            RulingFor.Initiator,
            "The rule clearly states one move per turn.",
            "Rulebook p.12",
            VerdictConfidence.High));

        return dispute;
    }

    private void SetupFeatureFlagEnabled(bool enabled = true)
    {
        _featureFlagServiceMock
            .Setup(x => x.IsEnabledAsync("Features:Arbitro.DemocraticOverride", null))
            .ReturnsAsync(enabled);
    }

    private void SetupDisputeGetById(Guid disputeId, RuleDispute? dispute)
    {
        _disputeRepositoryMock
            .Setup(x => x.GetByIdAsync(disputeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dispute);
    }

    // =============================================
    // CastVoteOnDisputeCommand tests
    // =============================================

    [Fact]
    public async Task CastVote_ValidCommand_CastsVoteSuccessfully()
    {
        // Arrange
        var dispute = CreateDisputeWithVerdict();
        var disputeId = dispute.Id;

        SetupFeatureFlagEnabled();
        SetupDisputeGetById(disputeId, dispute);

        var command = new CastVoteOnDisputeCommand(
            disputeId,
            DefaultVoterPlayerId,
            AcceptsVerdict: true);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        dispute.Votes.Should().ContainSingle();
        dispute.Votes[0].PlayerId.Should().Be(DefaultVoterPlayerId);
        (dispute.Votes[0].AcceptsVerdict).Should().BeTrue();

        _disputeRepositoryMock.Verify(
            x => x.UpdateAsync(dispute, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CastVote_FeatureDisabled_ThrowsInvalidOperationException()
    {
        // Arrange
        SetupFeatureFlagEnabled(false);

        var command = new CastVoteOnDisputeCommand(
            Guid.NewGuid(),
            DefaultVoterPlayerId,
            AcceptsVerdict: true);

        // Act & Assert
        var act =
            () => _handler.Handle(command, CancellationToken.None);
        var ex = (await act.Should().ThrowAsync<InvalidOperationException>()).Which;

        ex.Message.Should().Contain("disabled");
    }

    [Fact]
    public async Task CastVote_DisputeNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var disputeId = Guid.NewGuid();
        SetupFeatureFlagEnabled();
        SetupDisputeGetById(disputeId, null);

        var command = new CastVoteOnDisputeCommand(
            disputeId,
            DefaultVoterPlayerId,
            AcceptsVerdict: true);

        // Act & Assert
        var act =
            () => _handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CastVote_DuplicateVote_ThrowsInvalidOperationException()
    {
        // Arrange
        var dispute = CreateDisputeWithVerdict();
        var disputeId = dispute.Id;

        // Cast first vote directly on domain
        dispute.CastVote(DefaultVoterPlayerId, true);

        SetupFeatureFlagEnabled();
        SetupDisputeGetById(disputeId, dispute);

        var command = new CastVoteOnDisputeCommand(
            disputeId,
            DefaultVoterPlayerId,
            AcceptsVerdict: false);

        // Act & Assert
        var act =
            () => _handler.Handle(command, CancellationToken.None);
        var ex = (await act.Should().ThrowAsync<InvalidOperationException>()).Which;

        ex.Message.Should().Contain("already voted");
    }
}
