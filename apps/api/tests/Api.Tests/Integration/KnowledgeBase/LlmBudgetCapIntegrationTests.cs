using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.LlmManagement;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Services;
using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.Services;
using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Infrastructure.Persistence;
using Api.Configuration;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.Services.LlmClients;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using LlmUserContext = Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects.LlmUserContext;
using RagStrategy = Api.BoundedContexts.KnowledgeBase.Domain.Enums.RagStrategy;

namespace Api.Tests.Integration.KnowledgeBase;

/// <summary>
/// #4166 (ADR-095 D6, «Prova del vincolo»): con il consumo del giorno già al tetto, la chiamata
/// successiva <b>non</b> raggiunge il provider. Registro costi, catalogo e configurazione sono quelli
/// veri, su Postgres; solo il client del provider è un doppio, perché è lui che non va chiamato.
/// </summary>
/// <remarks>
/// Rosso prima: senza il gate il gateway chiamava il provider qualunque fosse la spesa. Visto
/// fallire per perturbazione, con un gate che ammette tutto (vedi la PR).
/// </remarks>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4166")]
public sealed class LlmBudgetCapIntegrationTests : IAsyncLifetime
{
    private const string CatalogModel = "deepseek-flash";

    private readonly SharedTestcontainersFixture _fixture;
    private readonly Mock<ILlmClient> _provider = new();
    private string _databaseName = null!;
    private ServiceProvider _services = null!;

    public LlmBudgetCapIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _provider.Setup(c => c.ProviderName).Returns("DeepSeek");
        _provider.Setup(c => c.SupportsModel(It.IsAny<string>())).Returns(true);
        _provider
            .Setup(c => c.GenerateCompletionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(
                "answer",
                new LlmUsage(100, 20, 120),
                new LlmCost { InputCost = 0.00003m, OutputCost = 0.000024m, ModelId = CatalogModel, Provider = "DeepSeek" }));
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"llm_budget_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(connectionString);
        services.AddScoped<ILlmCostLogRepository, LlmCostLogRepository>();
        services.AddScoped<IAiModelConfigurationRepository, EfAiModelConfigurationRepository>();
        services.AddScoped<ILlmSystemConfigRepository, EfLlmSystemConfigRepository>();
        services.AddSingleton(Options.Create(new AiProviderSettings()));
        services.AddSingleton<ILlmSystemConfigProvider, LlmSystemConfigProvider>();
        services.AddSingleton<CatalogModelPriceList>();
        services.AddSingleton<IModelPriceList>(sp => sp.GetRequiredService<CatalogModelPriceList>());
        services.AddSingleton<ILlmCostCalculator, LlmCostCalculator>();
        services.AddSingleton<ILlmBudgetGate, LlmBudgetGate>();
        _services = services.BuildServiceProvider();

        var ct = TestContext.Current.CancellationToken;
        using var scope = _services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>().Database.MigrateAsync(ct);
        await new SeedAiModelsCommandHandler(
                scope.ServiceProvider.GetRequiredService<IAiModelConfigurationRepository>(),
                scope.ServiceProvider.GetRequiredService<IUnitOfWork>(),
                NullLogger<SeedAiModelsCommandHandler>.Instance)
            .Handle(new SeedAiModelsCommand(), ct);
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    private async Task SpentTodayAsync(decimal amount, string source = "Manual")
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        db.LlmCostLogs.Add(new LlmCostLogEntity
        {
            ModelId = CatalogModel,
            Provider = "DeepSeek",
            PromptTokens = 1000,
            CompletionTokens = 500,
            TotalTokens = 1500,
            TotalCost = amount,
            Success = true,
            RequestSource = source,
            RequestDate = DateOnly.FromDateTime(DateTime.UtcNow),
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private HybridLlmService Gateway(string routedModel = CatalogModel)
    {
        var selector = new Mock<ILlmProviderSelector>();
        selector
            .Setup(s => s.SelectProviderAsync(It.IsAny<LlmUserContext>(), It.IsAny<RagStrategy>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderSelectionResult(_provider.Object, new LlmRoutingDecision("DeepSeek", routedModel, "test")));
        selector
            .Setup(s => s.GetNextFallbackAsync(It.IsAny<string>(), It.IsAny<HashSet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProviderSelectionResult.NoProvider("none"));

        return new HybridLlmService(
            [_provider.Object], selector.Object, new Mock<ICircuitBreakerRegistry>().Object, new Mock<ILlmCostService>().Object,
            NullLogger<HybridLlmService>.Instance, budgetGate: _services.GetRequiredService<ILlmBudgetGate>());
    }

    private void ProviderCalled(Times times) =>
        _provider.Verify(c => c.GenerateCompletionAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<double>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), times);

    [Fact]
    public async Task WithTodaysSpendAtTheCap_TheNextCallDoesNotReachTheProvider()
    {
        // Nessuna riga in LlmSystemConfigs: vale il tetto predefinito di 1 $ al giorno.
        await SpentTodayAsync(LlmSystemConfig.DefaultDailyBudgetUsd);

        var result = await Gateway().GenerateCompletionAsync("system", "How do I score?", RequestSource.Manual,
            TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        result.Metadata["error_code"].Should().Be(LlmBudgetAdmission.DailyBudgetErrorCode);
        ProviderCalled(Times.Never());
    }

    [Fact]
    public async Task UnderTheCap_TheCallReachesTheProvider()
    {
        await SpentTodayAsync(0.10m);

        var result = await Gateway().GenerateCompletionAsync("system", "How do I score?", RequestSource.Manual,
            TestContext.Current.CancellationToken);

        result.Success.Should().BeTrue();
        ProviderCalled(Times.Once());
    }

    [Fact]
    public async Task TheCapSavedByTheAdmin_IsTheOneApplied()
    {
        using (var scope = _services.CreateScope())
        {
            var config = LlmSystemConfig.CreateDefault();
            config.UpdateBudgetLimits(dailyBudgetUsd: 0.05m, monthlyBudgetUsd: 20m);
            await scope.ServiceProvider.GetRequiredService<ILlmSystemConfigRepository>()
                .UpsertAsync(config, TestContext.Current.CancellationToken);
        }

        await SpentTodayAsync(0.10m);

        var result = await Gateway().GenerateCompletionAsync("system", "How do I score?", RequestSource.Manual,
            TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse("0,10 $ spesi superano il tetto di 0,05 $ salvato dall'admin");
        ProviderCalled(Times.Never());
    }

    /// <summary>
    /// Trovato nella verifica a runtime della 3a: un tetto di 0 salvato dall'admin su una tabella
    /// vuota finiva a 10 $. EF, con un valore predefinito di colonna, non scrive una proprietà che
    /// vale il default del tipo (0), e la colonna prende il suo default. Lo stesso per una quota a 0.
    /// </summary>
    [Fact]
    public async Task ZeroSavedByTheAdmin_StaysZero_AndStopsEveryPaidCall()
    {
        using (var scope = _services.CreateScope())
        {
            var config = LlmSystemConfig.CreateDefault();
            config.UpdateBudgetLimits(dailyBudgetUsd: 0m, monthlyBudgetUsd: 20m);
            config.UpdateBudgetPolicy(degradeThresholdPercent: 80, backgroundQuotaPercent: 0, evaluationQuotaPercent: 0);
            await scope.ServiceProvider.GetRequiredService<ILlmSystemConfigRepository>()
                .UpsertAsync(config, TestContext.Current.CancellationToken);
        }

        using (var scope = _services.CreateScope())
        {
            var saved = await scope.ServiceProvider.GetRequiredService<ILlmSystemConfigRepository>()
                .GetCurrentAsync(TestContext.Current.CancellationToken);
            saved!.DailyBudgetUsd.Should().Be(0m, "un tetto a zero è il modo di spegnere l'AI a pagamento");
            saved.BackgroundQuotaPercent.Should().Be(0);
            saved.EvaluationQuotaPercent.Should().Be(0);
        }

        var result = await Gateway().GenerateCompletionAsync("system", "How do I score?", RequestSource.Manual,
            TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        ProviderCalled(Times.Never());
    }

    [Fact]
    public async Task BackgroundOverItsQuota_IsStopped_WhileAnswersPass()
    {
        await SpentTodayAsync(0.30m, source: nameof(RequestSource.ChunkTranslation)); // quota del 30% di 1 $

        var background = await Gateway().GenerateCompletionWithModelAsync(CatalogModel, "system", "Translate this",
            RequestSource.ChunkTranslation, ct: TestContext.Current.CancellationToken);
        var answer = await Gateway().GenerateCompletionAsync("system", "How do I score?", RequestSource.Manual,
            TestContext.Current.CancellationToken);

        background.Success.Should().BeFalse();
        background.Metadata["budget_rejection"].Should().Be(nameof(LlmBudgetRejection.DailyQuotaReached));
        answer.Success.Should().BeTrue("il background non consuma il budget delle risposte");
        ProviderCalled(Times.Once());
    }

    [Fact]
    public async Task AModelOutsideTheCatalog_IsRefused_EvenWithBudgetLeft()
    {
        var result = await Gateway(routedModel: "a-model-the-catalog-does-not-know")
            .GenerateCompletionAsync("system", "How do I score?", RequestSource.Manual, TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        result.Metadata["error_code"].Should().Be(LlmBudgetAdmission.ModelErrorCode);
        ProviderCalled(Times.Never());
    }
}
