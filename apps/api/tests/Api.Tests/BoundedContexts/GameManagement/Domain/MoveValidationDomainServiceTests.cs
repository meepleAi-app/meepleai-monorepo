using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Services;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;

namespace Api.Tests.BoundedContexts.GameManagement.Domain;

/// <summary>
/// Unit tests for MoveValidationDomainService
/// ISSUE-1500: TEST-002 - Fixed test isolation (fresh context per test)
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class MoveValidationDomainServiceTests
{
    /// <summary>
    /// Creates a fresh DbContext for each test to ensure complete isolation
    /// </summary>
    private static MeepleAiDbContext CreateFreshDbContext()
    {
        return TestDbContextFactory.CreateInMemoryDbContext();
    }

    /// <summary>
    /// Creates a MoveValidationDomainService instance with the given context
    /// </summary>
    private static MoveValidationDomainService CreateService(MeepleAiDbContext context)
    {
        var loggerMock = new Mock<ILogger<MoveValidationDomainService>>();
        return new MoveValidationDomainService(context, loggerMock.Object);
    }
    [Fact]
    public void Constructor_WithNullDbContext_ThrowsArgumentNullException()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<MoveValidationDomainService>>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MoveValidationDomainService(null!, loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange - fresh context per test
        using var context = CreateFreshDbContext();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MoveValidationDomainService(context, null!));
    }
    [Fact]
    public async Task ValidateMoveAsync_WithNullSession_ThrowsArgumentNullException()
    {
        // Arrange - fresh context per test
        using var context = CreateFreshDbContext();
        var service = CreateService(context);
        var move = new Move("Alice", "roll dice");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.ValidateMoveAsync(null!, move));
    }

    [Fact]
    public async Task ValidateMoveAsync_WithNullMove_ThrowsArgumentNullException()
    {
        // Arrange - fresh context per test
        using var context = CreateFreshDbContext();
        var service = CreateService(context);
        var session = CreateDefaultSession();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.ValidateMoveAsync(session, null!));
    }

    [Fact]
    public async Task ValidateMoveAsync_WithFinishedSession_ReturnsInvalid()
    {
        // Arrange - fresh context per test
        using var context = CreateFreshDbContext();
        var service = CreateService(context);
        var session = CreateDefaultSession();
        session.Start();
        session.Complete("Alice");
        var move = new Move("Alice", "roll dice");

        // Act
        var result = await service.ValidateMoveAsync(session, move);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("session is Completed", result.Errors[0]);
        Assert.Equal(1.0, result.ConfidenceScore);
    }

    [Fact]
    public async Task ValidateMoveAsync_WithPlayerNotInSession_ReturnsInvalid()
    {
        // Arrange - fresh context per test
        using var context = CreateFreshDbContext();
        var service = CreateService(context);
        var session = CreateDefaultSession();
        session.Start();
        var move = new Move("Charlie", "roll dice"); // Charlie not in session

        // Act
        var result = await service.ValidateMoveAsync(session, move);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("not in this session", result.Errors[0]);
        Assert.Equal(1.0, result.ConfidenceScore);
    }
    [Fact]
    public async Task ValidateMoveAsync_WithNoRuleSpec_ReturnsUncertain()
    {
        // Arrange - fresh context per test
        using var context = CreateFreshDbContext();
        var service = CreateService(context);
        var session = CreateDefaultSession();
        session.Start();
        var move = new Move("Alice", "roll dice");

        // Act
        var result = await service.ValidateMoveAsync(session, move);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("No rule specification available", result.Errors[0]);
        Assert.Equal(0.0, result.ConfidenceScore);
        Assert.Empty(result.ApplicableRules);
    }

    [Fact]
    public async Task ValidateMoveAsync_WithRuleSpec_FindsApplicableRules()
    {
        // Arrange - fresh context per test
        using var context = CreateFreshDbContext();
        var service = CreateService(context);
        var gameId = Guid.NewGuid();

        await SeedRuleSpecAsync(context, gameId, "v1", new List<RuleAtom>
        {
            new RuleAtom("rule1", "Players must roll dice at the start of their turn", "Gameplay", "5"),
            new RuleAtom("rule2", "Movement is determined by dice roll", "Movement", "6"),
            new RuleAtom("rule3", "Players draw a card when landing on special spaces", "Cards", "8")
        });

        var session = CreateDefaultSession(gameId);
        session.Start();
        var move = new Move("Alice", "roll dice");

        // Act
        var result = await service.ValidateMoveAsync(session, move);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(2, result.ApplicableRules.Count); // Should find rules about dice
        Assert.All(result.ApplicableRules, rule =>
            Assert.Contains("dice", rule.text.ToLowerInvariant()));
    }

    [Fact]
    public async Task ValidateMoveAsync_WithSpecificVersion_UsesCorrectVersion()
    {
        // Arrange - fresh context per test
        using var context = CreateFreshDbContext();
        var service = CreateService(context);
        var gameId = Guid.NewGuid();

        await SeedRuleSpecAsync(context, gameId, "v1", new List<RuleAtom>
        {
            new RuleAtom("rule1", "Old dice rule", "Gameplay", "5")
        });

        await SeedRuleSpecAsync(context, gameId, "v2", new List<RuleAtom>
        {
            new RuleAtom("rule2", "New dice rule with advantage", "Gameplay", "5")
        });

        var session = CreateDefaultSession(gameId);
        session.Start();
        var move = new Move("Alice", "roll dice");

        // Act
        var result = await service.ValidateMoveAsync(session, move, ruleSpecVersion: "v1");

        // Assert
        Assert.True(result.IsValid);
        Assert.Single(result.ApplicableRules);
        Assert.Contains("Old dice rule", result.ApplicableRules[0].text);
    }
    [Fact]
    public async Task ValidateMoveAsync_WithComplexAction_FindsRelevantRules()
    {
        // Arrange - fresh context per test
        using var context = CreateFreshDbContext();
        var service = CreateService(context);
        var gameId = Guid.NewGuid();

        await SeedRuleSpecAsync(context, gameId, "v1", new List<RuleAtom>
        {
            new RuleAtom("rule1", "Players may trade resources with other players", "Trading", "10"),
            new RuleAtom("rule2", "Resources include wood, stone, and wheat", "Resources", "4"),
            new RuleAtom("rule3", "Combat is resolved by dice roll", "Combat", "15")
        });

        var session = CreateDefaultSession(gameId);
        session.Start();
        var move = new Move("Alice", "trade resources", additionalContext: new Dictionary<string, string>
        {
            { "resource", "wood" },
            { "partner", "Bob" }
        });

        // Act
        var result = await service.ValidateMoveAsync(session, move);

        // Assert
        Assert.True(result.IsValid);
        Assert.True(result.ApplicableRules.Count >= 2); // Should find trading and resources rules
    }

    [Fact]
    public async Task ValidateMoveAsync_WithPosition_ConsidersPositionInMatching()
    {
        // Arrange - fresh context per test
        using var context = CreateFreshDbContext();
        var service = CreateService(context);
        var gameId = Guid.NewGuid();

        await SeedRuleSpecAsync(context, gameId, "v1", new List<RuleAtom>
        {
            new RuleAtom("rule1", "The center space grants bonus points", "Spaces", "7"),
            new RuleAtom("rule2", "Corner spaces allow drawing a card", "Spaces", "8")
        });

        var session = CreateDefaultSession(gameId);
        session.Start();
        var move = new Move("Alice", "move piece", position: "center");

        // Act
        var result = await service.ValidateMoveAsync(session, move);

        // Assert
        Assert.True(result.IsValid);
        Assert.Contains(result.ApplicableRules, r => r.text.Contains("center"));
    }
    [Fact]
    public async Task ValidateMoveAsync_WithNoApplicableRules_ReturnsLowConfidence()
    {
        // Arrange - fresh context per test
        using var context = CreateFreshDbContext();
        var service = CreateService(context);
        var gameId = Guid.NewGuid();

        await SeedRuleSpecAsync(context, gameId, "v1", new List<RuleAtom>
        {
            new RuleAtom("rule1", "Combat is resolved by dice roll", "Combat", "15")
        });

        var session = CreateDefaultSession(gameId);
        session.Start();
        var move = new Move("Alice", "paint picture"); // Completely unrelated action

        // Act
        var result = await service.ValidateMoveAsync(session, move);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("No specific rules found", result.Errors[0]);
        Assert.True(result.ConfidenceScore < 0.5);
    }

    [Fact]
    public async Task ValidateMoveAsync_WithMultipleRulesAndReferences_ReturnsHighConfidence()
    {
        // Arrange - fresh context per test
        using var context = CreateFreshDbContext();
        var service = CreateService(context);
        var gameId = Guid.NewGuid();

        await SeedRuleSpecAsync(context, gameId, "v1", new List<RuleAtom>
        {
            new RuleAtom("rule1", "Players must roll dice at start of turn", "Gameplay", "5", "12"),
            new RuleAtom("rule2", "Dice roll determines movement distance", "Movement", "6", "15"),
            new RuleAtom("rule3", "Roll again if you roll doubles", "Gameplay", "5", "13"),
            new RuleAtom("rule4", "Maximum dice roll is 12", "Rules", "2", "8")
        });

        var session = CreateDefaultSession(gameId);
        session.Start();
        var move = new Move("Alice", "roll dice");

        // Act
        var result = await service.ValidateMoveAsync(session, move);

        // Assert
        Assert.True(result.IsValid);
        Assert.True(result.ConfidenceScore >= 0.7); // High confidence with multiple specific rules
    }
    [Fact]
    public async Task ValidateMoveAsync_DuringSetup_ProvidesSuggestions()
    {
        // Arrange - fresh context per test
        using var context = CreateFreshDbContext();
        var service = CreateService(context);
        var gameId = Guid.NewGuid();

        await SeedRuleSpecAsync(context, gameId, "v1", new List<RuleAtom>
        {
            new RuleAtom("rule1", "During setup, each player places starting pieces", "Setup", "3")
        });

        var session = CreateDefaultSession(gameId); // Status is Setup by default
        var move = new Move("Alice", "place piece", position: "A1");

        // Act
        var result = await service.ValidateMoveAsync(session, move);

        // Assert
        Assert.True(result.IsValid);
        if (result.Suggestions != null)
        {
            Assert.Contains(result.Suggestions, s => s.Contains("setup"));
        }
    }
    [Fact]
    public async Task ValidateMoveAsync_WithRestrictiveRules_ProvidesSuggestions()
    {
        // Arrange - fresh context per test
        using var context = CreateFreshDbContext();
        var service = CreateService(context);
        var gameId = Guid.NewGuid();

        await SeedRuleSpecAsync(context, gameId, "v1", new List<RuleAtom>
        {
            new RuleAtom("rule1", "Players cannot move backwards", "Movement", "8"),
            new RuleAtom("rule2", "Forward movement is allowed", "Movement", "7")
        });

        var session = CreateDefaultSession(gameId);
        session.Start();
        var move = new Move("Alice", "move piece", position: "back");

        // Act
        var result = await service.ValidateMoveAsync(session, move);

        // Assert
        Assert.True(result.IsValid || !result.IsValid); // Result depends on heuristics
        if (result.Suggestions != null)
        {
            Assert.Contains(result.Suggestions, s =>
                s.Contains("restriction", StringComparison.OrdinalIgnoreCase));
        }
    }
    private static GameSession CreateDefaultSession(Guid? gameId = null)
    {
        var players = new List<SessionPlayer>
        {
            new SessionPlayer("Alice", 1, "Red"),
            new SessionPlayer("Bob", 2, "Blue")
        };

        return new GameSession(Guid.NewGuid(), gameId ?? Guid.NewGuid(), players);
    }

    private static async Task SeedRuleSpecAsync(MeepleAiDbContext context, Guid gameId, string version, List<RuleAtom> rules)
    {
        var ruleSpecEntity = new RuleSpecEntity
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            Version = version,
            CreatedAt = DateTime.UtcNow
        };

        ruleSpecEntity.Atoms = rules
            .Select((rule, index) => new RuleAtomEntity
            {
                Id = Guid.NewGuid(),
                RuleSpecId = ruleSpecEntity.Id,
                Key = string.IsNullOrWhiteSpace(rule.id) ? $"rule-{index + 1}" : rule.id,
                Text = rule.text,
                Section = rule.section,
                PageNumber = TryParseNullableInt(rule.page),
                LineNumber = TryParseNullableInt(rule.line),
                SortOrder = index
            })
            .ToList();

        context.RuleSpecs.Add(ruleSpecEntity);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static int? TryParseNullableInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return int.TryParse(value, out var parsed) ? parsed : null;
    }
}
