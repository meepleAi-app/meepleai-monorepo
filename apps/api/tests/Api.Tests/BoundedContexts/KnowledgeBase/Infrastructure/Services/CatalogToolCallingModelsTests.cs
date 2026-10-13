using Api.BoundedContexts.KnowledgeBase.Infrastructure.Services;
using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure.Services;

/// <summary>Spec 2026-10-11 §3.2: la porta con cui KnowledgeBase legge dal catalogo i modelli con tool calling.</summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4275")]
public sealed class CatalogToolCallingModelsTests
{
    private static AiModelConfiguration Model(string id, bool tools, bool isActive)
    {
        var model = AiModelConfiguration.Create(
            id, id, "DeepSeek", priority: 1, new ModelSettings(4096, 0.7m, ModelPricing.Free), isActive: isActive);
        model.DeclareToolCalling(tools);
        return model;
    }

    [Fact]
    public async Task ReturnsTheModelsThatDeclareToolCalling_EvenWhenInactive()
    {
        // deepseek-flash è disattivato nel catalogo ed è il modello principale del profilo: la capacità è
        // una proprietà del modello, non della sua abilitazione (come il prezzo, #4164).
        var repository = new Mock<IAiModelConfigurationRepository>();
        repository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            Model("deepseek-flash", tools: true, isActive: false),
            Model("deepseek-chat", tools: false, isActive: true),
        ]);

        var models = await new CatalogToolCallingModels(repository.Object)
            .GetToolCallingModelIdsAsync(TestContext.Current.CancellationToken);

        models.Should().BeEquivalentTo(["deepseek-flash"]);
        models.Contains("DEEPSEEK-FLASH").Should().BeFalse("gli id dei modelli si confrontano in modo esatto");
    }
}
