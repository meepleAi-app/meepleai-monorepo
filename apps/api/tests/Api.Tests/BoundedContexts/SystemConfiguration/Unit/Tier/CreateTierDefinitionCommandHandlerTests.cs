using Api.BoundedContexts.SystemConfiguration.Application.Commands.Tier;
using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Unit.Tier;

/// <summary>
/// Tests for CreateTierDefinitionCommandHandler.
/// E2-1: Admin Tier CRUD Endpoints.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SystemConfiguration")]
public sealed class CreateTierDefinitionCommandHandlerTests : IDisposable
{
    private readonly Api.Infrastructure.MeepleAiDbContext _db;
    private readonly CreateTierDefinitionCommandHandler _handler;

    public CreateTierDefinitionCommandHandlerTests()
    {
        _db = TestDbContextFactory.CreateInMemoryDbContext();
        _handler = new CreateTierDefinitionCommandHandler(_db);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Handle_WithValidCommand_CreatesTierAndReturnsDto()
    {
        // Arrange
        var limitsDto = TierLimitsDto.FromValueObject(TierLimits.FreeTier);
        var command = new CreateTierDefinitionCommand("free", "Free Tier", limitsDto, "free", true);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("free");
        result.DisplayName.Should().Be("Free Tier");
        result.LlmModelTier.Should().Be("free");
        result.IsDefault.Should().BeTrue();
        result.Id.Should().NotBeEmpty();
        result.Limits.MaxPrivateGames.Should().Be(3);

        // Verify persisted
        var persisted = await _db.TierDefinitions.FirstOrDefaultAsync(t => t.Name == "free");
        persisted.Should().NotBeNull();
        persisted!.DisplayName.Should().Be("Free Tier");
    }

    [Fact]
    public async Task Handle_WithDuplicateName_ThrowsInvalidOperationException()
    {
        // Arrange
        var existing = TierDefinition.Create("free", "Free Tier", TierLimits.FreeTier, "free");
        _db.TierDefinitions.Add(existing);
        await _db.SaveChangesAsync();

        var limitsDto = TierLimitsDto.FromValueObject(TierLimits.FreeTier);
        var command = new CreateTierDefinitionCommand("free", "Another Free", limitsDto, "free");

        // Act
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task Handle_NormalizesNameToLowercase()
    {
        // Arrange
        var limitsDto = TierLimitsDto.FromValueObject(TierLimits.PremiumTier);
        var command = new CreateTierDefinitionCommand("PREMIUM", "Premium Tier", limitsDto, "standard");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Name.Should().Be("premium");
    }

    [Fact]
    public async Task Handle_WithCaseDuplicateName_ThrowsInvalidOperationException()
    {
        // Arrange — seed lowercase
        var existing = TierDefinition.Create("free", "Free Tier", TierLimits.FreeTier, "free");
        _db.TierDefinitions.Add(existing);
        await _db.SaveChangesAsync();

        // Attempt to create with uppercase variant
        var limitsDto = TierLimitsDto.FromValueObject(TierLimits.FreeTier);
        var command = new CreateTierDefinitionCommand("FREE", "Free Again", limitsDto, "free");

        // Act
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }
}
