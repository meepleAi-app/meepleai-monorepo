using Api.BoundedContexts.SystemConfiguration.Application.Commands.UpdateLlmSystemConfig;
using Api.BoundedContexts.SystemConfiguration.Application.Services;
using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Application.Commands;

/// <summary>
/// Unit tests for UpdateLlmSystemConfigCommandHandler (Issue #5495).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SystemConfiguration")]
[Trait("Issue", "5495")]
public sealed class UpdateLlmSystemConfigCommandHandlerTests
{
    private readonly Mock<ILlmSystemConfigRepository> _repoMock = new();
    private readonly Mock<ILlmSystemConfigProvider> _providerMock = new();
    private readonly Mock<ILogger<UpdateLlmSystemConfigCommandHandler>> _loggerMock = new();

    private UpdateLlmSystemConfigCommandHandler CreateHandler()
        => new(_repoMock.Object, _providerMock.Object, _loggerMock.Object);

    [Fact]
    public async Task Handle_NoExistingConfig_CreatesDefaultAndUpdates()
    {
        _repoMock.Setup(r => r.GetCurrentAsync(It.IsAny<CancellationToken>())).ReturnsAsync((LlmSystemConfig?)null);

        var adminId = Guid.NewGuid();
        var command = new UpdateLlmSystemConfigCommand(
            CircuitBreakerFailureThreshold: 8,
            CircuitBreakerOpenDurationSeconds: 45,
            CircuitBreakerSuccessThreshold: 4,
            DailyBudgetUsd: 20.00m,
            MonthlyBudgetUsd: 200.00m,
            FallbackChainJson: "[\"OpenRouter\"]",
            UpdatedByUserId: adminId);

        var handler = CreateHandler();
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(8, result.CircuitBreakerFailureThreshold);
        Assert.Equal(45, result.CircuitBreakerOpenDurationSeconds);
        Assert.Equal(20.00m, result.DailyBudgetUsd);
        Assert.Equal("database", result.Source);
        Assert.Equal(adminId, result.LastUpdatedByUserId);

        _repoMock.Verify(r => r.UpsertAsync(It.IsAny<LlmSystemConfig>(), It.IsAny<CancellationToken>()), Times.Once);
        _providerMock.Verify(p => p.InvalidateCache(), Times.Once);
    }

    [Fact]
    public async Task Handle_ExistingConfig_UpdatesAndInvalidatesCache()
    {
        var existing = LlmSystemConfig.CreateDefault();
        _repoMock.Setup(r => r.GetCurrentAsync(It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var command = new UpdateLlmSystemConfigCommand(
            CircuitBreakerFailureThreshold: 10,
            CircuitBreakerOpenDurationSeconds: 60,
            CircuitBreakerSuccessThreshold: 5,
            DailyBudgetUsd: 50.00m,
            MonthlyBudgetUsd: 500.00m,
            FallbackChainJson: "[\"Ollama\"]",
            UpdatedByUserId: Guid.NewGuid());

        var handler = CreateHandler();
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(10, result.CircuitBreakerFailureThreshold);
        Assert.Equal(50.00m, result.DailyBudgetUsd);
        _providerMock.Verify(p => p.InvalidateCache(), Times.Once);
    }
}
