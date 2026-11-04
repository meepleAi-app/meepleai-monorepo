using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.DependencyInjection;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
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
        services.AddScoped(_ => Mock.Of<Api.BoundedContexts.SystemConfiguration.Domain.Repositories.IAiModelConfigurationRepository>());
        services.AddScoped(_ => Mock.Of<MediatR.IMediator>());
        services.AddScoped(_ => Mock.Of<Api.BoundedContexts.KnowledgeBase.Application.Services.Answering.IPublishedAgentProfileReader>());
        services.AddScoped(_ => Mock.Of<Api.BoundedContexts.KnowledgeBase.Domain.Repositories.IChatThreadRepository>());
        services.AddScoped(_ => Mock.Of<Api.BoundedContexts.KnowledgeBase.Domain.Repositories.IAssistantProposalRepository>());
        services.AddScoped(_ => Mock.Of<Api.SharedKernel.Infrastructure.Persistence.IUnitOfWork>());
        // #4411: cerca_regole usa il passo condiviso dell'accesso e la ricerca con tier di E1.
        services.AddScoped(_ => Mock.Of<Api.BoundedContexts.KnowledgeBase.Application.Services.Answering.IAnswerPreconditions>());
        services.AddScoped(_ => Mock.Of<Api.BoundedContexts.KnowledgeBase.Application.Services.Answering.ITieredPassageSearch>());
        services.AddScoped(_ => Mock.Of<Api.BoundedContexts.KnowledgeBase.Application.Services.IGroundedAnswerService>());
        // #4729: la sonda del regolamento legge i giochi del catalogo dal database.
        services.AddScoped(_ => Mock.Of<Api.SharedKernel.Application.Services.IDomainEventCollector>());
        services.AddDbContext<Api.Infrastructure.MeepleAiDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddSingleton(TimeProvider.System);
        services.AddSessionAssistant();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IAssistantChatClientFactory>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IAssistantPlayerMatcher>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Evaluation.SessionAssistantEvaluator>()
            .Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<Api.BoundedContexts.KnowledgeBase.Domain.Services.IToolCallingModelCatalog>()
            .Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IAssistantSessionDirectory>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IAssistantRulesReadiness>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Turn.AssistantTurnRunner>()
            .Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Confirmation.IAssistantProposalApplier>()
            .Should().NotBeNull();
    }
}
