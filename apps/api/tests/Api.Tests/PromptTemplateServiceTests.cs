using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace Api.Tests;

/// <summary>
/// ADMIN-01 Enhanced: Unit tests for PromptTemplateService
/// Tests template loading, rendering, question classification, fallback behavior, and Redis caching
/// </summary>
public class PromptTemplateServiceTests : IDisposable
{
    private readonly ITestOutputHelper _output;

    private readonly Mock<ILogger<PromptTemplateService>> _mockLogger;
    private readonly Mock<IConnectionMultiplexer> _redisMock;
    private readonly Mock<IDatabase> _redisDbMock;
    private readonly SqliteConnection _connection;
    private readonly MeepleAiDbContext _dbContext;

    public PromptTemplateServiceTests(ITestOutputHelper output)
    {
        _output = output;
        _mockLogger = new Mock<ILogger<PromptTemplateService>>();

        // ADMIN-01: Mock Redis
        _redisMock = new Mock<IConnectionMultiplexer>();
        _redisDbMock = new Mock<IDatabase>();
        _redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_redisDbMock.Object);

        // ADMIN-01: Setup SQLite in-memory database for testing
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new MeepleAiDbContext(options);
        _dbContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
        _connection?.Dispose();
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
        template.Should().NotBeNull();
        template.QuestionType.Should().Be(QuestionType.General);
        string.IsNullOrWhiteSpace(template.SystemPrompt).Should().BeFalse();
        string.IsNullOrWhiteSpace(template.UserPromptTemplate).Should().BeFalse();
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
        setupTemplate.Should().NotBeNull();
        gameplayTemplate.Should().NotBeNull();
        setupTemplate.QuestionType.Should().Be(QuestionType.Setup);
        gameplayTemplate.QuestionType.Should().Be(QuestionType.Gameplay);
        gameplayTemplate.SystemPrompt.Should().NotBe(setupTemplate.SystemPrompt);
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
        template.Should().NotBeNull();
        template.GameId.Should().Be(gameId);
        template.SystemPrompt.Should().Contain("Chess"); // Game-specific content
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
        template.Should().NotBeNull();
        template.GameId.Should().BeNull(); // Falls back to default
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
        template.Should().NotBeNull();
        template.QuestionType.Should().Be(QuestionType.General); // Falls back to General
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
        template.Should().NotBeNull();
        template.FewShotExamples.Should().NotBeEmpty();
        template.FewShotExamples.Should().OnlyContain(example =>
        {
            string.IsNullOrWhiteSpace(example.Question).Should().BeFalse();
            string.IsNullOrWhiteSpace(example.Answer).Should().BeFalse();
            string.IsNullOrWhiteSpace(example.Category).Should().BeFalse();
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
        template.Should().NotBeNull();
        template.FewShotExamples.Should().BeEmpty();
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
        renderedPrompt.Should().Contain("You are a board game assistant");
        renderedPrompt.Should().Contain("EXAMPLES:");
        renderedPrompt.Should().Contain("How do I set up Chess?");
        renderedPrompt.Should().Contain("Place the board...");
        renderedPrompt.Should().Contain("How does a pawn move?");
        renderedPrompt.Should().Contain("Pawns move forward...");
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
        renderedPrompt.Should().Be("You are a board game assistant.");
        renderedPrompt.Should().NotContain("EXAMPLES:");
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
        renderedPrompt.Should().Contain(context);
        renderedPrompt.Should().Contain(query);
        renderedPrompt.Should().NotContain("{context}");
        renderedPrompt.Should().NotContain("{query}");
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
        renderedPrompt.Should().Contain("What is the answer?");
        renderedPrompt.Should().NotContain("{query}");
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
        questionType.Should().Be(expected);
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
        questionType.Should().Be(expected);
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
        questionType.Should().Be(expected);
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
        questionType.Should().Be(expected);
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
        questionType.Should().Be(expected);
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
        nullResult.Should().Be(QuestionType.General);
        emptyResult.Should().Be(QuestionType.General);
        whitespaceResult.Should().Be(QuestionType.General);
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
        template.Should().NotBeNull();
        template.SystemPrompt.Should().Contain("board game rules assistant");
        template.UserPromptTemplate.Should().Contain("{context}");
        template.UserPromptTemplate.Should().Contain("{query}");
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
        template.Should().NotBeNull();
        string.IsNullOrWhiteSpace(template.SystemPrompt).Should().BeFalse();
        string.IsNullOrWhiteSpace(template.UserPromptTemplate).Should().BeFalse();
    }

    #endregion

    #region ADMIN-01: Database-Driven Prompt Management Tests

    [Fact]
    public async Task GetActivePromptAsync_WithCacheHit_ReturnsCachedPrompt()
    {
        // Arrange
        var templateName = "qa-system-prompt";
        var cachedContent = "Cached prompt content";
        var cacheKey = $"prompt:{templateName}:active";

        _redisDbMock.Setup(db => db.StringGetAsync(cacheKey, It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)cachedContent);

        var service = CreateService(null);

        // Act
        var result = await service.GetActivePromptAsync(templateName);

        // Assert
        result.Should().Be(cachedContent);
        _redisDbMock.Verify(db => db.StringGetAsync(cacheKey, It.IsAny<CommandFlags>()), Times.Once);
        // Should NOT query database on cache hit
    }

    [Fact]
    public async Task GetActivePromptAsync_WithCacheMiss_QueriesDatabaseAndPopulatesCache()
    {
        // Arrange
        var templateName = "qa-system-prompt";
        var promptContent = "Database prompt content";
        var cacheKey = $"prompt:{templateName}:active";

        // Setup cache miss
        _redisDbMock.Setup(db => db.StringGetAsync(cacheKey, It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        // Setup cache write
        _redisDbMock.Setup(db => db.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(), // keepTtl
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Seed database with active prompt
        await SeedActivePrompt(templateName, promptContent);

        var service = CreateService(null);

        // Act
        var result = await service.GetActivePromptAsync(templateName);

        // Assert
        result.Should().Be(promptContent);
        _redisDbMock.Verify(db => db.StringGetAsync(cacheKey, It.IsAny<CommandFlags>()), Times.Once);
        _redisDbMock.Verify(db => db.StringSetAsync(
            It.IsAny<RedisKey>(),
            It.Is<RedisValue>(v => v.ToString() == promptContent),
            It.Is<TimeSpan?>(ttl => ttl != null),
            It.IsAny<bool>(), // keepTtl parameter
            It.IsAny<When>(),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task GetActivePromptAsync_WhenRedisUnavailable_FallbackToDatabase()
    {
        // Arrange
        var templateName = "qa-system-prompt";
        var promptContent = "Database prompt content";

        // Setup Redis failure
        _redisDbMock.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisException("Redis connection failed"));

        // Seed database
        await SeedActivePrompt(templateName, promptContent);

        var service = CreateService(null);

        // Act
        var result = await service.GetActivePromptAsync(templateName);

        // Assert
        result.Should().Be(promptContent);
        // Should still work despite Redis failure
    }

    [Fact]
    public async Task GetActivePromptAsync_WhenNoActiveVersion_ReturnsNull()
    {
        // Arrange
        var templateName = "nonexistent-prompt";

        _redisDbMock.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        var service = CreateService(null);

        // Act
        var result = await service.GetActivePromptAsync(templateName);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ActivateVersionAsync_SuccessfulActivation_DeactivatesOldVersionAndInvalidatesCache()
    {
        // Arrange
        var (templateId, versionId, templateName) = await SeedTemplateWithMultipleVersions();
        var userId = await SeedUser("admin@test.com");

        // Setup Redis cache invalidation
        _redisDbMock.Setup(db => db.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        var service = CreateService(null);

        // Act
        var result = await service.ActivateVersionAsync(templateId, versionId, userId);

        // Assert
        result.Should().BeTrue();

        // Verify only the new version is active
        var allVersions = await _dbContext.Set<PromptVersionEntity>()
            .Where(v => v.TemplateId == templateId)
            .ToListAsync();

        allVersions.Where(v => v.IsActive).Should().ContainSingle();
        allVersions.First(v => v.IsActive).Id.Should().Be(versionId);

        // Verify cache invalidation
        _redisDbMock.Verify(db => db.KeyDeleteAsync(
            It.Is<RedisKey>(k => k.ToString().Contains(templateName)),
            It.IsAny<CommandFlags>()), Times.Once);

        // Verify audit log created
        var auditLogs = await _dbContext.Set<PromptAuditLogEntity>()
            .Where(a => a.TemplateId == templateId && a.VersionId == versionId)
            .ToListAsync();

        auditLogs.Should().ContainSingle();
        auditLogs[0].Action.Should().Be("version_activated");
    }

    [Fact]
    public async Task ActivateVersionAsync_WhenVersionNotFound_ReturnsFalse()
    {
        // Arrange
        var nonExistentTemplateId = Guid.NewGuid().ToString();
        var nonExistentVersionId = Guid.NewGuid().ToString();
        var userId = await SeedUser("admin@test.com");

        var service = CreateService(null);

        // Act
        var result = await service.ActivateVersionAsync(nonExistentTemplateId, nonExistentVersionId, userId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task InvalidateCacheAsync_DeletesCacheKey()
    {
        // Arrange
        var templateName = "qa-system-prompt";
        var cacheKey = $"prompt:{templateName}:active";

        _redisDbMock.Setup(db => db.KeyDeleteAsync(cacheKey, It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        var service = CreateService(null);

        // Act
        await service.InvalidateCacheAsync(templateName);

        // Assert
        _redisDbMock.Verify(db => db.KeyDeleteAsync(cacheKey, It.IsAny<CommandFlags>()), Times.Once);
    }

    #endregion

    #region Helper Methods

    private PromptTemplateService CreateService(RagPromptsConfiguration? config)
    {
        var options = Options.Create(config ?? new RagPromptsConfiguration());
        return new PromptTemplateService(_dbContext, _redisMock.Object, options, _mockLogger.Object);
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

    // ADMIN-01: Database seeding helpers

    private async Task<string> SeedUser(string email)
    {
        var userId = Guid.NewGuid().ToString();
        var user = new UserEntity
        {
            Id = userId,
            Email = email,
            DisplayName = "Test User",
            PasswordHash = "hash",
            Role = UserRole.Admin,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Set<UserEntity>().Add(user);
        await _dbContext.SaveChangesAsync();
        return userId;
    }

    private async Task SeedActivePrompt(string templateName, string content)
    {
        var userId = await SeedUser("creator@test.com");
        var templateId = Guid.NewGuid().ToString();

        var template = new PromptTemplateEntity
        {
            Id = templateId,
            Name = templateName,
            Description = "Test template",
            Category = "test",
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = await _dbContext.Set<UserEntity>().FindAsync(userId)
                ?? throw new InvalidOperationException("User not found")
        };

        var version = new PromptVersionEntity
        {
            Id = Guid.NewGuid().ToString(),
            TemplateId = templateId,
            VersionNumber = 1,
            Content = content,
            IsActive = true,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
            Template = template,
            CreatedBy = template.CreatedBy
        };

        _dbContext.Set<PromptTemplateEntity>().Add(template);
        _dbContext.Set<PromptVersionEntity>().Add(version);
        await _dbContext.SaveChangesAsync();
    }

    private async Task<(string templateId, string versionId, string templateName)> SeedTemplateWithMultipleVersions()
    {
        var userId = await SeedUser("creator@test.com");
        var templateId = Guid.NewGuid().ToString();
        var templateName = "test-prompt";

        var user = await _dbContext.Set<UserEntity>().FindAsync(userId)
            ?? throw new InvalidOperationException("User not found");

        var template = new PromptTemplateEntity
        {
            Id = templateId,
            Name = templateName,
            Description = "Test template with multiple versions",
            Category = "test",
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = user
        };

        // Create 3 versions (v1 active, v2-v3 inactive)
        var version1 = new PromptVersionEntity
        {
            Id = Guid.NewGuid().ToString(),
            TemplateId = templateId,
            VersionNumber = 1,
            Content = "Version 1 content",
            IsActive = true,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            Template = template,
            CreatedBy = user
        };

        var version2Id = Guid.NewGuid().ToString();
        var version2 = new PromptVersionEntity
        {
            Id = version2Id,
            TemplateId = templateId,
            VersionNumber = 2,
            Content = "Version 2 content",
            IsActive = false,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            Template = template,
            CreatedBy = user
        };

        var version3 = new PromptVersionEntity
        {
            Id = Guid.NewGuid().ToString(),
            TemplateId = templateId,
            VersionNumber = 3,
            Content = "Version 3 content",
            IsActive = false,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
            Template = template,
            CreatedBy = user
        };

        _dbContext.Set<PromptTemplateEntity>().Add(template);
        _dbContext.Set<PromptVersionEntity>().AddRange(version1, version2, version3);
        await _dbContext.SaveChangesAsync();

        return (templateId, version2Id, templateName); // Return v2 for activation tests
    }

    #endregion
}
