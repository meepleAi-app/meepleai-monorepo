using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// Tests for the AgentConfiguration entity.
/// Issue #3025: Backend 90% Coverage Target - Phase 11
/// </summary>
[Trait("Category", "Unit")]
public sealed class AgentConfigurationTests
{
    #region Create Factory Tests

    [Fact]
    public void Create_WithValidData_ReturnsAgentConfiguration()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();

        // Act
        var config = AgentConfiguration.Create(
            agentId: agentId,
            llmProvider: LlmProvider.OpenRouter,
            llmModel: "openai/gpt-4o-mini",
            agentMode: AgentMode.Chat,
            selectedDocumentIds: null,
            temperature: 0.7m,
            maxTokens: 4096,
            createdBy: createdBy);

        // Assert
        config.Id.Should().NotBe(Guid.Empty);
        config.AgentId.Should().Be(agentId);
        config.LlmProvider.Should().Be(LlmProvider.OpenRouter);
        config.LlmModel.Should().Be("openai/gpt-4o-mini");
        config.AgentMode.Should().Be(AgentMode.Chat);
        config.Temperature.Should().Be(0.7m);
        config.MaxTokens.Should().Be(4096);
        config.IsCurrent.Should().BeFalse();
        config.CreatedBy.Should().Be(createdBy);
        config.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Create_WithPlayerMode_RequiresDocuments()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();
        var documentIds = new List<Guid> { Guid.NewGuid() };

        // Act
        var config = AgentConfiguration.Create(
            agentId: agentId,
            llmProvider: LlmProvider.OpenRouter,
            llmModel: "openai/gpt-4o-mini",
            agentMode: AgentMode.Player,
            selectedDocumentIds: documentIds,
            temperature: 0.7m,
            maxTokens: 4096,
            createdBy: createdBy);

        // Assert
        config.AgentMode.Should().Be(AgentMode.Player);
        config.SelectedDocumentIds.Should().HaveCount(1);
    }

    [Fact]
    public void Create_WithLedgerMode_RequiresDocuments()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();
        var documentIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };

        // Act
        var config = AgentConfiguration.Create(
            agentId: agentId,
            llmProvider: LlmProvider.Ollama,
            llmModel: "llama3:8b",
            agentMode: AgentMode.Ledger,
            selectedDocumentIds: documentIds,
            temperature: 0.5m,
            maxTokens: 2048,
            createdBy: createdBy);

        // Assert
        config.AgentMode.Should().Be(AgentMode.Ledger);
        config.SelectedDocumentIds.Should().HaveCount(2);
    }

    [Fact]
    public void Create_WithSystemPromptOverride_SetsOverride()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();
        var systemPrompt = "You are a helpful assistant specialized in board games.";

        // Act
        var config = AgentConfiguration.Create(
            agentId: agentId,
            llmProvider: LlmProvider.OpenRouter,
            llmModel: "openai/gpt-4o-mini",
            agentMode: AgentMode.Chat,
            selectedDocumentIds: null,
            temperature: 0.7m,
            maxTokens: 4096,
            createdBy: createdBy,
            systemPromptOverride: systemPrompt);

        // Assert
        config.SystemPromptOverride.Should().Be(systemPrompt);
    }

    [Fact]
    public void Create_TrimsLlmModel()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();

        // Act
        var config = AgentConfiguration.Create(
            agentId: agentId,
            llmProvider: LlmProvider.OpenRouter,
            llmModel: "  openai/gpt-4o-mini  ",
            agentMode: AgentMode.Chat,
            selectedDocumentIds: null,
            temperature: 0.7m,
            maxTokens: 4096,
            createdBy: createdBy);

        // Assert
        config.LlmModel.Should().Be("openai/gpt-4o-mini");
    }

    #endregion

    #region Validation Tests

    [Fact]
    public void Create_WithEmptyAgentId_ThrowsArgumentException()
    {
        // Act
        var action = () => AgentConfiguration.Create(
            agentId: Guid.Empty,
            llmProvider: LlmProvider.OpenRouter,
            llmModel: "openai/gpt-4o-mini",
            agentMode: AgentMode.Chat,
            selectedDocumentIds: null,
            temperature: 0.7m,
            maxTokens: 4096,
            createdBy: Guid.NewGuid());

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*AgentId cannot be empty*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyLlmModel_ThrowsArgumentException(string? llmModel)
    {
        // Act
        var action = () => AgentConfiguration.Create(
            agentId: Guid.NewGuid(),
            llmProvider: LlmProvider.OpenRouter,
            llmModel: llmModel!,
            agentMode: AgentMode.Chat,
            selectedDocumentIds: null,
            temperature: 0.7m,
            maxTokens: 4096,
            createdBy: Guid.NewGuid());

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*LlmModel is required*");
    }

    [Fact]
    public void Create_WithLlmModelExceeding200Characters_ThrowsArgumentException()
    {
        // Arrange
        var longModel = new string('x', 201);

        // Act
        var action = () => AgentConfiguration.Create(
            agentId: Guid.NewGuid(),
            llmProvider: LlmProvider.OpenRouter,
            llmModel: longModel,
            agentMode: AgentMode.Chat,
            selectedDocumentIds: null,
            temperature: 0.7m,
            maxTokens: 4096,
            createdBy: Guid.NewGuid());

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*LlmModel cannot exceed 200 characters*");
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(2.1)]
    public void Create_WithInvalidTemperature_ThrowsArgumentException(decimal temperature)
    {
        // Act
        var action = () => AgentConfiguration.Create(
            agentId: Guid.NewGuid(),
            llmProvider: LlmProvider.OpenRouter,
            llmModel: "openai/gpt-4o-mini",
            agentMode: AgentMode.Chat,
            selectedDocumentIds: null,
            temperature: temperature,
            maxTokens: 4096,
            createdBy: Guid.NewGuid());

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Temperature must be between 0.0 and 2.0*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(32001)]
    public void Create_WithInvalidMaxTokens_ThrowsArgumentException(int maxTokens)
    {
        // Act
        var action = () => AgentConfiguration.Create(
            agentId: Guid.NewGuid(),
            llmProvider: LlmProvider.OpenRouter,
            llmModel: "openai/gpt-4o-mini",
            agentMode: AgentMode.Chat,
            selectedDocumentIds: null,
            temperature: 0.7m,
            maxTokens: maxTokens,
            createdBy: Guid.NewGuid());

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*MaxTokens must be between 1 and 32000*");
    }

    [Fact]
    public void Create_WithSystemPromptExceeding5000Characters_ThrowsArgumentException()
    {
        // Arrange
        var longPrompt = new string('x', 5001);

        // Act
        var action = () => AgentConfiguration.Create(
            agentId: Guid.NewGuid(),
            llmProvider: LlmProvider.OpenRouter,
            llmModel: "openai/gpt-4o-mini",
            agentMode: AgentMode.Chat,
            selectedDocumentIds: null,
            temperature: 0.7m,
            maxTokens: 4096,
            createdBy: Guid.NewGuid(),
            systemPromptOverride: longPrompt);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*SystemPromptOverride cannot exceed 5000 characters*");
    }

    [Fact]
    public void Create_PlayerModeWithNoDocuments_ThrowsArgumentException()
    {
        // Act
        var action = () => AgentConfiguration.Create(
            agentId: Guid.NewGuid(),
            llmProvider: LlmProvider.OpenRouter,
            llmModel: "openai/gpt-4o-mini",
            agentMode: AgentMode.Player,
            selectedDocumentIds: new List<Guid>(),
            temperature: 0.7m,
            maxTokens: 4096,
            createdBy: Guid.NewGuid());

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Player and Ledger modes require at least one document selected*");
    }

    [Fact]
    public void Create_LedgerModeWithNoDocuments_ThrowsArgumentException()
    {
        // Act
        var action = () => AgentConfiguration.Create(
            agentId: Guid.NewGuid(),
            llmProvider: LlmProvider.OpenRouter,
            llmModel: "openai/gpt-4o-mini",
            agentMode: AgentMode.Ledger,
            selectedDocumentIds: null,
            temperature: 0.7m,
            maxTokens: 4096,
            createdBy: Guid.NewGuid());

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Player and Ledger modes require at least one document selected*");
    }

    [Fact]
    public void Create_WithEmptyCreatedBy_ThrowsArgumentException()
    {
        // Act
        var action = () => AgentConfiguration.Create(
            agentId: Guid.NewGuid(),
            llmProvider: LlmProvider.OpenRouter,
            llmModel: "openai/gpt-4o-mini",
            agentMode: AgentMode.Chat,
            selectedDocumentIds: null,
            temperature: 0.7m,
            maxTokens: 4096,
            createdBy: Guid.Empty);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*CreatedBy cannot be empty*");
    }

    #endregion

    #region SetAsCurrent Tests

    [Fact]
    public void SetAsCurrent_SetsIsCurrentToTrue()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.IsCurrent.Should().BeFalse();

        // Act
        config.SetAsCurrent();

        // Assert
        config.IsCurrent.Should().BeTrue();
    }

    #endregion

    #region Deactivate Tests

    [Fact]
    public void Deactivate_SetsIsCurrentToFalse()
    {
        // Arrange
        var config = CreateValidConfiguration();
        config.SetAsCurrent();
        config.IsCurrent.Should().BeTrue();

        // Act
        config.Deactivate();

        // Assert
        config.IsCurrent.Should().BeFalse();
    }

    #endregion

    #region UpdateLlmConfiguration Tests

    [Fact]
    public void UpdateLlmConfiguration_WithValidData_UpdatesConfiguration()
    {
        // Arrange
        var config = CreateValidConfiguration();

        // Act
        config.UpdateLlmConfiguration(
            provider: LlmProvider.Ollama,
            model: "llama3:8b",
            temperature: 0.5m,
            maxTokens: 2048);

        // Assert
        config.LlmProvider.Should().Be(LlmProvider.Ollama);
        config.LlmModel.Should().Be("llama3:8b");
        config.Temperature.Should().Be(0.5m);
        config.MaxTokens.Should().Be(2048);
    }

    [Fact]
    public void UpdateLlmConfiguration_TrimsModel()
    {
        // Arrange
        var config = CreateValidConfiguration();

        // Act
        config.UpdateLlmConfiguration(
            provider: LlmProvider.Ollama,
            model: "  llama3:8b  ",
            temperature: 0.5m,
            maxTokens: 2048);

        // Assert
        config.LlmModel.Should().Be("llama3:8b");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateLlmConfiguration_WithEmptyModel_ThrowsArgumentException(string? model)
    {
        // Arrange
        var config = CreateValidConfiguration();

        // Act
        var action = () => config.UpdateLlmConfiguration(
            provider: LlmProvider.Ollama,
            model: model!,
            temperature: 0.5m,
            maxTokens: 2048);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*LlmModel is required*");
    }

    [Fact]
    public void UpdateLlmConfiguration_WithInvalidTemperature_ThrowsArgumentException()
    {
        // Arrange
        var config = CreateValidConfiguration();

        // Act
        var action = () => config.UpdateLlmConfiguration(
            provider: LlmProvider.Ollama,
            model: "llama3:8b",
            temperature: 3.0m,
            maxTokens: 2048);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Temperature must be between 0.0 and 2.0*");
    }

    #endregion

    #region UpdateSelectedDocuments Tests

    [Fact]
    public void UpdateSelectedDocuments_WithValidDocuments_UpdatesDocuments()
    {
        // Arrange
        var config = CreateValidConfiguration();
        var newDocIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };

        // Act
        config.UpdateSelectedDocuments(newDocIds);

        // Assert
        config.SelectedDocumentIds.Should().HaveCount(2);
        config.SelectedDocumentIds.Should().BeEquivalentTo(newDocIds);
    }

    [Fact]
    public void UpdateSelectedDocuments_ClearsExistingDocuments()
    {
        // Arrange
        var documentIds = new List<Guid> { Guid.NewGuid() };
        var config = AgentConfiguration.Create(
            agentId: Guid.NewGuid(),
            llmProvider: LlmProvider.OpenRouter,
            llmModel: "openai/gpt-4o-mini",
            agentMode: AgentMode.Player,
            selectedDocumentIds: documentIds,
            temperature: 0.7m,
            maxTokens: 4096,
            createdBy: Guid.NewGuid());

        var newDocIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

        // Act
        config.UpdateSelectedDocuments(newDocIds);

        // Assert
        config.SelectedDocumentIds.Should().HaveCount(3);
    }

    [Fact]
    public void UpdateSelectedDocuments_WithNull_ThrowsArgumentNullException()
    {
        // Arrange
        var config = CreateValidConfiguration();

        // Act
        var action = () => config.UpdateSelectedDocuments(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region UpdateSystemPrompt Tests

    [Fact]
    public void UpdateSystemPrompt_WithValidPrompt_UpdatesPrompt()
    {
        // Arrange
        var config = CreateValidConfiguration();
        var newPrompt = "New system prompt";

        // Act
        config.UpdateSystemPrompt(newPrompt);

        // Assert
        config.SystemPromptOverride.Should().Be(newPrompt);
    }

    [Fact]
    public void UpdateSystemPrompt_WithNull_ClearsPrompt()
    {
        // Arrange
        var config = AgentConfiguration.Create(
            agentId: Guid.NewGuid(),
            llmProvider: LlmProvider.OpenRouter,
            llmModel: "openai/gpt-4o-mini",
            agentMode: AgentMode.Chat,
            selectedDocumentIds: null,
            temperature: 0.7m,
            maxTokens: 4096,
            createdBy: Guid.NewGuid(),
            systemPromptOverride: "Original prompt");

        // Act
        config.UpdateSystemPrompt(null);

        // Assert
        config.SystemPromptOverride.Should().BeNull();
    }

    [Fact]
    public void UpdateSystemPrompt_TrimsPrompt()
    {
        // Arrange
        var config = CreateValidConfiguration();

        // Act
        config.UpdateSystemPrompt("  New prompt  ");

        // Assert
        config.SystemPromptOverride.Should().Be("New prompt");
    }

    [Fact]
    public void UpdateSystemPrompt_WithPromptExceeding5000Characters_ThrowsArgumentException()
    {
        // Arrange
        var config = CreateValidConfiguration();
        var longPrompt = new string('x', 5001);

        // Act
        var action = () => config.UpdateSystemPrompt(longPrompt);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*SystemPromptOverride cannot exceed 5000 characters*");
    }

    #endregion

    #region Boundary Tests

    [Fact]
    public void Create_WithTemperatureAtMinimum_Succeeds()
    {
        // Act
        var config = AgentConfiguration.Create(
            agentId: Guid.NewGuid(),
            llmProvider: LlmProvider.OpenRouter,
            llmModel: "openai/gpt-4o-mini",
            agentMode: AgentMode.Chat,
            selectedDocumentIds: null,
            temperature: 0.0m,
            maxTokens: 4096,
            createdBy: Guid.NewGuid());

        // Assert
        config.Temperature.Should().Be(0.0m);
    }

    [Fact]
    public void Create_WithTemperatureAtMaximum_Succeeds()
    {
        // Act
        var config = AgentConfiguration.Create(
            agentId: Guid.NewGuid(),
            llmProvider: LlmProvider.OpenRouter,
            llmModel: "openai/gpt-4o-mini",
            agentMode: AgentMode.Chat,
            selectedDocumentIds: null,
            temperature: 2.0m,
            maxTokens: 4096,
            createdBy: Guid.NewGuid());

        // Assert
        config.Temperature.Should().Be(2.0m);
    }

    [Fact]
    public void Create_WithMaxTokensAtMinimum_Succeeds()
    {
        // Act
        var config = AgentConfiguration.Create(
            agentId: Guid.NewGuid(),
            llmProvider: LlmProvider.OpenRouter,
            llmModel: "openai/gpt-4o-mini",
            agentMode: AgentMode.Chat,
            selectedDocumentIds: null,
            temperature: 0.7m,
            maxTokens: 1,
            createdBy: Guid.NewGuid());

        // Assert
        config.MaxTokens.Should().Be(1);
    }

    [Fact]
    public void Create_WithMaxTokensAtMaximum_Succeeds()
    {
        // Act
        var config = AgentConfiguration.Create(
            agentId: Guid.NewGuid(),
            llmProvider: LlmProvider.OpenRouter,
            llmModel: "openai/gpt-4o-mini",
            agentMode: AgentMode.Chat,
            selectedDocumentIds: null,
            temperature: 0.7m,
            maxTokens: 32000,
            createdBy: Guid.NewGuid());

        // Assert
        config.MaxTokens.Should().Be(32000);
    }

    [Fact]
    public void Create_WithLlmModelAt200Characters_Succeeds()
    {
        // Arrange
        var model = new string('x', 200);

        // Act
        var config = AgentConfiguration.Create(
            agentId: Guid.NewGuid(),
            llmProvider: LlmProvider.OpenRouter,
            llmModel: model,
            agentMode: AgentMode.Chat,
            selectedDocumentIds: null,
            temperature: 0.7m,
            maxTokens: 4096,
            createdBy: Guid.NewGuid());

        // Assert
        config.LlmModel.Should().HaveLength(200);
    }

    #endregion

    #region Helper Methods

    private static AgentConfiguration CreateValidConfiguration()
    {
        return AgentConfiguration.Create(
            agentId: Guid.NewGuid(),
            llmProvider: LlmProvider.OpenRouter,
            llmModel: "openai/gpt-4o-mini",
            agentMode: AgentMode.Chat,
            selectedDocumentIds: null,
            temperature: 0.7m,
            maxTokens: 4096,
            createdBy: Guid.NewGuid());
    }

    #endregion
}
