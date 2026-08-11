using Api.BoundedContexts.KnowledgeBase.Domain;
using Api.Services.LlmClients;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Services.LlmClients;

/// <summary>
/// Slice E (RAG answer-quality): guards against agents configured with a bare cloud-provider
/// model id (e.g. "claude-haiku-4-5-20251001") that no <see cref="Api.Services.LlmClients.ILlmClient"/>
/// can route — OpenRouter needs a "provider/model" slug, Ollama rejects cloud ids, DeepSeek only
/// takes "deepseek-*". Such an id makes GetClientForModel throw at chat time (500).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class LlmModelRoutingTests
{
    [Theory]
    // Bare (unprefixed) cloud-provider ids route to NO client.
    [InlineData("claude-haiku-4-5-20251001")]
    [InlineData("claude-3-opus")]
    [InlineData("gpt-4o")]
    [InlineData("gpt-3.5-turbo")]
    [InlineData("chatgpt-4o-latest")]
    [InlineData("gemini-1.5-pro")]
    [InlineData("grok-2")]
    [InlineData("o1-preview")]
    [InlineData("o3-mini")]
    public void IsUnroutableBareCloudId_BareCloudId_ReturnsTrue(string modelId)
    {
        LlmModelRouting.IsUnroutableBareCloudId(modelId).Should().BeTrue();
    }

    [Theory]
    // Prefixed OpenRouter slugs (routable via OpenRouter).
    [InlineData("anthropic/claude-3.5-haiku")]
    [InlineData("openai/gpt-4o-mini")]
    [InlineData("meta-llama/llama-3.3-70b-instruct")]
    // DeepSeek native ids (routable via DeepSeek).
    [InlineData("deepseek-chat")]
    [InlineData("deepseek-reasoner")]
    // Bare local Ollama ids (routable via Ollama) — incl. OpenAI's open-weight gpt-oss.
    [InlineData("llama3:8b")]
    [InlineData("mistral:latest")]
    [InlineData("qwen:7b")]
    [InlineData("gpt-oss:20b")]
    [InlineData("gpt-oss:120b")]
    // Empty/blank are not this rule's concern (NotEmpty handles them).
    [InlineData("")]
    [InlineData("   ")]
    public void IsUnroutableBareCloudId_RoutableOrBlank_ReturnsFalse(string modelId)
    {
        LlmModelRouting.IsUnroutableBareCloudId(modelId).Should().BeFalse();
    }

    [Fact]
    public void AgentDefaults_DefaultModel_IsRoutable()
    {
        // Regression guard: the seeded/default agent model must always be routable.
        LlmModelRouting.IsUnroutableBareCloudId(AgentDefaults.DefaultModel).Should().BeFalse();
    }
}
