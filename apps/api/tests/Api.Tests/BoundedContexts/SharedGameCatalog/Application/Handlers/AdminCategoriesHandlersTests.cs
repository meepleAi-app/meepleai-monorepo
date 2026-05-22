using Api.BoundedContexts.SecurityAudit.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands.AdminCategories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

/// <summary>
/// Handler tests for the admin-categories CRUD commands (#1440).
/// Covers AC-1..AC-4, AC-6 (audit log invocation), and the conflict guard
/// in AC-4 (forbid delete when linked games > 0).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "1440")]
public sealed class AdminCategoriesHandlersTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<HybridCache> _cacheMock;
    private readonly Mock<IAuditLogger> _auditLoggerMock;
    private readonly TimeProvider _clock;

    public AdminCategoriesHandlersTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"AdminCategories_{Guid.NewGuid()}")
            .Options;
        _dbContext = new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);
        _cacheMock = new Mock<HybridCache>();
        _auditLoggerMock = new Mock<IAuditLogger>();
        _clock = TimeProvider.System;
    }

    public void Dispose() => _dbContext.Dispose();

    // ── Create ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_WithValidCommand_PersistsCategoryAndAuditsAndInvalidatesCache()
    {
        var handler = new CreateGameCategoryCommandHandler(_dbContext, _cacheMock.Object, _auditLoggerMock.Object, _clock);
        var actor = Guid.NewGuid();
        var cmd = new CreateGameCategoryCommand("Strategy", "strategy", "🎯", "#ef4444", actor);

        var result = await handler.Handle(cmd, TestContext.Current.CancellationToken);

        result.Name.Should().Be("Strategy");
        result.Slug.Should().Be("strategy");
        result.Emoji.Should().Be("🎯");
        result.Color.Should().Be("#ef4444");
        result.GameCount.Should().Be(0);

        (await _dbContext.GameCategories.CountAsync(TestContext.Current.CancellationToken)).Should().Be(1);

        _auditLoggerMock.Verify(a => a.LogAsync(
            AuditEventType.CategoryCreated,
            actor,
            It.IsAny<Guid?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _cacheMock.Verify(c => c.RemoveAsync("game-categories", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_WithDuplicateName_ThrowsConflictException()
    {
        _dbContext.GameCategories.Add(new GameCategoryEntity { Id = Guid.NewGuid(), Name = "Strategy", Slug = "old-slug", CreatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var handler = new CreateGameCategoryCommandHandler(_dbContext, _cacheMock.Object, _auditLoggerMock.Object, _clock);
        var cmd = new CreateGameCategoryCommand("Strategy", "new-slug", null, null, Guid.NewGuid());

        var act = () => handler.Handle(cmd, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*Strategy*already exists*");
    }

    [Fact]
    public async Task Create_WithDuplicateSlug_ThrowsConflictException()
    {
        _dbContext.GameCategories.Add(new GameCategoryEntity { Id = Guid.NewGuid(), Name = "Old", Slug = "strategy", CreatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var handler = new CreateGameCategoryCommandHandler(_dbContext, _cacheMock.Object, _auditLoggerMock.Object, _clock);
        var cmd = new CreateGameCategoryCommand("New Name", "strategy", null, null, Guid.NewGuid());

        var act = () => handler.Handle(cmd, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*strategy*already exists*");
    }

    // ── Update ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_WithValidCommand_PersistsChangesAndAudits()
    {
        var id = Guid.NewGuid();
        _dbContext.GameCategories.Add(new GameCategoryEntity { Id = id, Name = "Old", Slug = "old", CreatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var handler = new UpdateGameCategoryCommandHandler(_dbContext, _cacheMock.Object, _auditLoggerMock.Object, _clock);
        var actor = Guid.NewGuid();
        var cmd = new UpdateGameCategoryCommand(id, "Strategy", "strategy", "🎯", "#ef4444", actor);

        var result = await handler.Handle(cmd, TestContext.Current.CancellationToken);

        result.Name.Should().Be("Strategy");
        result.Slug.Should().Be("strategy");
        result.Emoji.Should().Be("🎯");

        var persisted = await _dbContext.GameCategories.FindAsync([id], TestContext.Current.CancellationToken);
        persisted!.Name.Should().Be("Strategy");
        persisted.UpdatedBy.Should().Be(actor);
        persisted.UpdatedAt.Should().NotBeNull();

        _auditLoggerMock.Verify(a => a.LogAsync(
            AuditEventType.CategoryUpdated,
            actor,
            It.IsAny<Guid?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_WithNonExistentId_ThrowsNotFoundException()
    {
        var handler = new UpdateGameCategoryCommandHandler(_dbContext, _cacheMock.Object, _auditLoggerMock.Object, _clock);
        var cmd = new UpdateGameCategoryCommand(Guid.NewGuid(), "Strategy", "strategy", null, null, Guid.NewGuid());

        var act = () => handler.Handle(cmd, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Update_WithDuplicateNameOnDifferentCategory_ThrowsConflictException()
    {
        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();
        _dbContext.GameCategories.AddRange(
            new GameCategoryEntity { Id = idA, Name = "Strategy", Slug = "strategy", CreatedAt = DateTime.UtcNow },
            new GameCategoryEntity { Id = idB, Name = "Party", Slug = "party", CreatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var handler = new UpdateGameCategoryCommandHandler(_dbContext, _cacheMock.Object, _auditLoggerMock.Object, _clock);
        // Try to rename Party → Strategy (collides with A).
        var cmd = new UpdateGameCategoryCommand(idB, "Strategy", "party", null, null, Guid.NewGuid());

        var act = () => handler.Handle(cmd, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*Strategy*already exists*");
    }

    // ── Delete ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_WithNoLinkedGames_DeletesCategoryAndAudits()
    {
        var id = Guid.NewGuid();
        _dbContext.GameCategories.Add(new GameCategoryEntity { Id = id, Name = "Strategy", Slug = "strategy", CreatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var handler = new DeleteGameCategoryCommandHandler(_dbContext, _cacheMock.Object, _auditLoggerMock.Object);
        var actor = Guid.NewGuid();

        await handler.Handle(new DeleteGameCategoryCommand(id, actor), TestContext.Current.CancellationToken);

        (await _dbContext.GameCategories.FindAsync([id], TestContext.Current.CancellationToken)).Should().BeNull();

        _auditLoggerMock.Verify(a => a.LogAsync(
            AuditEventType.CategoryDeleted,
            actor,
            It.IsAny<Guid?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _cacheMock.Verify(c => c.RemoveAsync("game-categories", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_WithNonExistentId_ThrowsNotFoundException()
    {
        var handler = new DeleteGameCategoryCommandHandler(_dbContext, _cacheMock.Object, _auditLoggerMock.Object);

        var act = () => handler.Handle(new DeleteGameCategoryCommand(Guid.NewGuid(), Guid.NewGuid()), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Delete_WithLinkedGames_ThrowsConflictException()
    {
        // AC-4: category with gameCount > 0 must throw 409, never cascade.
        var categoryId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        var category = new GameCategoryEntity { Id = categoryId, Name = "Strategy", Slug = "strategy", CreatedAt = DateTime.UtcNow };
        var game = new SharedGameEntity
        {
            Id = gameId,
            Title = "Catan",
            Description = "Trading and building",
            YearPublished = 1995,
            MinPlayers = 3,
            MaxPlayers = 4,
            PlayingTimeMinutes = 90,
            MinAge = 10,
            ImageUrl = "https://example.com/catan.jpg",
            ThumbnailUrl = "https://example.com/catan-thumb.jpg",
            Status = (int)Api.BoundedContexts.SharedGameCatalog.Domain.Entities.GameStatus.Published,
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
        };
        category.SharedGames.Add(game);

        _dbContext.GameCategories.Add(category);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var handler = new DeleteGameCategoryCommandHandler(_dbContext, _cacheMock.Object, _auditLoggerMock.Object);

        var act = () => handler.Handle(new DeleteGameCategoryCommand(categoryId, Guid.NewGuid()), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*Strategy*1 linked games*");

        // Category MUST still exist after the rejection.
        (await _dbContext.GameCategories.FindAsync([categoryId], TestContext.Current.CancellationToken)).Should().NotBeNull();

        _auditLoggerMock.Verify(a => a.LogAsync(
            It.IsAny<string>(),
            It.IsAny<Guid?>(),
            It.IsAny<Guid?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
