using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Evaluation;
using Api.Routing;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Routing;

/// <summary>
/// #4256: il prompt alternativo arriva dal corpo della richiesta. Un campo che non arriva al comando non dà
/// errore: lo scenario gira con il prompt predefinito, e la misura dice una cosa per un'altra.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4256")]
public sealed class AdminSessionAssistantEndpointsTests
{
    private const string Body = """
        {
          "scenarios": { "version": 3, "scenarios": [] },
          "primaryModelId": "deepseek-flash",
          "prompts": { "english": "ENGLISH TEST PROMPT" }
        }
        """;

    [Fact]
    public void Request_ReadsThePromptOverride_AsTheScriptSendsIt()
    {
        var request = JsonSerializer.Deserialize<AdminSessionAssistantEndpoints.RunSessionAssistantEvaluationRequest>(
            Body, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        request.Prompts.Should().Be(new AssistantPromptOverride(Italian: null, English: "ENGLISH TEST PROMPT"));
    }

    [Fact]
    public void Request_ToCommand_CarriesThePromptOverride_AndAppliesTheDefaults()
    {
        var request = JsonSerializer.Deserialize<AdminSessionAssistantEndpoints.RunSessionAssistantEvaluationRequest>(
            Body, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        var command = request.ToCommand();

        command.Prompts.Should().Be(new AssistantPromptOverride(Italian: null, English: "ENGLISH TEST PROMPT"));
        command.PrimaryModelId.Should().Be("deepseek-flash");
        command.Temperature.Should().Be(0.2);
        command.MaxTokens.Should().Be(600);
        command.MaxToolIterations.Should().Be(4);
    }
}
