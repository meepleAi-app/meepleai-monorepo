using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Application.Commands;

/// <summary>
/// #4164 (ADR-095): il seed del catalogo modelli aggiorna anche un catalogo già popolato.
/// </summary>
/// <remarks>
/// L'handler precedente usciva su una tabella non vuota (<c>if (hasModels) return;</c>). Effetto
/// osservato sul DB locale il 2026-10-10: il catalogo ha le sei righe del primo seed e non ha
/// <c>deepseek-chat</c>, il modello di tutte le chiamate registrate in <c>llm_cost_logs</c>.
/// Questi test non compilano contro quell'handler (usano <c>Models</c>), quindi non c'è un «rosso
/// prima» eseguito: il difetto è documentato dal dato.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SystemConfiguration")]
public sealed class SeedAiModelsCommandHandlerTests
{
    private readonly Mock<IAiModelConfigurationRepository> _repository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly List<AiModelConfiguration> _added = [];
    private readonly List<AiModelConfiguration> _updated = [];

    public SeedAiModelsCommandHandlerTests()
    {
        _repository.Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<AiModelConfiguration>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<AiModelConfiguration>, CancellationToken>((models, _) => _added.AddRange(models))
            .Returns(Task.CompletedTask);
        _repository.Setup(r => r.UpdateAsync(It.IsAny<AiModelConfiguration>(), It.IsAny<CancellationToken>()))
            .Callback<AiModelConfiguration, CancellationToken>((model, _) => _updated.Add(model))
            .Returns(Task.CompletedTask);
    }

    private Task SeedAsync(params AiModelConfiguration[] catalog)
    {
        _repository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(catalog);
        var handler = new SeedAiModelsCommandHandler(
            _repository.Object, _unitOfWork.Object, NullLogger<SeedAiModelsCommandHandler>.Instance);
        return handler.Handle(new SeedAiModelsCommand(), CancellationToken.None);
    }

    /// <summary>Le righe che il seed precedente a #4164 scriveva, con i suoi prezzi.</summary>
    private static AiModelConfiguration[] Pre4164Catalog() =>
    [
        Row("meta-llama/llama-3.3-70b-instruct:free", "OpenRouter", 1, ModelPricing.Free, isActive: true, isPrimary: true),
        Row("meta-llama/llama-3.3-70b-instruct", "OpenRouter", 2, new ModelPricing(0.10m, 0.32m), isActive: true),
        Row("google/gemini-2.0-flash-exp:free", "OpenRouter", 3, ModelPricing.Free, isActive: true),
        Row("deepseek/deepseek-chat", "OpenRouter", 4, new ModelPricing(0.19m, 0.87m), isActive: true),
        Row("llama3:8b", "Ollama", 5, ModelPricing.Free, isActive: false),
        Row("mistral", "Ollama", 6, ModelPricing.Free, isActive: false)
    ];

    private static AiModelConfiguration Row(
        string modelId, string provider, int priority, ModelPricing pricing, bool isActive, bool isPrimary = false) =>
        AiModelConfiguration.Create(modelId, modelId, provider, priority,
            new ModelSettings(4096, 0.7m, pricing), isActive, isPrimary);

    [Fact]
    public async Task EmptyCatalog_GetsEveryModel()
    {
        await SeedAsync();

        _added.Select(m => m.ModelId).Should().BeEquivalentTo(SeedAiModelsCommandHandler.Models.Select(m => m.ModelId));
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PopulatedCatalog_GetsTheMissingModelsAndTheVerifiedPrices()
    {
        var catalog = Pre4164Catalog();

        await SeedAsync(catalog);

        // deepseek-chat è il nome richiesto; deepseek-flash quello che DeepSeek restituisce in
        // streaming (#4164 fetta 1b). Il registro li deve prezzare entrambi.
        _added.Select(m => m.ModelId).Should().BeEquivalentTo("deepseek-chat", "deepseek-flash");
        foreach (var deepseek in _added)
        {
            deepseek.Provider.Should().Be("DeepSeek");
            deepseek.IsActive.Should().BeFalse("una riga attiva in più cambierebbe l'ordine di fallback dei provider");
            deepseek.Settings.Pricing.VerifiedOn.Should().Be(SeedAiModelsCommandHandler.PricesVerifiedOn);
        }

        // Le altre righe aggiornate sono le dismesse (#4179), coperte dai test qui sotto.
        _updated.Select(m => m.ModelId).Should().Contain(["meta-llama/llama-3.3-70b-instruct", "deepseek/deepseek-chat"]);
        var llama = catalog.Single(m => m.ModelId == "meta-llama/llama-3.3-70b-instruct");
        llama.Settings.Pricing.InputPricePerMillion.Should().Be(0.22m);
        llama.Settings.Pricing.OutputPricePerMillion.Should().Be(0.50m);
        llama.Settings.Pricing.CacheReadPricePerMillion.Should().Be(0.11m);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PopulatedCatalog_KeepsTheAdminChoices()
    {
        var catalog = Pre4164Catalog();
        var llama = catalog.Single(m => m.ModelId == "meta-llama/llama-3.3-70b-instruct");
        llama.SetActive(false);
        llama.SetPrimary(true);
        llama.UpdatePriority(9);

        await SeedAsync(catalog);

        llama.IsActive.Should().BeFalse();
        llama.IsPrimary.Should().BeTrue();
        llama.Priority.Should().Be(9);
    }

    [Fact]
    public async Task APriceTheAdminEnteredAfterTheVerification_IsNotOverwritten()
    {
        // Senza le righe dismesse: il loro ritiro aggiorna il sostituto (#4179), e qui conta il prezzo.
        var catalog = Pre4164Catalog()
            .Where(m => SeedAiModelsCommandHandler.RetiredModels.All(r => r.ModelId != m.ModelId))
            .ToArray();
        var llama = catalog.Single(m => m.ModelId == "meta-llama/llama-3.3-70b-instruct");
        llama.UpdatePricing(0.99m, 0.99m); // verifica dell'admin, datata oggi

        await SeedAsync(catalog);

        llama.Settings.Pricing.InputPricePerMillion.Should().Be(0.99m);
        _updated.Should().NotContain(llama);
    }

    [Fact]
    public async Task UpToDateCatalog_SavesNothing()
    {
        var upToDate = SeedAiModelsCommandHandler.Models
            .Select(m => Row(m.ModelId, m.Provider, m.Priority, m.Pricing, m.IsActive, m.IsPrimary))
            .ToArray();

        await SeedAsync(upToDate);

        _added.Should().BeEmpty();
        _updated.Should().BeEmpty();
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// #4179: le due varianti «:free» sono sparite dal listino di OpenRouter. Il seed le disattiva
    /// e il ruolo di primario di OpenRouter passa al sostituto: senza, il modello di fallback di
    /// OpenRouter (il primario attivo) resterebbe un modello che il provider non elenca più.
    /// </summary>
    [Fact]
    public async Task RetiredModels_AreDeactivated_AndThePrimaryRolePassesToTheReplacement()
    {
        var catalog = Pre4164Catalog();

        await SeedAsync(catalog);

        foreach (var retired in SeedAiModelsCommandHandler.RetiredModels)
        {
            var row = catalog.Single(m => m.ModelId == retired.ModelId);
            row.IsActive.Should().BeFalse($"{retired.ModelId} non è più nel listino del provider");
            row.IsPrimary.Should().BeFalse();
            _updated.Should().Contain(row);
        }

        catalog.Single(m => m.ModelId == "meta-llama/llama-3.3-70b-instruct").IsPrimary.Should().BeTrue();
        catalog.Where(m => m.Provider == "OpenRouter" && m.IsPrimary).Should().ContainSingle();
    }

    [Fact]
    public async Task RetiredModels_AlreadyInactive_AreLeftAlone()
    {
        var catalog = SeedAiModelsCommandHandler.Models
            .Select(m => Row(m.ModelId, m.Provider, m.Priority, m.Pricing, m.IsActive, m.IsPrimary))
            .Append(Row("meta-llama/llama-3.3-70b-instruct:free", "OpenRouter", 1, ModelPricing.Free, isActive: false))
            .ToArray();

        await SeedAsync(catalog);

        _updated.Should().BeEmpty();
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ARetiredPrimary_DoesNotPromoteAReplacementTheAdminDeactivated()
    {
        var catalog = Pre4164Catalog();
        var llama = catalog.Single(m => m.ModelId == "meta-llama/llama-3.3-70b-instruct");
        llama.SetActive(false);

        await SeedAsync(catalog);

        llama.IsPrimary.Should().BeFalse("un sostituto disattivato dall'admin resta una sua scelta");
        catalog.Single(m => m.ModelId == "meta-llama/llama-3.3-70b-instruct:free").IsActive.Should().BeFalse();
    }

    [Fact]
    public void NoSeedModel_IsRetired()
    {
        SeedAiModelsCommandHandler.Models.Select(m => m.ModelId)
            .Should().NotIntersectWith(SeedAiModelsCommandHandler.RetiredModels.Select(m => m.ModelId));
        SeedAiModelsCommandHandler.Models.Select(m => m.ModelId)
            .Should().Contain(SeedAiModelsCommandHandler.RetiredModels.Select(m => m.ReplacedBy).Distinct(),
                "il sostituto di un modello dismesso deve essere nel catalogo");
    }

    [Fact]
    public void EveryPaidSeedPrice_CarriesItsVerification()
    {
        foreach (var model in SeedAiModelsCommandHandler.Models.Where(m => m.Pricing.InputPricePerMillion > 0 || m.Pricing.OutputPricePerMillion > 0))
        {
            model.Pricing.VerifiedOn.Should().NotBeNull($"{model.ModelId} ha un prezzo, quindi deve dire quando è stato verificato");
            model.Pricing.Source.Should().StartWith("https://", $"{model.ModelId} deve dire dove");
        }
    }
}
