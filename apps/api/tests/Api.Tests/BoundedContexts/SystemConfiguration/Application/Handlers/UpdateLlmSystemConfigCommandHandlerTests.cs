using Api.BoundedContexts.SystemConfiguration.Application.Commands.UpdateLlmSystemConfig;
using Api.BoundedContexts.SystemConfiguration.Application.Services;
using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Application.Handlers;

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

        result.CircuitBreakerFailureThreshold.Should().Be(8);
        result.CircuitBreakerOpenDurationSeconds.Should().Be(45);
        result.DailyBudgetUsd.Should().Be(20.00m);
        result.Source.Should().Be("database");
        result.LastUpdatedByUserId.Should().Be(adminId);

        _repoMock.Verify(r => r.UpsertAsync(It.IsAny<LlmSystemConfig>(), It.IsAny<CancellationToken>()), Times.Once);
        _providerMock.Verify(p => p.InvalidateCache(), Times.Once);
    }

    /// <summary>
    /// #4166: la pagina admin di oggi non conosce la politica di budget e non la manda. Un comando
    /// che la richiedesse fallirebbe (#4157); uno che la sostituisse la azzererebbe.
    /// </summary>
    [Fact]
    public async Task Handle_WithoutPolicyFields_KeepsTheSavedPolicy()
    {
        var existing = LlmSystemConfig.CreateDefault();
        existing.UpdateBudgetPolicy(70, 25, 15);
        _repoMock.Setup(r => r.GetCurrentAsync(It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var result = await CreateHandler().Handle(new UpdateLlmSystemConfigCommand(
            CircuitBreakerFailureThreshold: 5,
            CircuitBreakerOpenDurationSeconds: 30,
            CircuitBreakerSuccessThreshold: 3,
            DailyBudgetUsd: 2.00m,
            MonthlyBudgetUsd: 30.00m,
            FallbackChainJson: "[]",
            UpdatedByUserId: Guid.NewGuid()), CancellationToken.None);

        result.DailyBudgetUsd.Should().Be(2.00m);
        result.DegradeThresholdPercent.Should().Be(70);
        result.BackgroundQuotaPercent.Should().Be(25);
        result.EvaluationQuotaPercent.Should().Be(15);
    }

    [Fact]
    public async Task Handle_WithPolicyFields_UpdatesThem()
    {
        _repoMock.Setup(r => r.GetCurrentAsync(It.IsAny<CancellationToken>())).ReturnsAsync(LlmSystemConfig.CreateDefault());

        var result = await CreateHandler().Handle(new UpdateLlmSystemConfigCommand(
            5, 30, 3, 1.00m, 20.00m, "[]", Guid.NewGuid(),
            DegradeThresholdPercent: 90, BackgroundQuotaPercent: 10, EvaluationQuotaPercent: 5), CancellationToken.None);

        result.DegradeThresholdPercent.Should().Be(90);
        result.BackgroundQuotaPercent.Should().Be(10);
        result.EvaluationQuotaPercent.Should().Be(5);
    }

    [Fact]
    public async Task Handle_WithOneQuota_ChecksTheSumAgainstTheSavedOther()
    {
        _repoMock.Setup(r => r.GetCurrentAsync(It.IsAny<CancellationToken>())).ReturnsAsync(LlmSystemConfig.CreateDefault());

        var act = () => CreateHandler().Handle(new UpdateLlmSystemConfigCommand(
            5, 30, 3, 1.00m, 20.00m, "[]", Guid.NewGuid(), EvaluationQuotaPercent: 80), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>("80 più il 30 salvato del background supera il tetto");
        _repoMock.Verify(r => r.UpsertAsync(It.IsAny<LlmSystemConfig>(), It.IsAny<CancellationToken>()), Times.Never);
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

        result.CircuitBreakerFailureThreshold.Should().Be(10);
        result.DailyBudgetUsd.Should().Be(50.00m);
        _providerMock.Verify(p => p.InvalidateCache(), Times.Once);
    }
}
