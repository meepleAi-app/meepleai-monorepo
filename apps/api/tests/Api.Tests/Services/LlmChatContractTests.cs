using System.Text.Json;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Services;

/// <summary>
/// ADR-097: il contratto della chat con strumenti nel gateway. Un servizio che non la implementa
/// risponde con un fallimento esplicito, mai con una risposta vuota scambiata per un successo.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class LlmChatContractTests
{
    [Fact]
    public void CompletionResult_HasNoToolCallsByDefault()
    {
        LlmCompletionResult.CreateSuccess("text").ToolCalls.Should().BeEmpty();
        LlmCompletionResult.CreateFailure("error").ToolCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task ChatAsync_NotImplementedByAService_FailsExplicitly()
    {
        ILlmService service = new TestFailingLlmService();
        var request = new LlmChatRequest(
            [new LlmChatMessage(LlmChatRoles.User, "Marco fa 5 punti")],
            [new LlmToolDefinition("stato_partita", "Legge lo stato", JsonDocument.Parse("""{"type":"object","properties":{}}""").RootElement)],
            "deepseek-flash", null, 0.2, 600, null, RequestSource.SessionAssistant);

        var result = await service.ChatAsync(request, TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not supported");
    }
}
