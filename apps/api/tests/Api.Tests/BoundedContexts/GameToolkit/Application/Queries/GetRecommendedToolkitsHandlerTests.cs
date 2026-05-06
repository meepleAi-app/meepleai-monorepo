using Api.BoundedContexts.GameToolkit.Application.Queries.GetRecommendedToolkits;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameToolkit;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Api.Tests.BoundedContexts.GameToolkit.Application.Queries;

/// <summary>
/// Unit tests for GetRecommendedToolkitsHandler.
/// Verifies IsPublished filter and CreatedAt DESC ordering.
/// Uses InMemoryDatabase — no external dependencies.
/// Issue #728.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameToolkit")]
public sealed class GetRecommendedToolkitsHandlerTests
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GetRecommendedToolkitsHandler> _logger;
    private readonly GetRecommendedToolkitsHandler _handler;

    public GetRecommendedToolkitsHandlerTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _logger = Substitute.For<ILogger<GetRecommendedToolkitsHandler>>();
        _handler = new GetRecommendedToolkitsHandler(_dbContext, _logger);
    }

    [Fact]
    public async Task Handle_FiltersOutUnpublishedToolkits()
    {
        // Arrange — seed 3 published + 2 unpublished
        for (int i = 0; i < 3; i++)
            SeedToolkit($"Published {i}", isPublished: true, createdAt: DateTime.UtcNow.AddDays(-i));
        SeedToolkit("Unpublished 1", isPublished: false, createdAt: DateTime.UtcNow);
        SeedToolkit("Unpublished 2", isPublished: false, createdAt: DateTime.UtcNow);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(new GetRecommendedToolkitsQuery(10), CancellationToken.None);

        // Assert
        result.Should().HaveCount(3);
        result.Select(r => r.Name).Should().NotContain(n => n.StartsWith("Unpublished"));
    }

    [Fact]
    public async Task Handle_OrdersByCreatedAtDesc()
    {
        // Arrange
        SeedToolkit("Old", isPublished: true, createdAt: DateTime.UtcNow.AddDays(-30));
        SeedToolkit("Recent", isPublished: true, createdAt: DateTime.UtcNow.AddDays(-1));
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(new GetRecommendedToolkitsQuery(10), CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result.First().Name.Should().Be("Recent");
    }

    [Fact]
    public async Task Handle_EmptyState_ReturnsEmptyList()
    {
        // Act
        var result = await _handler.Handle(new GetRecommendedToolkitsQuery(10), CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    private void SeedToolkit(string name, bool isPublished, DateTime createdAt)
    {
        _dbContext.GameToolkits.Add(new GameToolkitEntity
        {
            Id = Guid.NewGuid(),
            Name = name,
            IsPublished = isPublished,
            CreatedAt = createdAt,
            UpdatedAt = DateTime.UtcNow,
            Version = 1,
            GameId = Guid.NewGuid(),
            CreatedByUserId = Guid.NewGuid(),
            RowVersion = Array.Empty<byte>()
        });
    }
}
