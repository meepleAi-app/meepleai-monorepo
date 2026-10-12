using Api.BoundedContexts.SystemConfiguration.Application.Commands.Tier;
using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
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
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly UpdateTierDefinitionCommandHandler _handler;

    public UpdateTierDefinitionCommandHandlerTests()
    {
        _db = TestDbContextFactory.CreateInMemoryDbContext();
        _handler = new UpdateTierDefinitionCommandHandler(_db, _cache);
    }

    public void Dispose()
    {
        _cache.Dispose();
        _db.Dispose();
    }

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

    /// <summary>
    /// Issue #4212: the context defaults to NoTracking (#3866), and the handler loaded the plan
    /// without AsTracking, so SaveChanges wrote nothing: every admin edit of a plan answered with
    /// the new values and was lost. The other tests here read the returned DTO, built from the
    /// mutated object in memory, and could not see it. This one reads the database back.
    /// </summary>
    [Fact]
    public async Task Handle_PersistsTheNewLimits()
    {
        var tier = TierDefinition.Create("free", "Free", TierLimits.FreeTier, "free");
        _db.TierDefinitions.Add(tier);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        _db.ChangeTracker.Clear();

        var raised = TierLimitsDto.FromValueObject(TierLimits.FreeTier with { MaxPrivateGames = 9 });
        await _handler.Handle(
            new UpdateTierDefinitionCommand("free", null, raised, null, null),
            TestContext.Current.CancellationToken);
        _db.ChangeTracker.Clear();

        var stored = await _db.TierDefinitions
            .SingleAsync(t => t.Name == "free", TestContext.Current.CancellationToken);
        stored.Limits.MaxPrivateGames.Should().Be(9);
    }

    /// <summary>
    /// Issue #4212: the limits DTO did not carry RaptorRebuildEnabled, so every admin save of a
    /// plan rebuilt the limits with the default (false) and silently took RAPTOR away from premium.
    /// </summary>
    [Fact]
    public async Task Handle_SavingTheLimitsOfAPlan_KeepsRaptorRebuildEnabled()
    {
        var tier = TierDefinition.Create("premium", "Premium", TierLimits.PremiumTier, "standard");
        _db.TierDefinitions.Add(tier);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sameLimits = TierLimitsDto.FromValueObject(TierLimits.PremiumTier);
        var result = await _handler.Handle(
            new UpdateTierDefinitionCommand("premium", null, sameLimits, null, null),
            TestContext.Current.CancellationToken);

        result.Limits.RaptorRebuildEnabled.Should().BeTrue();
        tier.Limits.RaptorRebuildEnabled.Should().BeTrue();
        tier.Limits.Should().Be(TierLimits.PremiumTier, "saving unchanged limits must change nothing");
    }

    /// <summary>
    /// Issue #4212: the plan is cached for five minutes by <c>TierEnforcementService</c>. An admin
    /// edit evicts it, otherwise the new limits would apply up to five minutes later.
    /// </summary>
    [Fact]
    public async Task Handle_EvictsTheCachedPlan()
    {
        var tier = TierDefinition.Create("free", "Free", TierLimits.FreeTier, "free");
        _db.TierDefinitions.Add(tier);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var key = Api.SharedKernel.Services.TierEnforcementService.TierDefinitionCacheKey("free");
        _cache.Set(key, tier);

        await _handler.Handle(
            new UpdateTierDefinitionCommand("free", null, null, "standard", null),
            TestContext.Current.CancellationToken);

        _cache.TryGetValue(key, out _).Should().BeFalse();
    }
}
