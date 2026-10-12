using System.Reflection;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.SessionAssistant;
using Api.Services;
using Api.Services.LlmClients;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Architecture;

/// <summary>Spec 2026-10-11 §3.2: la facciata M.E.AI sta sopra il gateway e non tocca né client né HTTP.</summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Architecture")]
[Trait("Issue", "4239")]
public sealed class SessionAssistantArchitectureTests
{
    private static readonly Type[] Forbidden = [typeof(ILlmClient), typeof(HttpClient), typeof(IHttpClientFactory), typeof(LlmProviderFactory)];

    [Theory]
    [InlineData(typeof(GatewayChatClient))]
    [InlineData(typeof(AssistantChatClientFactory))]
    public void FacadeTypes_DependOnlyOnTheGateway(Type type)
    {
        var dependencies = type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .SelectMany(c => c.GetParameters()).Select(p => p.ParameterType)
            .Concat(type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance).Select(f => f.FieldType))
            .ToList();

        dependencies.Should().NotContain(t => Forbidden.Contains(t), $"{type.Name} deve passare da ILlmService (ADR-097)");
        dependencies.Should().Contain(typeof(ILlmService));
    }

    [Fact]
    public void Tools_DoNotDependOnTheMediatorOrOnGameManagement()
    {
        var toolTypes = typeof(Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Tools.SessionAssistantToolset).Assembly
            .GetTypes()
            .Where(t => t.Namespace == "Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Tools")
            .ToList();

        toolTypes.Should().NotBeEmpty();
        foreach (var type in toolTypes)
        {
            var dependencies = type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .SelectMany(c => c.GetParameters()).Select(p => p.ParameterType)
                .Concat(type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance).Select(f => f.FieldType))
                .ToList();

            dependencies.Should().NotContain(typeof(MediatR.IMediator), $"{type.Name}: gli strumenti non scrivono (spec §3.2)");
            dependencies.Should().NotContain(typeof(MediatR.ISender));
            dependencies.Should().NotContain(t => t.Namespace != null && t.Namespace.StartsWith("Api.BoundedContexts.GameManagement", StringComparison.Ordinal));
        }
    }
}
