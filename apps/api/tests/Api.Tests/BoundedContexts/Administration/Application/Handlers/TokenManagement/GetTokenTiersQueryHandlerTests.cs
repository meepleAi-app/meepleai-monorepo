using Api.BoundedContexts.Administration.Application.Commands.TokenManagement;
using Api.BoundedContexts.Administration.Application.Queries.TokenManagement;
using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers.TokenManagement;

/// <summary>
/// Unit tests for GetTokenTiersQueryHandler (Issue #3787, Epic #3685)
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
[Trait("Epic", "3685")]
public sealed class GetTokenTiersQueryHandlerTests
{
    private readonly Mock<ITokenTierRepository> _mockRepository;
    private readonly GetTokenTiersQueryHandler _handler;

    public GetTokenTiersQueryHandlerTests()
    {
        _mockRepository = new Mock<ITokenTierRepository>();
        _handler = new GetTokenTiersQueryHandler(_mockRepository.Object);
    }

    [Fact]
    public async Task Handle_WithActiveTiers_ReturnsAllTiers()
    {
        // Arrange
        var tiers = new List<TokenTier>
        {
            TokenTier.CreateFreeTier(),
            TokenTier.CreateBasicTier(),
            TokenTier.CreateProTier()
        };

        _mockRepository
            .Setup(r => r.GetAllTiersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tiers);

        var query = new GetTokenTiersQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain(t => t.Name == "Free");
        result.Should().Contain(t => t.Name == "Basic");
        result.Should().Contain(t => t.Name == "Pro");
    }

    [Fact]
    public async Task Handle_WithInactiveTier_IncludesInactiveTiers()
    {
        // Arrange
        var activeTier = TokenTier.CreateFreeTier();
        var inactiveTier = TokenTier.CreateBasicTier();
        inactiveTier.Deactivate();

        var tiers = new List<TokenTier> { activeTier, inactiveTier };

        _mockRepository
            .Setup(r => r.GetAllTiersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tiers);

        var query = new GetTokenTiersQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(t => t.IsActive);
        result.Should().Contain(t => !t.IsActive);
    }

    [Fact]
    public async Task Handle_WithNoTiers_ReturnsEmptyList()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.GetAllTiersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TokenTier>());

        var query = new GetTokenTiersQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_MapsTierPropertiesCorrectly()
    {
        // Arrange
        var tier = TokenTier.CreateProTier();
        var tiers = new List<TokenTier> { tier };

        _mockRepository
            .Setup(r => r.GetAllTiersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tiers);

        var query = new GetTokenTiersQuery();

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        var dto = result.First();
        dto.Name.Should().Be("Pro");
        dto.TokensPerMonth.Should().Be(200_000);
        dto.TokensPerDay.Should().Be(10_000);
        dto.MessagesPerDay.Should().Be(200);
        dto.MonthlyPrice.Should().Be(29.99m);
        dto.IsActive.Should().BeTrue();
    }
}
