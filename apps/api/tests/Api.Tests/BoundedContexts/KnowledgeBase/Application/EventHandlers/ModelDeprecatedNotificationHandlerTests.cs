using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Application.EventHandlers;
using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
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
/// Unit tests for ModelDeprecatedNotificationHandler.
/// Issue #5499: Part of Epic #5490 - Model Versioning &amp; Availability Monitoring.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class ModelDeprecatedNotificationHandlerTests : IDisposable
{
    private readonly Mock<INotificationRepository> _notificationRepoMock;
    private readonly MeepleAiDbContext _dbContext;
    private readonly ModelDeprecatedNotificationHandler _handler;
    private readonly List<Notification> _capturedNotifications = new();

    public ModelDeprecatedNotificationHandlerTests()
    {
        _notificationRepoMock = new Mock<INotificationRepository>();

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new MeepleAiDbContext(
            options,
            Mock.Of<IMediator>(),
            new DomainEventCollector());

        _notificationRepoMock
            .Setup(r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Callback<Notification, CancellationToken>((n, _) => _capturedNotifications.Add(n))
            .Returns(Task.CompletedTask);

        _handler = new ModelDeprecatedNotificationHandler(
            _notificationRepoMock.Object,
            _dbContext,
            Mock.Of<ILogger<ModelDeprecatedNotificationHandler>>());
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private async Task SeedAdminUsers(params Guid[] adminIds)
    {
        foreach (var id in adminIds)
        {
            _dbContext.Set<UserEntity>().Add(new UserEntity
            {
                Id = id,
                Role = "admin",
                Email = $"admin-{id:N}@test.com",
                PasswordHash = "hash",
                CreatedAt = DateTime.UtcNow,
            });
        }

        // Also add a regular user to make sure it's excluded
        _dbContext.Set<UserEntity>().Add(new UserEntity
        {
            Id = Guid.NewGuid(),
            Role = "user",
            Email = "user@test.com",
            PasswordHash = "hash",
            CreatedAt = DateTime.UtcNow,
        });

        await _dbContext.SaveChangesAsync();
    }

    private static ModelDeprecatedEvent CreateEvent(
        string modelId = "meta-llama/llama-3.3-70b-instruct:free",
        string provider = "openrouter",
        string[]? affectedStrategies = null,
        string? suggestedReplacement = "qwen/qwen-2.5-72b:free",
        string reason = "Model no longer available on OpenRouter")
    {
        return new ModelDeprecatedEvent(
            modelId,
            provider,
            affectedStrategies ?? new[] { "BALANCED" },
            suggestedReplacement,
            reason,
            DateTime.UtcNow);
    }

    [Fact]
    public async Task Handle_WithAdminUsers_CreatesNotificationForEachAdmin()
    {
        // Arrange
        var admin1 = Guid.NewGuid();
        var admin2 = Guid.NewGuid();
        await SeedAdminUsers(admin1, admin2);

        var evt = CreateEvent(affectedStrategies: new[] { "BALANCED", "FAST" });

        // Act
        await _handler.Handle(evt, CancellationToken.None);

        // Assert
        _capturedNotifications.Should().HaveCount(2);

        var notification = _capturedNotifications.First(n => n.UserId == admin1);
        notification.Type.Should().Be(NotificationType.AdminModelDeprecated);
        notification.Severity.Should().Be(NotificationSeverity.Warning);
        notification.Title.Should().Contain("meta-llama/llama-3.3-70b-instruct:free");
        notification.Message.Should().Contain("BALANCED");
        notification.Message.Should().Contain("FAST");
        notification.Message.Should().Contain("qwen/qwen-2.5-72b:free");
        notification.Link.Should().Be("/admin/agents/usage");
    }

    [Fact]
    public async Task Handle_WithAdminUsers_IncludesCorrectMetadata()
    {
        // Arrange
        var admin1 = Guid.NewGuid();
        await SeedAdminUsers(admin1);

        var evt = CreateEvent(
            affectedStrategies: new[] { "PRECISE" },
            suggestedReplacement: "anthropic/claude-sonnet-4");

        // Act
        await _handler.Handle(evt, CancellationToken.None);

        // Assert
        _capturedNotifications.Should().ContainSingle();
        var notification = _capturedNotifications[0];
        notification.Metadata.Should().NotBeNullOrEmpty();

        var metadata = JsonSerializer.Deserialize<JsonElement>(notification.Metadata!);
        metadata.GetProperty("modelId").GetString().Should().Be("meta-llama/llama-3.3-70b-instruct:free");
        metadata.GetProperty("provider").GetString().Should().Be("openrouter");
        metadata.GetProperty("suggestedReplacement").GetString().Should().Be("anthropic/claude-sonnet-4");
        metadata.GetProperty("affectedStrategies").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithNoSuggestedReplacement_IncludesNoneAvailableInMessage()
    {
        // Arrange
        var admin1 = Guid.NewGuid();
        await SeedAdminUsers(admin1);

        var evt = CreateEvent(suggestedReplacement: null);

        // Act
        await _handler.Handle(evt, CancellationToken.None);

        // Assert
        _capturedNotifications.Should().ContainSingle();
        _capturedNotifications[0].Message.Should().Contain("none available");
    }

    [Fact]
    public async Task Handle_WithNoAdminUsers_DoesNotCreateNotifications()
    {
        // Arrange — only regular user, no admins
        _dbContext.Set<UserEntity>().Add(new UserEntity
        {
            Id = Guid.NewGuid(),
            Role = "user",
            Email = "user@test.com",
            PasswordHash = "hash",
            CreatedAt = DateTime.UtcNow,
        });
        await _dbContext.SaveChangesAsync();

        var evt = CreateEvent();

        // Act
        await _handler.Handle(evt, CancellationToken.None);

        // Assert
        _notificationRepoMock.Verify(
            r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_DoesNotNotifyRegularUsers()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var regularUserId = Guid.NewGuid();
        await SeedAdminUsers(adminId);

        var evt = CreateEvent();

        // Act
        await _handler.Handle(evt, CancellationToken.None);

        // Assert — only admin gets notification, not regular user
        _capturedNotifications.Should().ContainSingle();
        _capturedNotifications[0].UserId.Should().Be(adminId);
    }
}
