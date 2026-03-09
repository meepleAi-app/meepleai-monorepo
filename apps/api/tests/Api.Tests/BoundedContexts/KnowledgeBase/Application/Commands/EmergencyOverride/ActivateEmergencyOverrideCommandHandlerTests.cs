using Api.BoundedContexts.KnowledgeBase.Application.Commands.EmergencyOverride;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Services;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Commands.EmergencyOverride;

/// <summary>
/// Unit tests for ActivateEmergencyOverrideCommandHandler and
/// DeactivateEmergencyOverrideCommandHandler (Issue #5476).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "5476")]
public sealed class ActivateEmergencyOverrideCommandHandlerTests
{
    private readonly Mock<IEmergencyOverrideService> _overrideServiceMock = new();
    private readonly Mock<ILlmService> _llmServiceMock = new();
    private readonly Mock<IFreeModelQuotaTracker> _quotaTrackerMock = new();
    private readonly Mock<ILogger<ActivateEmergencyOverrideCommandHandler>> _loggerMock = new();

    private ActivateEmergencyOverrideCommandHandler CreateHandler(
        ILlmService? llmService = null,
        IFreeModelQuotaTracker? quotaTracker = null)
    {
        return new ActivateEmergencyOverrideCommandHandler(
            _overrideServiceMock.Object,
            llmService ?? _llmServiceMock.Object,
            _loggerMock.Object,
            quotaTracker);
    }

    // ─── ActivateEmergencyOverrideCommandHandler ────────────────────────────

    [Fact]
    public async Task Handle_Activate_CallsOverrideService()
    {
        var handler = CreateHandler();
        var command = new ActivateEmergencyOverrideCommand(
            "force-ollama-only", 30, "Emergency", Guid.NewGuid());

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.Success);
        _overrideServiceMock.Verify(
            s => s.ActivateOverrideAsync(
                "force-ollama-only", 30, "Emergency",
                command.AdminUserId, null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_Activate_ReturnsSuccessMessage()
    {
        var handler = CreateHandler();
        var command = new ActivateEmergencyOverrideCommand(
            "force-ollama-only", 60, "Outage", Guid.NewGuid());

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("force-ollama-only", result.Message, StringComparison.Ordinal);
        Assert.Contains("60", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Handle_FlushQuotaCache_CallsQuotaTracker()
    {
        var handler = CreateHandler(quotaTracker: _quotaTrackerMock.Object);
        var command = new ActivateEmergencyOverrideCommand(
            "flush-quota-cache", 5, "Cache stale", Guid.NewGuid());

        await handler.Handle(command, CancellationToken.None);

        _quotaTrackerMock.Verify(
            q => q.FlushCacheAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_FlushQuotaCache_NoTracker_DoesNotThrow()
    {
        // quotaTracker is null (optional dependency)
        var handler = CreateHandler(quotaTracker: null);
        var command = new ActivateEmergencyOverrideCommand(
            "flush-quota-cache", 5, "Cache stale", Guid.NewGuid());

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task Handle_ForceOllamaOnly_NoSideEffects()
    {
        var handler = CreateHandler(quotaTracker: _quotaTrackerMock.Object);
        var command = new ActivateEmergencyOverrideCommand(
            "force-ollama-only", 30, "Routing issue", Guid.NewGuid());

        await handler.Handle(command, CancellationToken.None);

        // force-ollama-only should NOT call quota tracker or circuit breaker reset
        _quotaTrackerMock.Verify(
            q => q.FlushCacheAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ─── DeactivateEmergencyOverrideCommandHandler ──────────────────────────

    [Fact]
    public async Task Handle_Deactivate_CallsOverrideService()
    {
        var deactivateHandler = new DeactivateEmergencyOverrideCommandHandler(
            _overrideServiceMock.Object);
        var command = new DeactivateEmergencyOverrideCommand(
            "force-ollama-only", Guid.NewGuid());

        var result = await deactivateHandler.Handle(command, CancellationToken.None);

        Assert.True(result.Success);
        _overrideServiceMock.Verify(
            s => s.DeactivateOverrideAsync(
                "force-ollama-only", command.AdminUserId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_Deactivate_ReturnsSuccessMessage()
    {
        var deactivateHandler = new DeactivateEmergencyOverrideCommandHandler(
            _overrideServiceMock.Object);
        var command = new DeactivateEmergencyOverrideCommand(
            "reset-circuit-breaker", Guid.NewGuid());

        var result = await deactivateHandler.Handle(command, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("deactivated", result.Message, StringComparison.Ordinal);
    }
}
