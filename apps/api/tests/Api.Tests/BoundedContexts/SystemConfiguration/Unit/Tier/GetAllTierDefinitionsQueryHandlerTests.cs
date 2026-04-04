using Api.BoundedContexts.SystemConfiguration.Application.Queries.Tier;
using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Unit.Tier;

/// <summary>
/// Tests for GetAllTierDefinitionsQueryHandler.
/// E2-1: Admin Tier CRUD Endpoints.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SystemConfiguration")]
public sealed class GetAllTierDefinitionsQueryHandlerTests : IDisposable
{
    private readonly Api.Infrastructure.MeepleAiDbContext _db;
    private readonly GetAllTierDefinitionsQueryHandler _handler;

    public GetAllTierDefinitionsQueryHandlerTests()
    {
        _db = TestDbContextFactory.CreateInMemoryDbContext();
        _handler = new GetAllTierDefinitionsQueryHandler(_db);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Handle_WhenNoTiersExist_ReturnsEmptyList()
    {
        // Act
        var result = await _handler.Handle(
            new GetAllTierDefinitionsQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenTiersExist_ReturnsAllTiersSortedByName()
    {
        // Arrange
        var premium = TierDefinition.Create("premium", "Premium Tier", TierLimits.PremiumTier, "standard");
        var free = TierDefinition.Create("free", "Free Tier", TierLimits.FreeTier, "free", isDefault: true);
        _db.TierDefinitions.AddRange(premium, free);
        await _db.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(
            new GetAllTierDefinitionsQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Should().HaveCount(2);
        result[0].Name.Should().Be("free");
        result[1].Name.Should().Be("premium");
    }

    [Fact]
    public async Task Handle_ReturnsDtoWithCorrectProperties()
    {
        // Arrange
        var tier = TierDefinition.Create("free", "Free Tier", TierLimits.FreeTier, "free", isDefault: true);
        _db.TierDefinitions.Add(tier);
        await _db.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(
            new GetAllTierDefinitionsQuery(), TestContext.Current.CancellationToken);

        // Assert
        var dto = result.Single();
        dto.Id.Should().Be(tier.Id);
        dto.Name.Should().Be("free");
        dto.DisplayName.Should().Be("Free Tier");
        dto.LlmModelTier.Should().Be("free");
        dto.IsDefault.Should().BeTrue();
        dto.Limits.MaxPrivateGames.Should().Be(3);
        dto.Limits.MaxPdfUploadsPerMonth.Should().Be(3);
        dto.Limits.SessionSaveEnabled.Should().BeFalse();
    }
}
