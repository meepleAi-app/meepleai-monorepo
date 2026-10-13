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

    private const string AssistantApplication = "Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant";

    /// <summary>
    /// Tutti i tipi sotto lo spazio dei nomi dell'assistente, compresi quelli generati dal compilatore per
    /// i metodi asincroni: una variabile locale che attraversa un await diventa un loro campo.
    /// </summary>
    private static List<Type> AssistantApplicationTypes() =>
        typeof(Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Tools.SessionAssistantToolset).Assembly
            .GetTypes()
            .Where(t => t.Namespace is not null && t.Namespace.StartsWith(AssistantApplication, StringComparison.Ordinal))
            .ToList();

    private static IEnumerable<Type> Mentioned(Type type)
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        var raw = type.GetConstructors(all).SelectMany(c => c.GetParameters()).Select(p => p.ParameterType)
            .Concat(type.GetFields(all).Select(f => f.FieldType))
            .Concat(type.GetMethods(all).SelectMany(m => m.GetParameters().Select(p => p.ParameterType).Append(m.ReturnType)));
        return raw.SelectMany(Unwrap);
    }

    private static IEnumerable<Type> Unwrap(Type type)
    {
        yield return type;
        if (type.HasElementType && type.GetElementType() is { } element)
        {
            foreach (var inner in Unwrap(element))
            {
                yield return inner;
            }
        }

        foreach (var argument in type.IsGenericType ? type.GetGenericArguments() : [])
        {
            foreach (var inner in Unwrap(argument))
            {
                yield return inner;
            }
        }
    }

    /// <summary>
    /// Spec §3.2: il lato applicativo dell'assistente non nomina GameManagement. Gli adattatori stanno in
    /// Infrastructure: così il turno non può arrivare a un comando di scrittura della partita.
    /// </summary>
    [Fact]
    public void AssistantApplication_DoesNotNameGameManagementTypes()
    {
        var types = AssistantApplicationTypes();

        types.Should().NotBeEmpty();
        foreach (var type in types)
        {
            Mentioned(type).Should().NotContain(
                t => t.Namespace != null && t.Namespace.StartsWith("Api.BoundedContexts.GameManagement", StringComparison.Ordinal),
                $"{type.FullName}: i tipi della sessione live passano dalle porte dell'assistente (spec §3.2)");
        }
    }

    /// <summary>Né il turno né gli strumenti ricevono il mediator: è il mezzo con cui si scrive la partita.</summary>
    [Fact]
    public void AssistantApplication_DoesNotReceiveTheMediator()
    {
        // Anche il contenitore: da un IServiceProvider il mediator si ottiene lo stesso.
        Type[] mediator =
        [
            typeof(MediatR.IMediator), typeof(MediatR.ISender), typeof(MediatR.IPublisher),
            typeof(IServiceProvider), typeof(Microsoft.Extensions.DependencyInjection.IServiceScopeFactory),
        ];

        foreach (var type in AssistantApplicationTypes())
        {
            Mentioned(type).Should().NotContain(t => mediator.Contains(t), $"{type.FullName}: niente mediator nell'assistente (spec §3.2)");
        }
    }

    /// <summary>
    /// Un tipo di GameManagement usato solo DENTRO un metodo sfugge alla riflessione. Qui si legge il testo
    /// dei sorgenti dell'assistente, saltando commenti e stringhe (<see cref="SourceScanner"/>).
    /// </summary>
    [Fact]
    public void AssistantApplicationSources_DoNotMentionGameManagementOrTheMediator()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "apps", "api", "src", "Api")))
        {
            root = root.Parent;
        }

        root.Should().NotBeNull("i sorgenti dell'API si cercano risalendo dalla cartella dei test");
        var folder = Path.Combine(
            root!.FullName, "apps", "api", "src", "Api", "BoundedContexts", "KnowledgeBase", "Application", "SessionAssistant");
        var files = Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories);

        files.Should().NotBeEmpty();
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            foreach (var forbidden in new[] { "BoundedContexts.GameManagement", "IMediator", "ISender" })
            {
                SourceScanner.FindCodeOccurrences(text, forbidden).Should().BeEmpty(
                    $"{Path.GetFileName(file)} nomina «{forbidden}» nel codice (spec §3.2)");
            }
        }
    }
}
