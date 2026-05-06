using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetTopAgents;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Unit tests for GetTopAgentsHandler.
/// Verifies ranking by distinct user count and empty state behavior.
/// Uses InMemoryDatabase — no external dependencies.
/// Issue #728.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class GetTopAgentsHandlerTests
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GetTopAgentsHandler> _logger;
    private readonly GetTopAgentsHandler _handler;

    public GetTopAgentsHandlerTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _logger = Substitute.For<ILogger<GetTopAgentsHandler>>();
        _handler = new GetTopAgentsHandler(_dbContext, _logger);
    }

    [Fact]
    public async Task Handle_RanksByDistinctUserCount()
    {
        // Arrange
        var agentA = Guid.NewGuid();
        var agentB = Guid.NewGuid();

        // agentA: 5 distinct users
        for (int i = 0; i < 5; i++)
            SeedAgentSession(agentDefinitionId: agentA, userId: Guid.NewGuid());

        // agentB: 1 distinct user (3 sessions)
        var sharedUserB = Guid.NewGuid();
        for (int i = 0; i < 3; i++)
            SeedAgentSession(agentDefinitionId: agentB, userId: sharedUserB);

        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(new GetTopAgentsQuery(10), CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result.First().Id.Should().Be(agentA);
        result.First().InstallCount.Should().Be(5);
        result.Last().Id.Should().Be(agentB);
        result.Last().InstallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_EmptyState_ReturnsEmptyList()
    {
        // Act
        var result = await _handler.Handle(new GetTopAgentsQuery(10), CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    private void SeedAgentSession(Guid agentDefinitionId, Guid userId)
    {
        _dbContext.AgentSessions.Add(new AgentSessionEntity
        {
            Id = Guid.NewGuid(),
            AgentDefinitionId = agentDefinitionId,
            UserId = userId,
            GameSessionId = Guid.NewGuid(),
            GameId = Guid.NewGuid(),
            CurrentGameStateJson = "{}",
            IsActive = true,
            StartedAt = DateTime.UtcNow
        });
    }
}
