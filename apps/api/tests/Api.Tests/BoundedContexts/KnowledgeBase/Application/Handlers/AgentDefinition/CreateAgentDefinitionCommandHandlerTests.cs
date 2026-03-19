using Api.BoundedContexts.KnowledgeBase.Application.Commands.AgentDefinition;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs.AgentDefinition;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.AgentDefinition;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using AgentDefinitionEntity = Api.BoundedContexts.KnowledgeBase.Domain.Entities.AgentDefinition;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers.AgentDefinition;

/// <summary>
/// Unit tests for CreateAgentDefinitionCommandHandler (Issue #3808)
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class CreateAgentDefinitionCommandHandlerTests
{
    private readonly Mock<IAgentDefinitionRepository> _mockRepository;
    private readonly Mock<ILogger<CreateAgentDefinitionCommandHandler>> _mockLogger;
    private readonly CreateAgentDefinitionCommandHandler _handler;

    public CreateAgentDefinitionCommandHandlerTests()
    {
        _mockRepository = new Mock<IAgentDefinitionRepository>();
        _mockLogger = new Mock<ILogger<CreateAgentDefinitionCommandHandler>>();
        _handler = new CreateAgentDefinitionCommandHandler(_mockRepository.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WithValidData_ShouldCreateAgentDefinition()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var command = new CreateAgentDefinitionCommand(
            Name: "TestAgent",
            Description: "Test description",
            Type: "RAG",
            Model: "gpt-4",
            MaxTokens: 2048,
            Temperature: 0.7f);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("TestAgent");
        result.Config.Model.Should().Be("gpt-4");
        _mockRepository.Verify(r => r.AddAsync(It.IsAny<AgentDefinitionEntity>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithExistingName_ShouldThrowInvalidOperationException()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = new CreateAgentDefinitionCommand(
            Name: "ExistingAgent",
            Description: "Desc",
            Type: "RAG",
            Model: "gpt-4",
            MaxTokens: 2048,
            Temperature: 0.7f);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<Api.Middleware.Exceptions.ConflictException>().WithMessage("*already exists*");
    }

    [Fact]
    public async Task Handle_WithPromptsAndTools_ShouldIncludeInResult()
    {
        // Arrange
        _mockRepository.Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var command = new CreateAgentDefinitionCommand(
            Name: "AgentWithTools",
            Description: "Desc",
            Type: "RAG",
            Model: "gpt-4",
            MaxTokens: 2048,
            Temperature: 0.7f,
            Prompts: new List<PromptTemplateDto>
            {
                new() { Role = "system", Content = "System prompt" }
            },
            Tools: new List<ToolConfigDto>
            {
                new() { Name = "web_search", Settings = new Dictionary<string, object>() }
            });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Prompts.Should().HaveCount(1);
        result.Tools.Should().HaveCount(1);
    }
}
