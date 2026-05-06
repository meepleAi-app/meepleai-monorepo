using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetNewGames;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Unit tests for GetNewGamesHandler.
/// Verifies ordering, soft-delete exclusion, and empty catalog behavior.
/// Uses InMemoryDatabase — no external dependencies.
/// Issue #728.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class GetNewGamesHandlerTests
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GetNewGamesHandler> _logger;
    private readonly GetNewGamesHandler _handler;

    public GetNewGamesHandlerTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _logger = Substitute.For<ILogger<GetNewGamesHandler>>();
        _handler = new GetNewGamesHandler(_dbContext, _logger);
    }

    [Fact]
    public async Task Handle_ReturnsLatestGamesByCreatedAtDesc()
    {
        // Arrange: 15 games, each 1 day older than the previous
        for (int i = 0; i < 15; i++)
        {
            _dbContext.SharedGames.Add(new SharedGameEntity
            {
                Id = Guid.NewGuid(),
                Title = $"Game {i}",
                CreatedAt = DateTime.UtcNow.AddDays(-i),
                CreatedBy = Guid.NewGuid(),
                IsDeleted = false
            });
        }
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(new GetNewGamesQuery(Limit: 10), CancellationToken.None);

        // Assert
        result.Should().HaveCount(10);
        result.Select(r => r.CreatedAt).Should().BeInDescendingOrder();
        result.First().Title.Should().Be("Game 0");
    }

    [Fact]
    public async Task Handle_ExcludesSoftDeletedGames()
    {
        // Arrange
        _dbContext.SharedGames.Add(new SharedGameEntity
        {
            Id = Guid.NewGuid(),
            Title = "Visible",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid(),
            IsDeleted = false
        });
        _dbContext.SharedGames.Add(new SharedGameEntity
        {
            Id = Guid.NewGuid(),
            Title = "Hidden",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid(),
            IsDeleted = true
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(new GetNewGamesQuery(10), CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result.Single().Title.Should().Be("Visible");
    }

    [Fact]
    public async Task Handle_EmptyCatalog_ReturnsEmptyList()
    {
        // Act
        var result = await _handler.Handle(new GetNewGamesQuery(10), CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }
}
