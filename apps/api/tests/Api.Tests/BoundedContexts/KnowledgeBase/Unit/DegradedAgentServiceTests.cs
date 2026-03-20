using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Infrastructure.Entities.UserLibrary;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Unit;

/// <summary>
/// Unit tests for DegradedAgentService.
/// E4-1: Degraded Agent Service — BGG-only mode when no KB cards are available.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class DegradedAgentServiceTests
{
    #region BuildDegradedSystemPrompt Tests (pure function — no DB needed)

    [Fact]
    public void BuildDegradedSystemPrompt_WithFullMetadata_ShouldIncludeAllFields()
    {
        // Arrange
        var service = CreateServiceWithFreshDb(out _);
        var context = new DegradedAgentContext
        {
            GameName = "Catan",
            Description = "Trade, build, and settle the island of Catan.",
            MinPlayers = 3,
            MaxPlayers = 4,
            PlayTimeMinutes = 90,
            ComplexityWeight = 2.3m,
            YearPublished = "1995",
            Categories = new List<string> { "Strategy", "Economic" },
            Mechanics = new List<string> { "Dice Rolling", "Trading" },
            Publisher = "Kosmos",
            Designer = "Klaus Teuber"
        };

        // Act
        var prompt = service.BuildDegradedSystemPrompt(context);

        // Assert
        prompt.Should().Contain("**Catan**");
        prompt.Should().Contain("3-4 players");
        prompt.Should().Contain("~90 minutes");
        prompt.Should().Contain("2.3/5");
        prompt.Should().Contain("1995");
        prompt.Should().Contain("Kosmos");
        prompt.Should().Contain("Klaus Teuber");
        prompt.Should().Contain("Strategy, Economic");
        prompt.Should().Contain("Dice Rolling, Trading");
        prompt.Should().Contain("Trade, build, and settle the island of Catan.");
    }

    [Fact]
    public void BuildDegradedSystemPrompt_WithMinimalMetadata_ShouldStillWork()
    {
        // Arrange
        var service = CreateServiceWithFreshDb(out _);
        var context = new DegradedAgentContext
        {
            GameName = "Unknown Game"
        };

        // Act
        var prompt = service.BuildDegradedSystemPrompt(context);

        // Assert
        prompt.Should().Contain("**Unknown Game**");
        prompt.Should().Contain("What You Can Help With");
        // Should not contain optional metadata fields that weren't set (check bold format from Game Information section)
        prompt.Should().NotContain("**Players**");
        prompt.Should().NotContain("**Play Time**");
        prompt.Should().NotContain("**Complexity**");
        prompt.Should().NotContain("**Year Published**");
        prompt.Should().NotContain("**Publisher**");
        prompt.Should().NotContain("**Designer**");
        prompt.Should().NotContain("**Categories**");
        prompt.Should().NotContain("**Mechanics**");
        prompt.Should().NotContain("Game Description");
    }

    [Fact]
    public void BuildDegradedSystemPrompt_ShouldIncludeLimitationsWarning()
    {
        // Arrange
        var service = CreateServiceWithFreshDb(out _);
        var context = new DegradedAgentContext { GameName = "Catan" };

        // Act
        var prompt = service.BuildDegradedSystemPrompt(context);

        // Assert
        prompt.Should().Contain("Important Limitations");
        prompt.Should().Contain("do NOT have access to the full rulebook");
        prompt.Should().Contain("upload the PDF");
    }

    [Fact]
    public void BuildDegradedSystemPrompt_ShouldTruncateLongDescription()
    {
        // Arrange
        var service = CreateServiceWithFreshDb(out _);
        var longDescription = new string('A', 2000);
        var context = new DegradedAgentContext
        {
            GameName = "Verbose Game",
            Description = longDescription
        };

        // Act
        var prompt = service.BuildDegradedSystemPrompt(context);

        // Assert — description should be truncated to 1000 chars + "..."
        prompt.Should().Contain("...");
        prompt.Should().NotContain(longDescription);
        // The truncated description should be exactly 1003 chars (1000 + "...")
        prompt.Should().Contain(longDescription[..1000]);
    }

    [Fact]
    public void BuildDegradedSystemPrompt_WithSameMinMaxPlayers_ShouldShowSingleNumber()
    {
        // Arrange
        var service = CreateServiceWithFreshDb(out _);
        var context = new DegradedAgentContext
        {
            GameName = "Solo Game",
            MinPlayers = 1,
            MaxPlayers = 1
        };

        // Act
        var prompt = service.BuildDegradedSystemPrompt(context);

        // Assert
        prompt.Should().Contain("1 players");
        prompt.Should().NotContain("1-1");
    }

    #endregion

    #region GetAgentCapabilityAsync Tests (requires InMemory DB)

    [Fact]
    public async Task GetAgentCapability_WithKbCards_ShouldReturnFull()
    {
        // Arrange
        var service = CreateServiceWithFreshDb(out var db);

        var agentDef = AgentDefinition.Create(
            "Test Agent",
            "Test description",
            AgentType.Custom("rag", "RAG agent"),
            AgentDefinitionConfig.Create("gpt-4", 1000, 0.7f));

        // Give it KB cards
        agentDef.UpdateKbCardIds(new[] { Guid.NewGuid() });

        db.AgentDefinitions.Add(agentDef);
        await db.SaveChangesAsync();

        // Act
        var capability = await service.GetAgentCapabilityAsync(agentDef.Id);

        // Assert
        capability.Level.Should().Be(AgentCapabilityLevel.Full);
        capability.HasKbCards.Should().BeTrue();
        capability.HasRulebookAnalysis.Should().BeFalse();
    }

    [Fact]
    public async Task GetAgentCapability_WithKbCardsAndRulebookAnalysis_ShouldReturnFullWithRulebook()
    {
        // Arrange
        var service = CreateServiceWithFreshDb(out var db);

        var agentDef = AgentDefinition.Create(
            "Agent With Rulebook",
            "Test",
            AgentType.Custom("rag", "RAG agent"),
            AgentDefinitionConfig.Create("gpt-4", 1000, 0.7f));

        agentDef.UpdateKbCardIds(new[] { Guid.NewGuid() });
        db.AgentDefinitions.Add(agentDef);

        // Link a shared game to this agent
        var sharedGame = new SharedGameEntity
        {
            Id = Guid.NewGuid(),
            AgentDefinitionId = agentDef.Id,
            Title = "Test Game",
            Description = "A test game",
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };
        db.SharedGames.Add(sharedGame);

        // Add a rulebook analysis for the shared game
        var rulebookAnalysis = new RulebookAnalysisEntity
        {
            Id = Guid.NewGuid(),
            SharedGameId = sharedGame.Id,
            PdfDocumentId = Guid.NewGuid(),
            GameTitle = "Test Game",
            Summary = "Test summary",
            ConfidenceScore = 0.9m,
            IsActive = true,
            AnalyzedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
        db.RulebookAnalyses.Add(rulebookAnalysis);
        await db.SaveChangesAsync();

        // Act
        var capability = await service.GetAgentCapabilityAsync(agentDef.Id);

        // Assert
        capability.Level.Should().Be(AgentCapabilityLevel.Full);
        capability.HasKbCards.Should().BeTrue();
        capability.HasRulebookAnalysis.Should().BeTrue();
    }

    [Fact]
    public async Task GetAgentCapability_WithNoKbCardsButSharedGame_ShouldReturnDegraded()
    {
        // Arrange
        var service = CreateServiceWithFreshDb(out var db);

        var agentDef = AgentDefinition.Create(
            "Degraded Agent",
            "No KB cards",
            AgentType.Custom("rag", "RAG agent"),
            AgentDefinitionConfig.Create("gpt-4", 1000, 0.7f));

        // No KB cards — leave KbCardIds empty
        db.AgentDefinitions.Add(agentDef);

        // Link a shared game to this agent (BGG metadata available)
        var sharedGame = new SharedGameEntity
        {
            Id = Guid.NewGuid(),
            AgentDefinitionId = agentDef.Id,
            Title = "BGG Game",
            Description = "A game from BGG",
            MinPlayers = 2,
            MaxPlayers = 4,
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };
        db.SharedGames.Add(sharedGame);
        await db.SaveChangesAsync();

        // Act
        var capability = await service.GetAgentCapabilityAsync(agentDef.Id);

        // Assert
        capability.Level.Should().Be(AgentCapabilityLevel.Degraded);
        capability.HasKbCards.Should().BeFalse();
        capability.HasBggMetadata.Should().BeTrue();
    }

    [Fact]
    public async Task GetAgentCapability_WithNoKbCardsButPrivateGame_ShouldReturnDegraded()
    {
        // Arrange
        var service = CreateServiceWithFreshDb(out var db);

        var agentDef = AgentDefinition.Create(
            "Private Game Agent",
            "No KB cards, private game",
            AgentType.Custom("rag", "RAG agent"),
            AgentDefinitionConfig.Create("gpt-4", 1000, 0.7f));

        db.AgentDefinitions.Add(agentDef);

        // Link a private game to this agent
        var privateGame = new PrivateGameEntity
        {
            Id = Guid.NewGuid(),
            OwnerId = Guid.NewGuid(),
            AgentDefinitionId = agentDef.Id,
            Title = "My Private Game",
            MinPlayers = 1,
            MaxPlayers = 2,
            CreatedAt = DateTime.UtcNow
        };
        db.PrivateGames.Add(privateGame);
        await db.SaveChangesAsync();

        // Act
        var capability = await service.GetAgentCapabilityAsync(agentDef.Id);

        // Assert
        capability.Level.Should().Be(AgentCapabilityLevel.Degraded);
        capability.HasKbCards.Should().BeFalse();
        capability.HasBggMetadata.Should().BeTrue();
    }

    [Fact]
    public async Task GetAgentCapability_WithNoAgent_ShouldReturnNone()
    {
        // Arrange
        var service = CreateServiceWithFreshDb(out _);
        var nonExistentAgentId = Guid.NewGuid();

        // Act
        var capability = await service.GetAgentCapabilityAsync(nonExistentAgentId);

        // Assert
        capability.Level.Should().Be(AgentCapabilityLevel.None);
        capability.HasKbCards.Should().BeFalse();
        capability.HasBggMetadata.Should().BeFalse();
        capability.HasRulebookAnalysis.Should().BeFalse();
    }

    [Fact]
    public async Task GetAgentCapability_WithAgentButNoLinkedGame_ShouldReturnNone()
    {
        // Arrange
        var service = CreateServiceWithFreshDb(out var db);

        var agentDef = AgentDefinition.Create(
            "Orphan Agent",
            "No KB cards, no game",
            AgentType.Custom("rag", "RAG agent"),
            AgentDefinitionConfig.Create("gpt-4", 1000, 0.7f));

        db.AgentDefinitions.Add(agentDef);
        await db.SaveChangesAsync();

        // Act
        var capability = await service.GetAgentCapabilityAsync(agentDef.Id);

        // Assert
        capability.Level.Should().Be(AgentCapabilityLevel.None);
        capability.HasKbCards.Should().BeFalse();
        capability.HasBggMetadata.Should().BeFalse();
    }

    #endregion

    #region AgentCapability Value Object Tests

    [Fact]
    public void AgentCapability_Full_ShouldHaveCorrectProperties()
    {
        var capability = AgentCapability.Full();
        capability.Level.Should().Be(AgentCapabilityLevel.Full);
        capability.HasKbCards.Should().BeTrue();
        capability.HasBggMetadata.Should().BeTrue();
        capability.HasRulebookAnalysis.Should().BeFalse();
        capability.Description.Should().Contain("Full RAG");
    }

    [Fact]
    public void AgentCapability_Full_WithRulebook_ShouldIncludeRulebookInDescription()
    {
        var capability = AgentCapability.Full(hasRulebookAnalysis: true);
        capability.HasRulebookAnalysis.Should().BeTrue();
        capability.Description.Should().Contain("rulebook analysis");
    }

    [Fact]
    public void AgentCapability_Degraded_ShouldHaveCorrectProperties()
    {
        var capability = AgentCapability.Degraded();
        capability.Level.Should().Be(AgentCapabilityLevel.Degraded);
        capability.HasKbCards.Should().BeFalse();
        capability.HasBggMetadata.Should().BeTrue();
        capability.HasRulebookAnalysis.Should().BeFalse();
        capability.Description.Should().Contain("BGG metadata only");
    }

    [Fact]
    public void AgentCapability_None_ShouldHaveCorrectProperties()
    {
        var capability = AgentCapability.None();
        capability.Level.Should().Be(AgentCapabilityLevel.None);
        capability.HasKbCards.Should().BeFalse();
        capability.HasBggMetadata.Should().BeFalse();
        capability.HasRulebookAnalysis.Should().BeFalse();
        capability.Description.Should().Contain("No knowledge sources");
    }

    #endregion

    #region Helpers

    private static DegradedAgentService CreateServiceWithFreshDb(out MeepleAiDbContext db)
    {
        db = TestDbContextFactory.CreateInMemoryDbContext();
        return new DegradedAgentService(db);
    }

    #endregion
}
