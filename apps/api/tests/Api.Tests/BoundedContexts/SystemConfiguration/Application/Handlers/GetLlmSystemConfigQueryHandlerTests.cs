using Api.BoundedContexts.SystemConfiguration.Application.Handlers;
using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.Configuration;
using Api.Tests.Constants;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Application.Handlers;

/// <summary>
/// Unit tests for GetLlmSystemConfigQueryHandler (Issue #5495).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SystemConfiguration")]
[Trait("Issue", "5495")]
public sealed class GetLlmSystemConfigQueryHandlerTests
{
    private readonly Mock<ILlmSystemConfigRepository> _repoMock = new();
    private readonly IOptions<AiProviderSettings> _aiSettings;

    public GetLlmSystemConfigQueryHandlerTests()
    {
        _aiSettings = Options.Create(new AiProviderSettings
        {
            CircuitBreaker = new CircuitBreakerConfig
            {
                FailureThreshold = 7,
                OpenDurationSeconds = 45,
                SuccessThreshold = 4
            }
        });
    }

    [Fact]
    public async Task Handle_WithDbConfig_ReturnsDbValuesAndDatabaseSource()
    {
        var config = LlmSystemConfig.CreateDefault();
        config.UpdateCircuitBreakerSettings(10, 60, 5, Guid.NewGuid());
        config.UpdateBudgetLimits(50.00m, 500.00m);
        _repoMock.Setup(r => r.GetCurrentAsync(It.IsAny<CancellationToken>())).ReturnsAsync(config);

        var handler = new GetLlmSystemConfigQueryHandler(_repoMock.Object, _aiSettings);
        var result = await handler.Handle(new GetLlmSystemConfigQuery(), CancellationToken.None);

        Assert.Equal(10, result.CircuitBreakerFailureThreshold);
        Assert.Equal(60, result.CircuitBreakerOpenDurationSeconds);
        Assert.Equal(5, result.CircuitBreakerSuccessThreshold);
        Assert.Equal(50.00m, result.DailyBudgetUsd);
        Assert.Equal(500.00m, result.MonthlyBudgetUsd);
        Assert.Equal("database", result.Source);
        Assert.NotNull(result.LastUpdatedAt);
    }

    [Fact]
    public async Task Handle_WithoutDbConfig_ReturnsAppsettingsDefaultsAndAppsettingsSource()
    {
        _repoMock.Setup(r => r.GetCurrentAsync(It.IsAny<CancellationToken>())).ReturnsAsync((LlmSystemConfig?)null);

        var handler = new GetLlmSystemConfigQueryHandler(_repoMock.Object, _aiSettings);
        var result = await handler.Handle(new GetLlmSystemConfigQuery(), CancellationToken.None);

        Assert.Equal(7, result.CircuitBreakerFailureThreshold);
        Assert.Equal(45, result.CircuitBreakerOpenDurationSeconds);
        Assert.Equal(4, result.CircuitBreakerSuccessThreshold);
        Assert.Equal(10.00m, result.DailyBudgetUsd);
        Assert.Equal(100.00m, result.MonthlyBudgetUsd);
        Assert.Equal("appsettings", result.Source);
        Assert.Null(result.LastUpdatedAt);
        Assert.Null(result.LastUpdatedByUserId);
    }
}
