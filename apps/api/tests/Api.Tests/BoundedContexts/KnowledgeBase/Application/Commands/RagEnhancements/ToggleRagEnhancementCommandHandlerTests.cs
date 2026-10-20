using Api.BoundedContexts.KnowledgeBase.Application.Commands.RagEnhancements;
using Api.Services;
using Api.SharedKernel.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Commands.RagEnhancements;

/// <summary>
/// #4594: the handler reads the state of the flag, writes the opposite one, and passes the admin id as author.
/// The HTTP behavior (status codes, rows, author saved) is covered by RagEnhancementAdminEndpointsIntegrationTests.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4594")]
public sealed class ToggleRagEnhancementCommandHandlerTests
{
    private const string Key = "rag.enhancement.crag-evaluation";

    private readonly Mock<IFeatureFlagService> _flags = new(MockBehavior.Strict);
    private readonly Guid _adminId = Guid.NewGuid();
    private readonly ToggleRagEnhancementCommandHandler _handler;

    public ToggleRagEnhancementCommandHandlerTests()
    {
        _handler = new ToggleRagEnhancementCommandHandler(_flags.Object);
    }

    [Fact]
    public async Task Global_WhenOff_TurnsItOn_WithTheAdminAsAuthor()
    {
        _flags.Setup(f => f.IsEnabledAsync(Key, null)).ReturnsAsync(false);
        _flags.Setup(f => f.EnableFeatureAsync(Key, null, _adminId.ToString())).Returns(Task.CompletedTask);

        var isNowEnabled = await _handler.Handle(new ToggleRagEnhancementCommand(Key, _adminId), CancellationToken.None);

        isNowEnabled.Should().BeTrue();
        _flags.Verify(f => f.IsEnabledAsync(Key, null), Times.Once);
        _flags.Verify(f => f.EnableFeatureAsync(Key, null, _adminId.ToString()), Times.Once);
        _flags.VerifyNoOtherCalls(); // the read and the write are the only calls
    }

    [Fact]
    public async Task Global_WhenOn_TurnsItOff_WithTheAdminAsAuthor()
    {
        _flags.Setup(f => f.IsEnabledAsync(Key, null)).ReturnsAsync(true);
        _flags.Setup(f => f.DisableFeatureAsync(Key, null, _adminId.ToString())).Returns(Task.CompletedTask);

        var isNowEnabled = await _handler.Handle(new ToggleRagEnhancementCommand(Key, _adminId), CancellationToken.None);

        isNowEnabled.Should().BeFalse();
        _flags.Verify(f => f.IsEnabledAsync(Key, null), Times.Once);
        _flags.Verify(f => f.DisableFeatureAsync(Key, null, _adminId.ToString()), Times.Once);
        _flags.VerifyNoOtherCalls(); // the read and the write are the only calls
    }

    [Fact]
    public async Task Tier_WhenOff_TurnsItOn_WithTheAdminAsAuthor()
    {
        var tier = UserTier.Premium;
        _flags.Setup(f => f.IsEnabledForTierAsync(Key, tier)).ReturnsAsync(false);
        _flags.Setup(f => f.EnableFeatureForTierAsync(Key, tier, _adminId.ToString())).Returns(Task.CompletedTask);

        var isNowEnabled = await _handler.Handle(new ToggleRagEnhancementCommand(Key, _adminId, tier), CancellationToken.None);

        isNowEnabled.Should().BeTrue();
        _flags.Verify(f => f.IsEnabledForTierAsync(Key, tier), Times.Once);
        _flags.Verify(f => f.EnableFeatureForTierAsync(Key, tier, _adminId.ToString()), Times.Once);
        _flags.VerifyNoOtherCalls(); // the read and the write are the only calls
    }

    [Fact]
    public async Task Tier_WhenOn_TurnsItOff_WithTheAdminAsAuthor()
    {
        var tier = UserTier.Free;
        _flags.Setup(f => f.IsEnabledForTierAsync(Key, tier)).ReturnsAsync(true);
        _flags.Setup(f => f.DisableFeatureForTierAsync(Key, tier, _adminId.ToString())).Returns(Task.CompletedTask);

        var isNowEnabled = await _handler.Handle(new ToggleRagEnhancementCommand(Key, _adminId, tier), CancellationToken.None);

        isNowEnabled.Should().BeFalse();
        _flags.Verify(f => f.IsEnabledForTierAsync(Key, tier), Times.Once);
        _flags.Verify(f => f.DisableFeatureForTierAsync(Key, tier, _adminId.ToString()), Times.Once);
        _flags.VerifyNoOtherCalls(); // the read and the write are the only calls
    }
}
