using Api.BoundedContexts.KnowledgeBase.Application.Commands.AgentDefinition;
using Api.BoundedContexts.KnowledgeBase.Application.Validators.AgentDefinition;
using Api.BoundedContexts.KnowledgeBase.Domain;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers.AgentDefinition;

/// <summary>
/// Slice E: the Create/Update agent-definition validators reject a bare cloud-provider model id
/// (routes to no LLM client → chat 500). See <see cref="Api.Services.LlmClients.LlmModelRouting"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class AgentDefinitionModelRoutabilityValidatorTests
{
    private readonly CreateAgentDefinitionCommandValidator _createValidator =
        new(new Mock<IVectorDocumentRepository>().Object);
    private readonly UpdateAgentDefinitionCommandValidator _updateValidator =
        new(new Mock<IVectorDocumentRepository>().Object);

    private static CreateAgentDefinitionCommand CreateWith(string model) =>
        new(Name: "A", Description: "d", Type: "RAG", Model: model, MaxTokens: 500, Temperature: 0.3f);

    private static UpdateAgentDefinitionCommand UpdateWith(string model) =>
        new(Id: Guid.NewGuid(), Name: "A", Description: "d", Type: "RAG", Model: model, MaxTokens: 500, Temperature: 0.3f);

    [Fact]
    public async Task Create_BareCloudModelId_FailsValidation()
    {
        var result = await _createValidator.TestValidateAsync(CreateWith("claude-haiku-4-5-20251001"));
        result.ShouldHaveValidationErrorFor(x => x.Model);
    }

    [Fact]
    public async Task Update_BareCloudModelId_FailsValidation()
    {
        var result = await _updateValidator.TestValidateAsync(UpdateWith("claude-haiku-4-5-20251001"));
        result.ShouldHaveValidationErrorFor(x => x.Model);
    }

    [Theory]
    [InlineData("anthropic/claude-3.5-haiku")]
    [InlineData("deepseek-chat")]
    [InlineData("gpt-oss:20b")]
    public async Task Create_RoutableModelId_PassesModelRule(string model)
    {
        var result = await _createValidator.TestValidateAsync(CreateWith(model));
        result.ShouldNotHaveValidationErrorFor(x => x.Model);
    }

    [Fact]
    public async Task Update_RoutableModelId_PassesModelRule()
    {
        var result = await _updateValidator.TestValidateAsync(UpdateWith(AgentDefaults.DefaultModel));
        result.ShouldNotHaveValidationErrorFor(x => x.Model);
    }
}
