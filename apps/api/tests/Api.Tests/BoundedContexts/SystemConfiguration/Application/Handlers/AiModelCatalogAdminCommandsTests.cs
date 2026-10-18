using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.Validators;
using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Application.Handlers;

/// <summary>
/// #4169 (ADR-095 D5/D8, pagina «Modelli e prezzi»): il prezzo del catalogo si cambia da solo, senza
/// rimandare priorità e stato; «predefinito del provider» è una scelta esclusiva per provider.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SystemConfiguration")]
[Trait("Issue", "4169")]
public sealed class AiModelCatalogAdminCommandsTests
{
    private readonly Mock<IAiModelConfigurationRepository> _repository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly List<AiModelConfiguration> _catalog = [];

    public AiModelCatalogAdminCommandsTests()
    {
        _repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => _catalog.Find(m => m.Id == id));
        _repository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _catalog.ToList());
    }

    private AiModelConfiguration Model(string modelId, string provider, bool isPrimary = false, bool isActive = true)
    {
        var model = AiModelConfiguration.Create(modelId, modelId, provider, priority: 1,
            new ModelSettings(4096, 0.3m, new ModelPricing(0.30m, 1.20m, "USD", 0.006m, new DateOnly(2026, 10, 10), "listino")),
            isActive, isPrimary);
        _catalog.Add(model);
        return model;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ThePrice_ChangesAlone_AsAnAdminVerificationOfToday()
    {
        var model = Model("deepseek-flash", "DeepSeek", isPrimary: true);
        model.SetActive(false);

        var dto = await new UpdateAiModelPricingCommandHandler(_repository.Object, _unitOfWork.Object)
            .Handle(new UpdateAiModelPricingCommand(model.Id, 0.27m, 1.10m), Ct);

        dto.Settings.Pricing.InputPricePerMillion.Should().Be(0.27m);
        dto.Settings.Pricing.OutputPricePerMillion.Should().Be(1.10m);
        dto.Settings.Pricing.VerifiedOn.Should().Be(DateOnly.FromDateTime(DateTime.UtcNow));
        dto.Settings.Pricing.Source.Should().Be(AiModelConfiguration.AdminPricingSource);
        dto.Settings.Pricing.CacheReadPricePerMillion.Should().Be(0.006m, "il prezzo della cache resta");
        dto.IsActive.Should().BeFalse("il prezzo non tocca lo stato");
        dto.IsPrimary.Should().BeTrue("né il ruolo di predefinito");
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ThePriceOfAMissingModel_IsNotFound()
    {
        var act = () => new UpdateAiModelPricingCommandHandler(_repository.Object, _unitOfWork.Object)
            .Handle(new UpdateAiModelPricingCommand(Guid.NewGuid(), 1m, 1m), Ct);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Theory]
    [InlineData(-0.01, 1)]
    [InlineData(1, -0.01)]
    [InlineData(1001, 1)]
    public void APriceOutOfRange_IsRefused(double input, double output)
    {
        new UpdateAiModelPricingCommandValidator()
            .Validate(new UpdateAiModelPricingCommand(Guid.NewGuid(), (decimal)input, (decimal)output))
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public void AFreePrice_IsAccepted()
    {
        new UpdateAiModelPricingCommandValidator()
            .Validate(new UpdateAiModelPricingCommand(Guid.NewGuid(), 0m, 0m))
            .IsValid.Should().BeTrue("un modello locale costa zero");
    }

    [Fact]
    public async Task TheDefaultOfAProvider_IsExclusiveWithinThatProviderOnly()
    {
        var oldDefault = Model("meta-llama/llama-3.3-70b-instruct", "OpenRouter", isPrimary: true);
        var newDefault = Model("deepseek/deepseek-chat", "OpenRouter");
        var otherProvider = Model("deepseek-flash", "DeepSeek", isPrimary: true);

        var dto = await new SetPrimaryAiModelCommandHandler(_repository.Object, _unitOfWork.Object)
            .Handle(new SetPrimaryAiModelCommand(newDefault.Id), Ct);

        dto.IsPrimary.Should().BeTrue();
        newDefault.IsPrimary.Should().BeTrue();
        oldDefault.IsPrimary.Should().BeFalse("un provider ha un solo predefinito");
        otherProvider.IsPrimary.Should().BeTrue("il predefinito di un altro provider non cambia");
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AnInactiveModel_CannotBeTheDefault()
    {
        var inactive = Model("deepseek/deepseek-chat", "OpenRouter", isActive: false);

        var act = () => new SetPrimaryAiModelCommandHandler(_repository.Object, _unitOfWork.Object)
            .Handle(new SetPrimaryAiModelCommand(inactive.Id), Ct);

        await act.Should().ThrowAsync<ConflictException>("il selettore legge solo i modelli attivi");
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void ANewModel_CanBeOfAProviderThatHasAClient()
    {
        var validator = new CreateAiModelCommandValidator();
        var settings = new ModelSettings(4096, 0.3m, new ModelPricing(0.3m, 1.2m));

        validator.Validate(new CreateAiModelCommand("deepseek-flash", "DeepSeek Flash", "DeepSeek", 1, true, false, settings))
            .IsValid.Should().BeTrue("DeepSeek ha un client e mancava dall'elenco");
        validator.Validate(new CreateAiModelCommand("gpt-x", "GPT X", "OpenAI", 1, true, false, settings))
            .IsValid.Should().BeFalse("OpenAI non ha un client: il modello non sarebbe chiamabile");
    }

    /// <summary>
    /// #4275: la capacità di tool calling la dichiara solo il seed, per i modelli misurati. Il corpo della
    /// richiesta di creazione è lo stesso tipo che finisce nel jsonb, quindi un client potrebbe mandarla a
    /// «vero»: la creazione la ignora, altrimenti basterebbe una POST per far passare l'invariante di
    /// pubblicazione dell'assistente su un modello mai misurato.
    /// </summary>
    [Fact]
    public async Task ANewModel_NeverDeclaresToolCalling_WhateverTheRequestSays()
    {
        AiModelConfiguration? created = null;
        _repository.Setup(r => r.AddAsync(It.IsAny<AiModelConfiguration>(), It.IsAny<CancellationToken>()))
            .Callback<AiModelConfiguration, CancellationToken>((model, _) => created = model)
            .Returns(Task.CompletedTask);
        var settings = new ModelSettings(4096, 0.3m, new ModelPricing(0.3m, 1.2m)) { SupportsToolCalling = true };

        var dto = await new CreateAiModelCommandHandler(_repository.Object, _unitOfWork.Object)
            .Handle(new CreateAiModelCommand("deepseek-reasoner", "DeepSeek Reasoner", "DeepSeek", 9, false, false, settings), Ct);

        created!.Settings.SupportsToolCalling.Should().BeFalse();
        dto.Settings.SupportsToolCalling.Should().BeFalse();
    }
}
