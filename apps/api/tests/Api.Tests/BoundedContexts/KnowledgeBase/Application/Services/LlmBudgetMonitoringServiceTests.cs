using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Application.Services;
using Api.Models;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// ISSUE-1725 / #4166: gli allarmi sul budget LLM leggono tetti e soglia di degrado dalla stessa
/// politica salvata che il gateway applica (ADR-095 D6), non più da <c>LlmBudgetAlerts</c> in
/// appsettings, e non cambiano più i modelli.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Issue", "4166")]
public sealed class LlmBudgetMonitoringServiceTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private readonly Mock<ILlmCostLogRepository> _costLog = new();
    private readonly Mock<IAlertingService> _alerting = new();
    private readonly Mock<ILlmSystemConfigProvider> _config = new();
    private readonly List<(string Type, string Period)> _alerts = [];

    public LlmBudgetMonitoringServiceTests()
    {
        _alerting
            .Setup(a => a.SendAlertAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IDictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, IDictionary<string, object>?, CancellationToken>(
                (type, _, _, metadata, _) => _alerts.Add((type, (string)metadata!["period"])))
            .ReturnsAsync(new AlertDto(Guid.NewGuid(), "LlmBudget", "Warning", "message", null, DateTime.UtcNow, null, true, null));
    }

    private LlmBudgetMonitoringService CreateSut(IConfiguration? configuration = null)
    {
        var services = new ServiceCollection()
            .AddScoped(_ => _costLog.Object)
            .AddScoped(_ => _alerting.Object)
            .BuildServiceProvider();
        return new LlmBudgetMonitoringService(
            services.GetRequiredService<IServiceScopeFactory>(),
            configuration ?? new ConfigurationBuilder().Build(),
            _config.Object,
            NullLogger<LlmBudgetMonitoringService>.Instance);
    }

    private void Policy(decimal dailyCap, decimal monthlyCap = 20m, int degrade = 80) =>
        _config.Setup(c => c.GetBudgetPolicyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmBudgetPolicy(dailyCap, monthlyCap, degrade, 30, 20));

    /// <summary>
    /// La spesa del giorno è la query che parte da oggi; il primo del mese coincide con quella
    /// del mese, e le asserzioni guardano solo l'allarme giornaliero, con un tetto mensile ampio.
    /// </summary>
    private void SpentToday(decimal amount) =>
        _costLog.Setup(r => r.GetTotalCostAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateOnly from, DateOnly _, CancellationToken _) => from == Today ? amount : 0m);

    private Task CheckAsync(IConfiguration? configuration = null) =>
        CreateSut(configuration).CheckBudgetThresholdsAsync(TestContext.Current.CancellationToken);

    [Fact]
    public async Task UnderTheDegradeThreshold_NoAlert()
    {
        Policy(dailyCap: 1m);
        SpentToday(0.5m);

        await CheckAsync();

        _alerts.Should().BeEmpty();
    }

    [Fact]
    public async Task AtTheDegradeThreshold_AWarning()
    {
        Policy(dailyCap: 1m);
        SpentToday(0.8m);

        await CheckAsync();

        _alerts.Should().ContainSingle().Which.Should().Be(("LlmBudgetWarning", "Daily"));
    }

    [Fact]
    public async Task AtTheCap_ACriticalAlert()
    {
        Policy(dailyCap: 1m);
        SpentToday(1.0m);

        await CheckAsync();

        _alerts.Should().ContainSingle().Which.Should().Be(("LlmBudgetCritical", "Daily"));
    }

    [Fact]
    public async Task AZeroCap_IsPaidAiOff_NotAnAlert()
    {
        Policy(dailyCap: 0m);
        SpentToday(0.5m);

        await CheckAsync();

        _alerts.Should().NotContain(a => a.Period == "Daily");
    }

    /// <summary>
    /// Il caso che la fetta 3b corregge: con i 50 $ di appsettings una spesa di 0,90 $ non faceva
    /// scattare nulla, mentre il gateway, con il tetto salvato di 1 $, stava già degradando.
    /// </summary>
    [Fact]
    public async Task TheCapsComeFromTheSavedPolicy_NotFromAppsettings()
    {
        Policy(dailyCap: 1m);
        SpentToday(0.9m);
        var legacy = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["LlmBudgetAlerts:DailyBudgetUsd"] = "50.00",
            ["LlmBudgetAlerts:MonthlyBudgetUsd"] = "1000.00",
        }).Build();

        await CheckAsync(legacy);

        _alerts.Should().ContainSingle().Which.Should().Be(("LlmBudgetWarning", "Daily"));
    }
}
