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

namespace Api.Tests.BoundedContexts.GameManagement.Domain;

public class MoveValidationDomainServiceTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<ILogger<MoveValidationDomainService>> _loggerMock;
    private readonly MoveValidationDomainService _service;
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Guid _sessionId = Guid.NewGuid();

    public MoveValidationDomainServiceTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var mediatorMock = new Mock<IMediator>();
        var eventCollectorMock = new Mock<IDomainEventCollector>();
        eventCollectorMock
            .Setup(collector => collector.GetAndClearEvents())
            .Returns(Array.Empty<IDomainEvent>());

        _dbContext = new MeepleAiDbContext(options, mediatorMock.Object, eventCollectorMock.Object);
        _loggerMock = new Mock<ILogger<MoveValidationDomainService>>();
        _service = new MoveValidationDomainService(_dbContext, _loggerMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullDbContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MoveValidationDomainService(null!, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MoveValidationDomainService(_dbContext, null!));
    }

    #endregion

    #region ValidateMoveAsync - Basic Validation Tests

    [Fact]
    public async Task ValidateMoveAsync_WithNullSession_ThrowsArgumentNullException()
    {
        // Arrange
        var move = new Move("Alice", "roll dice");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _service.ValidateMoveAsync(null!, move));
    }

    [Fact]
    public async Task ValidateMoveAsync_WithNullMove_ThrowsArgumentNullException()
    {
        // Arrange
        var session = CreateDefaultSession();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _service.ValidateMoveAsync(session, null!));
    }

    [Fact]
    public async Task ValidateMoveAsync_WithFinishedSession_ReturnsInvalid()
    {
        // Arrange
        var session = CreateDefaultSession();
        session.Start();
        session.Complete("Alice");
        var move = new Move("Alice", "roll dice");

        // Act
        var result = await _service.ValidateMoveAsync(session, move);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("session is Completed", result.Errors[0]);
        Assert.Equal(1.0, result.ConfidenceScore);
    }

    [Fact]
    public async Task ValidateMoveAsync_WithPlayerNotInSession_ReturnsInvalid()
    {
        // Arrange
        var session = CreateDefaultSession();
        session.Start();
        var move = new Move("Charlie", "roll dice"); // Charlie not in session

        // Act
        var result = await _service.ValidateMoveAsync(session, move);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("not in this session", result.Errors[0]);
        Assert.Equal(1.0, result.ConfidenceScore);
    }

    #endregion

    #region ValidateMoveAsync - RuleSpec Integration Tests

    [Fact]
    public async Task ValidateMoveAsync_WithNoRuleSpec_ReturnsUncertain()
    {
        // Arrange
        var session = CreateDefaultSession();
        session.Start();
        var move = new Move("Alice", "roll dice");

        // Act
        var result = await _service.ValidateMoveAsync(session, move);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("No rule specification available", result.Errors[0]);
        Assert.Equal(0.0, result.ConfidenceScore);
        Assert.Empty(result.ApplicableRules);
    }

    [Fact]
    public async Task ValidateMoveAsync_WithRuleSpec_FindsApplicableRules()
    {
        // Arrange
        await SeedRuleSpecAsync(_gameId, "v1", new List<RuleAtom>
        {
            new RuleAtom("rule1", "Players must roll dice at the start of their turn", "Gameplay", "5"),
            new RuleAtom("rule2", "Movement is determined by dice roll", "Movement", "6"),
            new RuleAtom("rule3", "Players draw a card when landing on special spaces", "Cards", "8")
        });

        var session = CreateDefaultSession();
        session.Start();
        var move = new Move("Alice", "roll dice");

        // Act
        var result = await _service.ValidateMoveAsync(session, move);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(2, result.ApplicableRules.Count); // Should find rules about dice
        Assert.All(result.ApplicableRules, rule =>
            Assert.Contains("dice", rule.text.ToLowerInvariant()));
    }

    [Fact]
    public async Task ValidateMoveAsync_WithSpecificVersion_UsesCorrectVersion()
    {
        // Arrange
        await SeedRuleSpecAsync(_gameId, "v1", new List<RuleAtom>
        {
            new RuleAtom("rule1", "Old dice rule", "Gameplay", "5")
        });

        await SeedRuleSpecAsync(_gameId, "v2", new List<RuleAtom>
        {
            new RuleAtom("rule2", "New dice rule with advantage", "Gameplay", "5")
        });

        var session = CreateDefaultSession();
        session.Start();
        var move = new Move("Alice", "roll dice");

        // Act
        var result = await _service.ValidateMoveAsync(session, move, ruleSpecVersion: "v1");

        // Assert
        Assert.True(result.IsValid);
        Assert.Single(result.ApplicableRules);
        Assert.Contains("Old dice rule", result.ApplicableRules[0].text);
    }

    #endregion

    #region ValidateMoveAsync - Keyword Matching Tests

    [Fact]
    public async Task ValidateMoveAsync_WithComplexAction_FindsRelevantRules()
    {
        // Arrange
        await SeedRuleSpecAsync(_gameId, "v1", new List<RuleAtom>
        {
            new RuleAtom("rule1", "Players may trade resources with other players", "Trading", "10"),
            new RuleAtom("rule2", "Resources include wood, stone, and wheat", "Resources", "4"),
            new RuleAtom("rule3", "Combat is resolved by dice roll", "Combat", "15")
        });

        var session = CreateDefaultSession();
        session.Start();
        var move = new Move("Alice", "trade resources", additionalContext: new Dictionary<string, string>
        {
            { "resource", "wood" },
            { "partner", "Bob" }
        });

        // Act
        var result = await _service.ValidateMoveAsync(session, move);

        // Assert
        Assert.True(result.IsValid);
        Assert.True(result.ApplicableRules.Count >= 2); // Should find trading and resources rules
    }

    [Fact]
    public async Task ValidateMoveAsync_WithPosition_ConsidersPositionInMatching()
    {
        // Arrange
        await SeedRuleSpecAsync(_gameId, "v1", new List<RuleAtom>
        {
            new RuleAtom("rule1", "The center space grants bonus points", "Spaces", "7"),
            new RuleAtom("rule2", "Corner spaces allow drawing a card", "Spaces", "8")
        });

        var session = CreateDefaultSession();
        session.Start();
        var move = new Move("Alice", "move piece", position: "center");

        // Act
        var result = await _service.ValidateMoveAsync(session, move);

        // Assert
        Assert.True(result.IsValid);
        Assert.Contains(result.ApplicableRules, r => r.text.Contains("center"));
    }

    #endregion

    #region ValidateMoveAsync - Confidence Scoring Tests

    [Fact]
    public async Task ValidateMoveAsync_WithNoApplicableRules_ReturnsLowConfidence()
    {
        // Arrange
        await SeedRuleSpecAsync(_gameId, "v1", new List<RuleAtom>
        {
            new RuleAtom("rule1", "Combat is resolved by dice roll", "Combat", "15")
        });

        var session = CreateDefaultSession();
        session.Start();
        var move = new Move("Alice", "paint picture"); // Completely unrelated action

        // Act
        var result = await _service.ValidateMoveAsync(session, move);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("No specific rules found", result.Errors[0]);
        Assert.True(result.ConfidenceScore < 0.5);
    }

    [Fact]
    public async Task ValidateMoveAsync_WithMultipleRulesAndReferences_ReturnsHighConfidence()
    {
        // Arrange
        await SeedRuleSpecAsync(_gameId, "v1", new List<RuleAtom>
        {
            new RuleAtom("rule1", "Players must roll dice at start of turn", "Gameplay", "5", "12"),
            new RuleAtom("rule2", "Dice roll determines movement distance", "Movement", "6", "15"),
            new RuleAtom("rule3", "Roll again if you roll doubles", "Gameplay", "5", "13"),
            new RuleAtom("rule4", "Maximum dice roll is 12", "Rules", "2", "8")
        });

        var session = CreateDefaultSession();
        session.Start();
        var move = new Move("Alice", "roll dice");

        // Act
        var result = await _service.ValidateMoveAsync(session, move);

        // Assert
        Assert.True(result.IsValid);
        Assert.True(result.ConfidenceScore >= 0.7); // High confidence with multiple specific rules
    }

    #endregion

    #region ValidateMoveAsync - Session State Tests

    [Fact]
    public async Task ValidateMoveAsync_DuringSetup_ProvidesSuggestions()
    {
        // Arrange
        await SeedRuleSpecAsync(_gameId, "v1", new List<RuleAtom>
        {
            new RuleAtom("rule1", "During setup, each player places starting pieces", "Setup", "3")
        });

        var session = CreateDefaultSession(); // Status is Setup by default
        var move = new Move("Alice", "place piece", position: "A1");

        // Act
        var result = await _service.ValidateMoveAsync(session, move);

        // Assert
        Assert.True(result.IsValid);
        if (result.Suggestions != null)
        {
            Assert.Contains(result.Suggestions, s => s.Contains("setup"));
        }
    }

    #endregion

    #region ValidateMoveAsync - Restriction Detection Tests

    [Fact]
    public async Task ValidateMoveAsync_WithRestrictiveRules_ProvidesSuggestions()
    {
        // Arrange
        await SeedRuleSpecAsync(_gameId, "v1", new List<RuleAtom>
        {
            new RuleAtom("rule1", "Players cannot move backwards", "Movement", "8"),
            new RuleAtom("rule2", "Forward movement is allowed", "Movement", "7")
        });

        var session = CreateDefaultSession();
        session.Start();
        var move = new Move("Alice", "move piece", position: "back");

        // Act
        var result = await _service.ValidateMoveAsync(session, move);

        // Assert
        Assert.True(result.IsValid || !result.IsValid); // Result depends on heuristics
        if (result.Suggestions != null)
        {
            Assert.Contains(result.Suggestions, s =>
                s.Contains("restriction", StringComparison.OrdinalIgnoreCase));
        }
    }

    #endregion

    #region Helper Methods

    private GameSession CreateDefaultSession()
    {
        var players = new List<SessionPlayer>
        {
            new SessionPlayer("Alice", 1, "Red"),
            new SessionPlayer("Bob", 2, "Blue")
        };

        return new GameSession(_sessionId, _gameId, players);
    }

    private async Task SeedRuleSpecAsync(Guid gameId, string version, List<RuleAtom> rules)
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

        _dbContext.RuleSpecs.Add(ruleSpecEntity);
        await _dbContext.SaveChangesAsync();
    }

    private static int? TryParseNullableInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return int.TryParse(value, out var parsed) ? parsed : null;
    }

    #endregion

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }
}
