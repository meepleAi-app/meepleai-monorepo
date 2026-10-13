using System.Reflection;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Domain.Enums;
using Api.SharedKernel.Domain.Exceptions;
using Api.Tests.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>Spec 2026-10-11 §3.2: la sezione dell'assistente dentro il contenuto di una versione del profilo.</summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4275")]
public sealed class AgentProfileContentSessionAssistantTests
{
    [Fact]
    public void Content_WithoutTheSection_HasTheAssistantDisabled()
    {
        AgentProfileTests.Content().SessionAssistant.Should().Be(SessionAssistantSettings.Disabled);
    }

    [Fact]
    public void Content_KeepsTheSectionItWasGiven()
    {
        var assistant = AgentProfileTests.Assistant();

        AgentProfileTests.Content(assistant: assistant).SessionAssistant.Should().Be(assistant);
    }

    [Fact]
    public void TwoContents_DifferingOnlyInTheAssistant_AreNotEqual()
    {
        var plain = AgentProfileTests.Content();
        var withAssistant = AgentProfileTests.Content(assistant: AgentProfileTests.Assistant());

        withAssistant.Should().NotBe(plain);
        withAssistant.Should().Be(AgentProfileTests.Content(assistant: AgentProfileTests.Assistant()));
    }

    [Fact]
    public void EnabledAssistant_WithoutAPromptForTheFallbackLanguage_IsRejected()
    {
        // Il contenuto di prova ha «en» come lingua di riserva: un assistente con il solo prompt italiano
        // lascerebbe senza prompt una sessione in una lingua non prevista.
        var onlyItalian = SessionAssistantSettings.Create(
            true, new Dictionary<string, string>(StringComparer.Ordinal) { ["it"] = "Sei l'assistente al tavolo." });

        var act = () => AgentProfileTests.Content(assistant: onlyItalian);

        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void DisabledAssistant_WithoutAPromptForTheFallbackLanguage_IsAllowed()
    {
        var disabled = SessionAssistantSettings.Create(
            false, new Dictionary<string, string>(StringComparer.Ordinal) { ["it"] = "Sei l'assistente al tavolo." });

        var content = AgentProfileTests.Content(assistant: disabled);

        content.SessionAssistant.Enabled.Should().BeFalse();
    }

    /// <summary>
    /// Review finale di A2: la sezione è un argomento obbligatorio. Con un parametro opzionale, chi
    /// ricostruisce un contenuto campo per campo (lo farà la pagina admin del profilo) compilerebbe senza
    /// passarla, e pubblicherebbe una versione con l'assistente spento senza che nessuno l'abbia deciso.
    /// </summary>
    [Theory]
    [InlineData("Create")]
    [InlineData("Restore")]
    public void TheFactories_TakeTheAssistantSection_AsARequiredArgument(string factory)
    {
        var parameters = typeof(AgentProfileContent)
            .GetMethod(factory, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!
            .GetParameters();

        parameters.Should().NotContain(p => p.IsOptional);
        parameters[^1].ParameterType.Should().Be(typeof(SessionAssistantSettings));
    }
}
