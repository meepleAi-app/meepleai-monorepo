using Api.BoundedContexts.GameManagement.Application.Handlers;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.Infrastructure.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Queries;

/// <summary>
/// Comprehensive tests for GetRuleSpecsQueryHandler.
/// Tests rule specification retrieval for games.
/// Note: Uses in-memory database since RuleSpec is in infrastructure layer.
/// ISSUE-1500: TEST-002 - Fixed test isolation (fresh context per test)
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetRuleSpecsQueryHandlerTests
{
    /// <summary>
    /// Creates a fresh DbContext for each test to ensure complete isolation
    /// </summary>
    private static MeepleAiDbContext CreateFreshDbContext()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var mockMediator = new Mock<IMediator>();
        var mockEventCollector = new Mock<IDomainEventCollector>();
        mockEventCollector.Setup(x => x.GetAndClearEvents())
            .Returns(new List<Api.SharedKernel.Domain.Interfaces.IDomainEvent>().AsReadOnly());

        return new MeepleAiDbContext(options, mockMediator.Object, mockEventCollector.Object);
    }

    /// <summary>
    /// Creates a GetRuleSpecsQueryHandler instance with the given context
    /// </summary>
    private static GetRuleSpecsQueryHandler CreateHandler(MeepleAiDbContext context)
    {
        return new GetRuleSpecsQueryHandler(context);
    }
    [Fact]
    public async Task Handle_WithExistingRuleSpecs_ReturnsOrderedByCreatedAt()
    {
        // Arrange - fresh context per test
        using var context = CreateFreshDbContext();
        var handler = CreateHandler(context);
        var gameId = Guid.NewGuid();
        var ruleSpec1 = CreateRuleSpec(gameId, "1.0", DateTime.UtcNow.AddDays(-7));
        var ruleSpec2 = CreateRuleSpec(gameId, "1.1", DateTime.UtcNow.AddDays(-3));
        var ruleSpec3 = CreateRuleSpec(gameId, "2.0", DateTime.UtcNow);

        await context.RuleSpecs.AddRangeAsync(ruleSpec1, ruleSpec2, ruleSpec3);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var query = new GetRuleSpecsQuery(gameId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Count);
        Assert.Equal("2.0", result[0].Version); // Most recent first
        Assert.Equal("1.1", result[1].Version);
        Assert.Equal("1.0", result[2].Version);
    }

    [Fact]
    public async Task Handle_WithRuleAtoms_IncludesAtoms()
    {
        // Arrange - fresh context per test
        using var context = CreateFreshDbContext();
        var handler = CreateHandler(context);
        var gameId = Guid.NewGuid();
        var ruleSpec = CreateRuleSpec(gameId, "1.0", DateTime.UtcNow);

        var atom1 = new RuleAtomEntity
        {
            Id = Guid.NewGuid(),
            RuleSpecId = ruleSpec.Id,
            Key = "rule-1",
            Text = "Setup: Each player draws 5 cards",
            Section = "Setup",
            PageNumber = 1,
            LineNumber = 10,
            SortOrder = 1
        };

        var atom2 = new RuleAtomEntity
        {
            Id = Guid.NewGuid(),
            RuleSpecId = ruleSpec.Id,
            Key = "rule-2",
            Text = "Gameplay: Take 1 action per turn",
            Section = "Gameplay",
            PageNumber = 2,
            LineNumber = 5,
            SortOrder = 2
        };

        ruleSpec.Atoms = new List<RuleAtomEntity> { atom1, atom2 };

        await context.RuleSpecs.AddAsync(ruleSpec);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var query = new GetRuleSpecsQuery(gameId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);

        var spec = result[0];
        Assert.Equal(2, spec.Atoms.Count);

        // Atoms should be ordered by SortOrder
        Assert.Equal("rule-1", spec.Atoms[0].Id);
        Assert.Equal("Setup: Each player draws 5 cards", spec.Atoms[0].Text);
        Assert.Equal("Setup", spec.Atoms[0].Section);
        Assert.Equal("1", spec.Atoms[0].Page);
        Assert.Equal("10", spec.Atoms[0].Line);

        Assert.Equal("rule-2", spec.Atoms[1].Id);
        Assert.Equal("Gameplay: Take 1 action per turn", spec.Atoms[1].Text);
    }

    [Fact]
    public async Task Handle_WithNoRuleSpecs_ReturnsEmptyList()
    {
        // Arrange - fresh context per test
        using var context = CreateFreshDbContext();
        var handler = CreateHandler(context);
        var gameId = Guid.NewGuid();
        var query = new GetRuleSpecsQuery(gameId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task Handle_WithMultipleGames_FiltersCorrectly()
    {
        // Arrange - fresh context per test
        using var context = CreateFreshDbContext();
        var handler = CreateHandler(context);
        var gameId1 = Guid.NewGuid();
        var gameId2 = Guid.NewGuid();

        var ruleSpec1 = CreateRuleSpec(gameId1, "1.0", DateTime.UtcNow);
        var ruleSpec2 = CreateRuleSpec(gameId2, "1.0", DateTime.UtcNow);

        await context.RuleSpecs.AddRangeAsync(ruleSpec1, ruleSpec2);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var query = new GetRuleSpecsQuery(gameId1);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(gameId1, result[0].GameId);
    }

    [Fact]
    public async Task Handle_WithVersionHierarchy_IncludesParentId()
    {
        // Arrange - fresh context per test
        using var context = CreateFreshDbContext();
        var handler = CreateHandler(context);
        var gameId = Guid.NewGuid();
        var parentSpec = CreateRuleSpec(gameId, "1.0", DateTime.UtcNow.AddDays(-7));
        await context.RuleSpecs.AddAsync(parentSpec);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var childSpec = CreateRuleSpec(gameId, "1.1", DateTime.UtcNow);
        childSpec.ParentVersionId = parentSpec.Id;
        await context.RuleSpecs.AddAsync(childSpec);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var query = new GetRuleSpecsQuery(gameId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);

        var child = result.First(r => r.Version == "1.1");
        Assert.Equal(parentSpec.Id, child.ParentVersionId);

        var parent = result.First(r => r.Version == "1.0");
        Assert.Null(parent.ParentVersionId);
    }
    [Fact]
    public async Task Handle_WithCancellationToken_Cancels()
    {
        // Arrange - fresh context per test
        using var context = CreateFreshDbContext();
        var handler = CreateHandler(context);
        var gameId = Guid.NewGuid();
        var query = new GetRuleSpecsQuery(gameId);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await handler.Handle(query, cts.Token);
        });
    }

    [Fact]
    public async Task Handle_WithNullAtomFields_HandlesGracefully()
    {
        // Arrange - fresh context per test
        using var context = CreateFreshDbContext();
        var handler = CreateHandler(context);
        var gameId = Guid.NewGuid();
        var ruleSpec = CreateRuleSpec(gameId, "1.0", DateTime.UtcNow);

        var atom = new RuleAtomEntity
        {
            Id = Guid.NewGuid(),
            RuleSpecId = ruleSpec.Id,
            Key = "rule-1",
            Text = "Rule text",
            Section = null, // Nullable fields
            PageNumber = null,
            LineNumber = null,
            SortOrder = 1
        };

        ruleSpec.Atoms = new List<RuleAtomEntity> { atom };

        await context.RuleSpecs.AddAsync(ruleSpec);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var query = new GetRuleSpecsQuery(gameId);

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);

        var atomDto = result[0].Atoms[0];
        Assert.Null(atomDto.Section);
        Assert.Null(atomDto.Page);
        Assert.Null(atomDto.Line);
    }
    private static RuleSpecEntity CreateRuleSpec(Guid gameId, string version, DateTime createdAt)
    {
        return new RuleSpecEntity
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            Version = version,
            CreatedAt = createdAt,
            CreatedByUserId = null,
            ParentVersionId = null,
            Atoms = new List<RuleAtomEntity>()
        };
    }
}

