using Api.BoundedContexts.SystemConfiguration.Application.Commands.Tier;
using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Unit.Tier;

/// <summary>
/// Tests for UpdateTierDefinitionCommandHandler.
/// E2-1: Admin Tier CRUD Endpoints.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SystemConfiguration")]
public sealed class UpdateTierDefinitionCommandHandlerTests : IDisposable
{
    private readonly Api.Infrastructure.MeepleAiDbContext _db;
    private readonly UpdateTierDefinitionCommandHandler _handler;

    public UpdateTierDefinitionCommandHandlerTests()
    {
        _db = TestDbContextFactory.CreateInMemoryDbContext();
        _handler = new UpdateTierDefinitionCommandHandler(_db);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Handle_WithNewLimits_UpdatesLimitsAndReturnsDto()
    {
        // Arrange
        var tier = TierDefinition.Create("free", "Free Tier", TierLimits.FreeTier, "free");
        _db.TierDefinitions.Add(tier);
        await _db.SaveChangesAsync();

        var newLimits = TierLimitsDto.FromValueObject(TierLimits.PremiumTier);
        var command = new UpdateTierDefinitionCommand("free", null, newLimits, null, null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Name.Should().Be("free");
        result.Limits.MaxPrivateGames.Should().Be(15);
        result.Limits.MaxPdfUploadsPerMonth.Should().Be(15);
        result.Limits.SessionSaveEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithNewLlmModelTier_UpdatesModelTier()
    {
        // Arrange
        var tier = TierDefinition.Create("free", "Free Tier", TierLimits.FreeTier, "free");
        _db.TierDefinitions.Add(tier);
        await _db.SaveChangesAsync();

        var command = new UpdateTierDefinitionCommand("free", null, null, "standard", null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.LlmModelTier.Should().Be("standard");
    }

    [Fact]
    public async Task Handle_WithIsDefault_UpdatesDefaultStatus()
    {
        // Arrange
        var tier = TierDefinition.Create("free", "Free Tier", TierLimits.FreeTier, "free");
        _db.TierDefinitions.Add(tier);
        await _db.SaveChangesAsync();

        var command = new UpdateTierDefinitionCommand("free", null, null, null, true);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithNonExistentTier_ThrowsKeyNotFoundException()
    {
        // Arrange
        var command = new UpdateTierDefinitionCommand("nonexistent", null, null, "standard", null);

        // Act
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task Handle_WithAllFieldsNull_ReturnsUnchangedTier()
    {
        // Arrange
        var tier = TierDefinition.Create("free", "Free Tier", TierLimits.FreeTier, "free");
        _db.TierDefinitions.Add(tier);
        await _db.SaveChangesAsync();

        var command = new UpdateTierDefinitionCommand("free", null, null, null, null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Name.Should().Be("free");
        result.LlmModelTier.Should().Be("free");
        result.IsDefault.Should().BeFalse();
        result.Limits.MaxPrivateGames.Should().Be(3);
    }

    [Fact]
    public async Task Handle_NormalizesNameToLowercase()
    {
        // Arrange
        var tier = TierDefinition.Create("premium", "Premium", TierLimits.PremiumTier, "standard");
        _db.TierDefinitions.Add(tier);
        await _db.SaveChangesAsync();

        var command = new UpdateTierDefinitionCommand("PREMIUM", null, null, "premium", null);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.LlmModelTier.Should().Be("premium");
    }
}
