using Api.Models;
using Api.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Api.Tests;

/// <summary>
/// AI-07.1: Unit tests for PromptTemplateService
/// Tests template loading, rendering, question classification, and fallback behavior
/// </summary>
public class PromptTemplateServiceTests
{
    private readonly Mock<ILogger<PromptTemplateService>> _mockLogger;

    public PromptTemplateServiceTests()
    {
        _mockLogger = new Mock<ILogger<PromptTemplateService>>();
    }

    #region Template Loading Tests

    [Fact]
    public async Task GetTemplateAsync_WithDefaultConfiguration_ReturnsDefaultTemplate()
    {
        // Arrange
        var config = CreateMinimalConfiguration();
        var service = CreateService(config);

        // Act
        var template = await service.GetTemplateAsync(null, QuestionType.General);

        // Assert
        Assert.NotNull(template);
        Assert.Equal(QuestionType.General, template.QuestionType);
        Assert.False(string.IsNullOrWhiteSpace(template.SystemPrompt));
        Assert.False(string.IsNullOrWhiteSpace(template.UserPromptTemplate));
    }

    [Fact]
    public async Task GetTemplateAsync_WithQuestionTypeSpecificTemplate_ReturnsCorrectTemplate()
    {
        // Arrange
        var config = CreateConfigurationWithQuestionTypes();
        var service = CreateService(config);

        // Act
        var setupTemplate = await service.GetTemplateAsync(null, QuestionType.Setup);
        var gameplayTemplate = await service.GetTemplateAsync(null, QuestionType.Gameplay);

        // Assert
        Assert.NotNull(setupTemplate);
        Assert.NotNull(gameplayTemplate);
        Assert.Equal(QuestionType.Setup, setupTemplate.QuestionType);
        Assert.Equal(QuestionType.Gameplay, gameplayTemplate.QuestionType);
        Assert.NotEqual(setupTemplate.SystemPrompt, gameplayTemplate.SystemPrompt);
    }

    [Fact]
    public async Task GetTemplateAsync_WithGameSpecificTemplate_ReturnsGameTemplate()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var config = CreateConfigurationWithGameTemplates(gameId);
        var service = CreateService(config);

        // Act
        var template = await service.GetTemplateAsync(gameId, QuestionType.General);

        // Assert
        Assert.NotNull(template);
        Assert.Equal(gameId, template.GameId);
        Assert.Contains("Chess", template.SystemPrompt); // Game-specific content
    }

    [Fact]
    public async Task GetTemplateAsync_WithMissingGameTemplate_FallsBackToDefault()
    {
        // Arrange
        var config = CreateMinimalConfiguration();
        var service = CreateService(config);
        var nonExistentGameId = Guid.NewGuid();

        // Act
        var template = await service.GetTemplateAsync(nonExistentGameId, QuestionType.General);

        // Assert
        Assert.NotNull(template);
        Assert.Null(template.GameId); // Falls back to default
    }

    [Fact]
    public async Task GetTemplateAsync_WithMissingQuestionType_FallsBackToGeneral()
    {
        // Arrange
        var config = CreateMinimalConfiguration();
        var service = CreateService(config);

        // Act
        var template = await service.GetTemplateAsync(null, QuestionType.EdgeCases);

        // Assert
        Assert.NotNull(template);
        Assert.Equal(QuestionType.General, template.QuestionType); // Falls back to General
    }

    #endregion

    #region Few-Shot Examples Tests

    [Fact]
    public async Task GetTemplateAsync_WithFewShotExamples_IncludesExamples()
    {
        // Arrange
        var config = CreateConfigurationWithFewShotExamples();
        var service = CreateService(config);

        // Act
        var template = await service.GetTemplateAsync(null, QuestionType.Setup);

        // Assert
        Assert.NotNull(template);
        Assert.NotEmpty(template.FewShotExamples);
        Assert.All(template.FewShotExamples, example =>
        {
            Assert.False(string.IsNullOrWhiteSpace(example.Question));
            Assert.False(string.IsNullOrWhiteSpace(example.Answer));
            Assert.False(string.IsNullOrWhiteSpace(example.Category));
        });
    }

    [Fact]
    public async Task GetTemplateAsync_WithEmptyFewShotExamples_ReturnsEmptyList()
    {
        // Arrange
        var config = CreateMinimalConfiguration();
        var service = CreateService(config);

        // Act
        var template = await service.GetTemplateAsync(null, QuestionType.General);

        // Assert
        Assert.NotNull(template);
        Assert.Empty(template.FewShotExamples);
    }

    #endregion

    #region Prompt Rendering Tests

    [Fact]
    public void RenderSystemPrompt_WithFewShotExamples_IncludesExamplesInPrompt()
    {
        // Arrange
        var config = CreateConfigurationWithFewShotExamples();
        var service = CreateService(config);
        var template = new PromptTemplate
        {
            SystemPrompt = "You are a board game assistant.",
            UserPromptTemplate = "Context: {context}\nQuestion: {query}",
            FewShotExamples = new List<FewShotExample>
            {
                new() { Question = "How do I set up Chess?", Answer = "Place the board...", Category = "Setup" },
                new() { Question = "How does a pawn move?", Answer = "Pawns move forward...", Category = "Gameplay" }
            }
        };

        // Act
        var renderedPrompt = service.RenderSystemPrompt(template);

        // Assert
        Assert.Contains("You are a board game assistant", renderedPrompt);
        Assert.Contains("EXAMPLES:", renderedPrompt);
        Assert.Contains("How do I set up Chess?", renderedPrompt);
        Assert.Contains("Place the board...", renderedPrompt);
        Assert.Contains("How does a pawn move?", renderedPrompt);
        Assert.Contains("Pawns move forward...", renderedPrompt);
    }

    [Fact]
    public void RenderSystemPrompt_WithoutFewShotExamples_ReturnsOnlySystemPrompt()
    {
        // Arrange
        var config = CreateMinimalConfiguration();
        var service = CreateService(config);
        var template = new PromptTemplate
        {
            SystemPrompt = "You are a board game assistant.",
            UserPromptTemplate = "Context: {context}\nQuestion: {query}"
        };

        // Act
        var renderedPrompt = service.RenderSystemPrompt(template);

        // Assert
        Assert.Equal("You are a board game assistant.", renderedPrompt);
        Assert.DoesNotContain("EXAMPLES:", renderedPrompt);
    }

    [Fact]
    public void RenderUserPrompt_WithContextAndQuery_ReplacesPlaceholders()
    {
        // Arrange
        var config = CreateMinimalConfiguration();
        var service = CreateService(config);
        var template = new PromptTemplate
        {
            SystemPrompt = "System prompt",
            UserPromptTemplate = "CONTEXT FROM RULEBOOK:\n{context}\n\nQUESTION:\n{query}\n\nANSWER:"
        };
        var context = "Page 1: Chess is played on an 8x8 board.";
        var query = "How big is a chess board?";

        // Act
        var renderedPrompt = service.RenderUserPrompt(template, context, query);

        // Assert
        Assert.Contains(context, renderedPrompt);
        Assert.Contains(query, renderedPrompt);
        Assert.DoesNotContain("{context}", renderedPrompt);
        Assert.DoesNotContain("{query}", renderedPrompt);
    }

    [Fact]
    public void RenderUserPrompt_WithEmptyContext_HandlesGracefully()
    {
        // Arrange
        var config = CreateMinimalConfiguration();
        var service = CreateService(config);
        var template = new PromptTemplate
        {
            SystemPrompt = "System prompt",
            UserPromptTemplate = "CONTEXT:\n{context}\n\nQUESTION:\n{query}"
        };

        // Act
        var renderedPrompt = service.RenderUserPrompt(template, "", "What is the answer?");

        // Assert
        Assert.Contains("What is the answer?", renderedPrompt);
        Assert.DoesNotContain("{query}", renderedPrompt);
    }

    #endregion

    #region Question Classification Tests

    [Theory]
    [InlineData("How do I set up Chess?", QuestionType.Setup)]
    [InlineData("How do I start the game?", QuestionType.Setup)]
    [InlineData("How to prepare the board?", QuestionType.Setup)]
    [InlineData("Where do I place pieces?", QuestionType.Setup)]
    public void ClassifyQuestion_WithSetupKeywords_ReturnsSetup(string query, QuestionType expected)
    {
        // Arrange
        var config = CreateMinimalConfiguration();
        var service = CreateService(config);

        // Act
        var questionType = service.ClassifyQuestion(query);

        // Assert
        Assert.Equal(expected, questionType);
    }

    [Theory]
    [InlineData("How does a knight move?", QuestionType.Gameplay)]
    [InlineData("Can I move my pawn backwards?", QuestionType.Gameplay)]
    [InlineData("What actions can I take?", QuestionType.Gameplay)]
    [InlineData("Is this move allowed?", QuestionType.Gameplay)]
    public void ClassifyQuestion_WithGameplayKeywords_ReturnsGameplay(string query, QuestionType expected)
    {
        // Arrange
        var config = CreateMinimalConfiguration();
        var service = CreateService(config);

        // Act
        var questionType = service.ClassifyQuestion(query);

        // Assert
        Assert.Equal(expected, questionType);
    }

    [Theory]
    [InlineData("How do I win Chess?", QuestionType.WinningConditions)]
    [InlineData("What is checkmate?", QuestionType.WinningConditions)]
    [InlineData("How can I lose?", QuestionType.WinningConditions)]
    [InlineData("Victory conditions?", QuestionType.WinningConditions)]
    public void ClassifyQuestion_WithWinningKeywords_ReturnsWinningConditions(string query, QuestionType expected)
    {
        // Arrange
        var config = CreateMinimalConfiguration();
        var service = CreateService(config);

        // Act
        var questionType = service.ClassifyQuestion(query);

        // Assert
        Assert.Equal(expected, questionType);
    }

    [Theory]
    [InlineData("What is en passant?", QuestionType.EdgeCases)]
    [InlineData("Can I castle after moving my king?", QuestionType.EdgeCases)]
    [InlineData("What is a stalemate?", QuestionType.EdgeCases)]
    [InlineData("Are there any special rules?", QuestionType.EdgeCases)]
    public void ClassifyQuestion_WithEdgeCaseKeywords_ReturnsEdgeCases(string query, QuestionType expected)
    {
        // Arrange
        var config = CreateMinimalConfiguration();
        var service = CreateService(config);

        // Act
        var questionType = service.ClassifyQuestion(query);

        // Assert
        Assert.Equal(expected, questionType);
    }

    [Theory]
    [InlineData("Tell me about Chess", QuestionType.General)]
    [InlineData("What is this game?", QuestionType.General)]
    [InlineData("Random question", QuestionType.General)]
    public void ClassifyQuestion_WithoutSpecificKeywords_ReturnsGeneral(string query, QuestionType expected)
    {
        // Arrange
        var config = CreateMinimalConfiguration();
        var service = CreateService(config);

        // Act
        var questionType = service.ClassifyQuestion(query);

        // Assert
        Assert.Equal(expected, questionType);
    }

    [Fact]
    public void ClassifyQuestion_WithNullOrEmpty_ReturnsGeneral()
    {
        // Arrange
        var config = CreateMinimalConfiguration();
        var service = CreateService(config);

        // Act
        var nullResult = service.ClassifyQuestion(null!);
        var emptyResult = service.ClassifyQuestion("");
        var whitespaceResult = service.ClassifyQuestion("   ");

        // Assert
        Assert.Equal(QuestionType.General, nullResult);
        Assert.Equal(QuestionType.General, emptyResult);
        Assert.Equal(QuestionType.General, whitespaceResult);
    }

    #endregion

    #region Fallback Tests

    [Fact]
    public async Task GetTemplateAsync_WithNullConfiguration_ReturnsFallbackTemplate()
    {
        // Arrange
        var service = CreateService(null);

        // Act
        var template = await service.GetTemplateAsync(null, QuestionType.General);

        // Assert
        Assert.NotNull(template);
        Assert.Contains("board game rules assistant", template.SystemPrompt);
        Assert.Contains("{context}", template.UserPromptTemplate);
        Assert.Contains("{query}", template.UserPromptTemplate);
    }

    [Fact]
    public async Task GetTemplateAsync_WithEmptyConfiguration_ReturnsFallbackTemplate()
    {
        // Arrange
        var emptyConfig = new RagPromptsConfiguration();
        var service = CreateService(emptyConfig);

        // Act
        var template = await service.GetTemplateAsync(null, QuestionType.General);

        // Assert
        Assert.NotNull(template);
        Assert.False(string.IsNullOrWhiteSpace(template.SystemPrompt));
        Assert.False(string.IsNullOrWhiteSpace(template.UserPromptTemplate));
    }

    #endregion

    #region Helper Methods

    private PromptTemplateService CreateService(RagPromptsConfiguration? config)
    {
        var options = Options.Create(config ?? new RagPromptsConfiguration());
        return new PromptTemplateService(options, _mockLogger.Object);
    }

    private RagPromptsConfiguration CreateMinimalConfiguration()
    {
        return new RagPromptsConfiguration
        {
            Default = new PromptTemplateConfig
            {
                SystemPrompt = "You are a board game rules assistant.",
                UserPromptTemplate = "CONTEXT FROM RULEBOOK:\n{context}\n\nQUESTION:\n{query}\n\nANSWER:",
                FewShotExamples = new List<FewShotExampleConfig>()
            }
        };
    }

    private RagPromptsConfiguration CreateConfigurationWithQuestionTypes()
    {
        var config = CreateMinimalConfiguration();
        config.Templates = new Dictionary<string, PromptTemplateConfig>
        {
            ["Setup"] = new PromptTemplateConfig
            {
                SystemPrompt = "You are a game setup expert. Help users set up their board games.",
                UserPromptTemplate = "CONTEXT:\n{context}\n\nSETUP QUESTION:\n{query}\n\nANSWER:",
                FewShotExamples = new List<FewShotExampleConfig>()
            },
            ["Gameplay"] = new PromptTemplateConfig
            {
                SystemPrompt = "You are a gameplay rules expert. Answer questions about how to play the game.",
                UserPromptTemplate = "CONTEXT:\n{context}\n\nGAMEPLAY QUESTION:\n{query}\n\nANSWER:",
                FewShotExamples = new List<FewShotExampleConfig>()
            }
        };
        return config;
    }

    private RagPromptsConfiguration CreateConfigurationWithGameTemplates(Guid gameId)
    {
        var config = CreateMinimalConfiguration();
        config.GameTemplates = new Dictionary<string, Dictionary<string, PromptTemplateConfig>>
        {
            [gameId.ToString()] = new Dictionary<string, PromptTemplateConfig>
            {
                ["General"] = new PromptTemplateConfig
                {
                    SystemPrompt = "You are a Chess rules expert. Answer questions about Chess specifically.",
                    UserPromptTemplate = "CHESS RULEBOOK:\n{context}\n\nCHESS QUESTION:\n{query}\n\nANSWER:",
                    FewShotExamples = new List<FewShotExampleConfig>()
                }
            }
        };
        return config;
    }

    private RagPromptsConfiguration CreateConfigurationWithFewShotExamples()
    {
        var config = CreateMinimalConfiguration();
        config.Templates = new Dictionary<string, PromptTemplateConfig>
        {
            ["Setup"] = new PromptTemplateConfig
            {
                SystemPrompt = "You are a game setup expert.",
                UserPromptTemplate = "CONTEXT:\n{context}\n\nQUESTION:\n{query}",
                FewShotExamples = new List<FewShotExampleConfig>
                {
                    new()
                    {
                        Question = "How do I set up Chess?",
                        Answer = "To set up Chess, place the board so each player has a white square in the bottom-right corner.",
                        Category = "Setup"
                    },
                    new()
                    {
                        Question = "How do I set up Tic-Tac-Toe?",
                        Answer = "Draw a 3x3 grid. Each player chooses X or O.",
                        Category = "Setup"
                    }
                }
            }
        };
        return config;
    }

    #endregion
}
