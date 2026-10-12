using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.DependencyInjection;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Infrastructure.SessionAssistant;

/// <summary>
/// EndpointContractTests non risolve questi servizi (nessuna rotta nota li usa): la registrazione si
/// verifica qui, con ValidateOnBuild e ValidateScopes.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4239")]
public sealed class SessionAssistantRegistrationTests
{
    [Fact]
    public void AddSessionAssistant_ResolvesEveryService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped(_ => Mock.Of<ILlmService>());
        services.AddScoped(_ => Mock.Of<Api.BoundedContexts.GameManagement.Application.Services.IPlayerNameResolutionService>());
        services.AddSessionAssistant();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IAssistantChatClientFactory>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IAssistantPlayerMatcher>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Evaluation.SessionAssistantEvaluator>()
            .Should().NotBeNull();
    }
}
