using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Application.EventHandlers;
using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.EventHandlers;

/// <summary>
/// Unit tests for ModelDeprecatedAutoFallbackHandler.
/// Issue #5501: Part of Epic #5490 - Model Versioning &amp; Availability Monitoring.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class ModelDeprecatedAutoFallbackHandlerTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<IModelCompatibilityRepository> _compatibilityRepoMock;
    private readonly Mock<INotificationRepository> _notificationRepoMock;
    private readonly ModelDeprecatedAutoFallbackHandler _handler;
    private readonly List<Notification> _capturedNotifications = new();

    public ModelDeprecatedAutoFallbackHandlerTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new MeepleAiDbContext(
            options,
            Mock.Of<IMediator>(),
            new DomainEventCollector());
        _compatibilityRepoMock = new Mock<IModelCompatibilityRepository>();
        _notificationRepoMock = new Mock<INotificationRepository>();

        _notificationRepoMock
            .Setup(r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Callback<Notification, CancellationToken>((n, _) => _capturedNotifications.Add(n))
            .Returns(Task.CompletedTask);

        _handler = new ModelDeprecatedAutoFallbackHandler(
            _dbContext,
            _compatibilityRepoMock.Object,
            _notificationRepoMock.Object,
            Mock.Of<ILogger<ModelDeprecatedAutoFallbackHandler>>());
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private async Task SeedMapping(string strategy, string primaryModel, string[] fallbackModels)
    {
        _dbContext.Set<StrategyModelMappingEntity>().Add(new StrategyModelMappingEntity
        {
            Id = Guid.NewGuid(),
            Strategy = strategy,
            PrimaryModel = primaryModel,
            FallbackModels = fallbackModels,
            Provider = "openrouter",
            IsCustomizable = true,
            AdminOnly = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await _dbContext.SaveChangesAsync();
    }

    private async Task SeedAdminUser(Guid adminId)
    {
        _dbContext.Set<UserEntity>().Add(new UserEntity
        {
            Id = adminId,
            Role = "admin",
            Email = $"admin-{adminId:N}@test.com",
            PasswordHash = "hash",
            CreatedAt = DateTime.UtcNow,
        });
        await _dbContext.SaveChangesAsync();
    }

    private static ModelDeprecatedEvent CreateEvent(
        string modelId = "deprecated-model",
        string[]? affectedStrategies = null)
    {
        return new ModelDeprecatedEvent(
            modelId,
            "openrouter",
            affectedStrategies ?? new[] { "BALANCED" },
            null,
            "Model no longer available",
            DateTime.UtcNow);
    }

    [Fact]
    public async Task Handle_WithFallbacks_SwitchesPrimaryToFirstFallback()
    {
        // Arrange
        await SeedMapping("BALANCED", "deprecated-model", new[] { "fallback-1", "fallback-2" });
        var evt = CreateEvent();

        // Act
        await _handler.Handle(evt, CancellationToken.None);

        // Assert
        var mapping = await _dbContext.Set<StrategyModelMappingEntity>()
            .FirstAsync(m => m.Strategy == "BALANCED");
        mapping.PrimaryModel.Should().Be("fallback-1");
        mapping.FallbackModels.Should().BeEquivalentTo(new[] { "fallback-2" });
    }

    [Fact]
    public async Task Handle_WithFallbacks_LogsChangeAsAutomatic()
    {
        // Arrange
        await SeedMapping("FAST", "deprecated-model", new[] { "fallback-1" });
        var evt = CreateEvent(affectedStrategies: new[] { "FAST" });

        // Act
        await _handler.Handle(evt, CancellationToken.None);

        // Assert
        _compatibilityRepoMock.Verify(
            r => r.LogChangeAsync(
                It.Is<ModelChangeLogEntry>(e =>
                    e.ModelId == "deprecated-model" &&
                    e.ChangeType == "fallback_activated" &&
                    e.PreviousModelId == "deprecated-model" &&
                    e.NewModelId == "fallback-1" &&
                    e.AffectedStrategy == "FAST" &&
                    e.IsAutomatic),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithFallbacks_NotifiesAdmins()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        await SeedAdminUser(adminId);
        await SeedMapping("BALANCED", "deprecated-model", new[] { "fallback-1" });
        var evt = CreateEvent();

        // Act
        await _handler.Handle(evt, CancellationToken.None);

        // Assert
        _capturedNotifications.Should().ContainSingle();
        var notification = _capturedNotifications[0];
        notification.UserId.Should().Be(adminId);
        notification.Type.Should().Be(NotificationType.AdminModelAutoFallback);
        notification.Severity.Should().Be(NotificationSeverity.Warning);
        notification.Title.Should().Contain("Auto-Fallback");
        notification.Title.Should().Contain("deprecated-model");
        notification.Message.Should().Contain("fallback-1");
        notification.Link.Should().Be("/admin/agents/strategy");
    }

    [Fact]
    public async Task Handle_WithFallbacks_IncludesCorrectMetadata()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        await SeedAdminUser(adminId);
        await SeedMapping("PRECISE", "deprecated-model", new[] { "new-model" });
        var evt = CreateEvent(affectedStrategies: new[] { "PRECISE" });

        // Act
        await _handler.Handle(evt, CancellationToken.None);

        // Assert
        _capturedNotifications.Should().ContainSingle();
        var metadata = JsonSerializer.Deserialize<JsonElement>(_capturedNotifications[0].Metadata!);
        metadata.GetProperty("modelId").GetString().Should().Be("deprecated-model");
        metadata.GetProperty("isAutomatic").GetBoolean().Should().BeTrue();
        metadata.GetProperty("switchedStrategies").GetArrayLength().Should().Be(1);
        var switched = metadata.GetProperty("switchedStrategies")[0];
        switched.GetProperty("strategy").GetString().Should().Be("PRECISE");
        switched.GetProperty("newModel").GetString().Should().Be("new-model");
    }

    [Fact]
    public async Task Handle_WithNoFallbacks_DoesNotSwitchOrNotify()
    {
        // Arrange — mapping has empty fallback array
        await SeedMapping("BALANCED", "deprecated-model", Array.Empty<string>());
        var adminId = Guid.NewGuid();
        await SeedAdminUser(adminId);
        var evt = CreateEvent();

        // Act
        await _handler.Handle(evt, CancellationToken.None);

        // Assert
        var mapping = await _dbContext.Set<StrategyModelMappingEntity>()
            .FirstAsync(m => m.Strategy == "BALANCED");
        mapping.PrimaryModel.Should().Be("deprecated-model"); // unchanged

        _compatibilityRepoMock.Verify(
            r => r.LogChangeAsync(It.IsAny<ModelChangeLogEntry>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _capturedNotifications.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithNoMatchingMappings_DoesNothing()
    {
        // Arrange — mapping has different primary model
        await SeedMapping("BALANCED", "other-model", new[] { "fallback-1" });
        var evt = CreateEvent();

        // Act
        await _handler.Handle(evt, CancellationToken.None);

        // Assert
        var mapping = await _dbContext.Set<StrategyModelMappingEntity>()
            .FirstAsync(m => m.Strategy == "BALANCED");
        mapping.PrimaryModel.Should().Be("other-model"); // unchanged

        _compatibilityRepoMock.Verify(
            r => r.LogChangeAsync(It.IsAny<ModelChangeLogEntry>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_MultipleStrategies_SwitchesAll()
    {
        // Arrange
        await SeedMapping("BALANCED", "deprecated-model", new[] { "fallback-a" });
        await SeedMapping("FAST", "deprecated-model", new[] { "fallback-b", "fallback-c" });
        var evt = CreateEvent(affectedStrategies: new[] { "BALANCED", "FAST" });

        // Act
        await _handler.Handle(evt, CancellationToken.None);

        // Assert
        var balanced = await _dbContext.Set<StrategyModelMappingEntity>()
            .FirstAsync(m => m.Strategy == "BALANCED");
        balanced.PrimaryModel.Should().Be("fallback-a");
        balanced.FallbackModels.Should().BeEmpty();

        var fast = await _dbContext.Set<StrategyModelMappingEntity>()
            .FirstAsync(m => m.Strategy == "FAST");
        fast.PrimaryModel.Should().Be("fallback-b");
        fast.FallbackModels.Should().BeEquivalentTo(new[] { "fallback-c" });

        _compatibilityRepoMock.Verify(
            r => r.LogChangeAsync(It.IsAny<ModelChangeLogEntry>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task Handle_OnlyMatchingStrategiesWithFallbacks_MixedScenario()
    {
        // Arrange — one with fallbacks, one without
        await SeedMapping("BALANCED", "deprecated-model", new[] { "fallback-1" });
        await SeedMapping("FAST", "deprecated-model", Array.Empty<string>());
        var evt = CreateEvent(affectedStrategies: new[] { "BALANCED", "FAST" });

        // Act
        await _handler.Handle(evt, CancellationToken.None);

        // Assert — only BALANCED switched
        var balanced = await _dbContext.Set<StrategyModelMappingEntity>()
            .FirstAsync(m => m.Strategy == "BALANCED");
        balanced.PrimaryModel.Should().Be("fallback-1");

        var fast = await _dbContext.Set<StrategyModelMappingEntity>()
            .FirstAsync(m => m.Strategy == "FAST");
        fast.PrimaryModel.Should().Be("deprecated-model"); // unchanged

        _compatibilityRepoMock.Verify(
            r => r.LogChangeAsync(It.IsAny<ModelChangeLogEntry>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_UnrelatedStrategyWithSameModel_NotSwitched()
    {
        // Arrange — PRECISE uses same model but is NOT in affectedStrategies
        await SeedMapping("BALANCED", "deprecated-model", new[] { "fallback-1" });
        await SeedMapping("PRECISE", "deprecated-model", new[] { "fallback-2" });
        var evt = CreateEvent(affectedStrategies: new[] { "BALANCED" }); // only BALANCED

        // Act
        await _handler.Handle(evt, CancellationToken.None);

        // Assert — only BALANCED switched, PRECISE left untouched
        var balanced = await _dbContext.Set<StrategyModelMappingEntity>()
            .FirstAsync(m => m.Strategy == "BALANCED");
        balanced.PrimaryModel.Should().Be("fallback-1");

        var precise = await _dbContext.Set<StrategyModelMappingEntity>()
            .FirstAsync(m => m.Strategy == "PRECISE");
        precise.PrimaryModel.Should().Be("deprecated-model"); // unchanged

        _compatibilityRepoMock.Verify(
            r => r.LogChangeAsync(It.IsAny<ModelChangeLogEntry>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenExceptionOccurs_SwallowsAndDoesNotThrow()
    {
        // Arrange — make LogChangeAsync throw
        await SeedMapping("BALANCED", "deprecated-model", new[] { "fallback-1" });
        _compatibilityRepoMock
            .Setup(r => r.LogChangeAsync(It.IsAny<ModelChangeLogEntry>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB failure"));

        var evt = CreateEvent();

        // Act — should not throw
        var act = () => _handler.Handle(evt, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }
}
