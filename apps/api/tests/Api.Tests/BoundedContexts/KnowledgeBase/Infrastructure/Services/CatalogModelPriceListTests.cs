using Api.BoundedContexts.KnowledgeBase.Infrastructure.Services;
using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure.Services;

/// <summary>
/// #4164 (ADR-095): il listino in memoria servito dal catalogo <c>AiModelConfiguration</c>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class CatalogModelPriceListTests : IDisposable
{
    private readonly Mock<IAiModelConfigurationRepository> _repository = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero));
    private readonly ServiceProvider _services;
    private readonly CatalogModelPriceList _priceList;

    public CatalogModelPriceListTests()
    {
        _services = new ServiceCollection()
            .AddScoped(_ => _repository.Object)
            .BuildServiceProvider();
        _priceList = new CatalogModelPriceList(
            _services.GetRequiredService<IServiceScopeFactory>(),
            _time,
            NullLogger<CatalogModelPriceList>.Instance);
    }

    public void Dispose()
    {
        _priceList.Dispose();
        _services.Dispose();
    }

    private static AiModelConfiguration Model(
        string modelId, string provider, decimal input, decimal output, string currency = "USD", bool isActive = true) =>
        AiModelConfiguration.Create(
            modelId, modelId, provider, priority: 1,
            new ModelSettings(4096, 0.7m, new ModelPricing(input, output, currency)),
            isActive: isActive);

    private void CatalogContains(params AiModelConfiguration[] models) =>
        _repository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(models);

    [Fact]
    public async Task StartAsync_LoadsTheCatalog_IncludingInactiveModels()
    {
        CatalogContains(
            Model("deepseek-chat", "DeepSeek", 0.30m, 1.20m, isActive: false),
            Model("meta-llama/llama-3.3-70b-instruct", "OpenRouter", 0.22m, 0.50m));

        await _priceList.StartAsync(CancellationToken.None);

        var deepseek = _priceList.Find("deepseek-chat");
        deepseek.Should().NotBeNull("il prezzo vale anche per un modello disattivato");
        deepseek!.Provider.Should().Be("DeepSeek");
        deepseek.InputCostPer1M.Should().Be(0.30m);
        deepseek.OutputCostPer1M.Should().Be(1.20m);
        _priceList.Find("meta-llama/llama-3.3-70b-instruct")!.OutputCostPer1M.Should().Be(0.50m);
        _priceList.Find("absent").Should().BeNull();
    }

    [Fact]
    public async Task Find_IsCaseSensitive_LikeTheProviders()
    {
        CatalogContains(Model("deepseek-chat", "DeepSeek", 0.30m, 1.20m));
        await _priceList.StartAsync(CancellationToken.None);

        _priceList.Find("DeepSeek-Chat").Should().BeNull();
    }

    [Fact]
    public async Task NonUsdPrice_IsLeftOutOfTheList()
    {
        CatalogContains(Model("eur-model", "OpenRouter", 1m, 1m, currency: "EUR"));

        await _priceList.StartAsync(CancellationToken.None);

        _priceList.Find("eur-model").Should().BeNull("i costi si registrano in dollari");
    }

    [Fact]
    public async Task Find_WithinRefreshInterval_DoesNotReloadTheCatalog()
    {
        CatalogContains(Model("m", "OpenRouter", 1m, 1m));
        await _priceList.StartAsync(CancellationToken.None);

        _time.Advance(CatalogModelPriceList.RefreshInterval - TimeSpan.FromSeconds(1));
        _priceList.Find("m");
        await _priceList.PendingRefresh;

        _repository.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Find_AfterRefreshInterval_ServesTheOldSnapshotAndReloadsInBackground()
    {
        CatalogContains(Model("m", "OpenRouter", 1m, 1m));
        await _priceList.StartAsync(CancellationToken.None);

        // L'admin cambia il prezzo.
        CatalogContains(Model("m", "OpenRouter", 2m, 2m));
        _time.Advance(CatalogModelPriceList.RefreshInterval);

        _priceList.Find("m")!.InputCostPer1M.Should().Be(1m, "la ricarica avviene in background");
        await _priceList.PendingRefresh;

        _priceList.Find("m")!.InputCostPer1M.Should().Be(2m);
    }

    [Fact]
    public async Task FailedReload_KeepsThePreviousSnapshot_AndRetriesLater()
    {
        CatalogContains(Model("m", "OpenRouter", 1m, 1m));
        await _priceList.StartAsync(CancellationToken.None);

        _repository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database down"));
        _time.Advance(CatalogModelPriceList.RefreshInterval);
        _priceList.Find("m");
        await _priceList.PendingRefresh;

        _priceList.Find("m")!.InputCostPer1M.Should().Be(1m);

        // Prima dell'attesa di riprova non si ritenta.
        _priceList.Find("m");
        await _priceList.PendingRefresh;
        _repository.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));

        CatalogContains(Model("m", "OpenRouter", 3m, 3m));
        _time.Advance(CatalogModelPriceList.RetryInterval);
        _priceList.Find("m");
        await _priceList.PendingRefresh;

        _priceList.Find("m")!.InputCostPer1M.Should().Be(3m);
    }

    [Fact]
    public async Task EnsureLoadedAsync_OnAColdList_LoadsIt_AndOnAWarmOne_DoesNothing()
    {
        // Senza hosted service (come nei test di integrazione) il listino è freddo: Find
        // risponderebbe null, EnsureLoadedAsync invece attende il caricamento.
        CatalogContains(Model("deepseek-chat", "DeepSeek", 0.30m, 1.20m));

        await _priceList.EnsureLoadedAsync(CancellationToken.None);
        _priceList.Find("deepseek-chat")!.InputCostPer1M.Should().Be(0.30m);

        await _priceList.EnsureLoadedAsync(CancellationToken.None);
        _repository.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnsureLoadedAsync_WhenTheCatalogIsUnreachable_Throws_InsteadOfAnsweringNoPrice()
    {
        // Un listino vuoto per guasto farebbe dire all'estrattore «il modello non ha prezzo,
        // aggiungilo al catalogo» (review di #4164): l'errore deve dire il vero.
        _repository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database down"));

        var act = () => _priceList.EnsureLoadedAsync(CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .Which.Message.Should().Contain("could not be loaded");
    }

    [Fact]
    public async Task StartAsync_WhenTheCatalogIsUnreachable_DoesNotThrow_AndTheFirstFindLoadsIt()
    {
        _repository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("relation does not exist"));

        var start = () => _priceList.StartAsync(CancellationToken.None);
        await start.Should().NotThrowAsync("un catalogo irraggiungibile non deve bloccare l'avvio");
        _priceList.Find("m").Should().BeNull();

        CatalogContains(Model("m", "OpenRouter", 1m, 1m));
        _time.Advance(CatalogModelPriceList.RetryInterval);
        _priceList.Find("m");
        await _priceList.PendingRefresh;

        _priceList.Find("m")!.InputCostPer1M.Should().Be(1m);
    }
}
