using Api.BoundedContexts.SharedGameCatalog.Application;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class UpdateSharedGameCommandHandlerTests : IDisposable
{
    private readonly Mock<ISharedGameRepository> _repositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();

    /// <summary>
    /// Issue #3866: the unit of work is mocked, so nothing was ever written — the assertions below
    /// used to pass only because a tracking-by-default test context resolved the reload to the same
    /// in-memory instance the handler had mutated. They verified the mutation, not the persistence.
    /// Wiring the mock to the real SaveChangesAsync makes them verify what they claim to.
    /// </summary>
    private void MakeUnitOfWorkPersist() =>
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken ct) => _dbContext.SaveChangesAsync(ct));
    private readonly Mock<ILogger<UpdateSharedGameCommandHandler>> _loggerMock = new();
    private readonly MeepleAiDbContext _dbContext;

    public UpdateSharedGameCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"update-shared-game-{Guid.NewGuid()}")
            .Options;
        _dbContext = new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task Handle_WithCoreFieldsOnly_UpdatesGameSuccessfully()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var domainAggregate = BuildAggregate(gameId, userId);
        _repositoryMock.Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(domainAggregate);

        var command = new UpdateSharedGameCommand(
            GameId: gameId,
            Title: "Wingspan ITA",
            YearPublished: 2019,
            Description: "Updated description",
            MinPlayers: 1,
            MaxPlayers: 5,
            PlayingTimeMinutes: 70,
            MinAge: 10,
            ComplexityRating: 2.4m,
            AverageRating: 8.1m,
            ImageUrl: "https://cdn/img.webp",
            ThumbnailUrl: "https://cdn/thumb.webp",
            Rules: null,
            ModifiedBy: userId);

        var handler = new UpdateSharedGameCommandHandler(
            _repositoryMock.Object, _unitOfWorkMock.Object, _dbContext, _loggerMock.Object);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(MediatR.Unit.Value);
        _repositoryMock.Verify(r => r.Update(It.IsAny<SharedGame>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithCategoriesAndMechanics_ReplacesCollections()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var entity = SeedGameEntity(gameId, userId);
        entity.Categories.Add(new GameCategoryEntity
        {
            Id = Guid.NewGuid(),
            Name = "Old",
            Slug = "old",
            CreatedAt = DateTime.UtcNow
        });
        _dbContext.Set<SharedGameEntity>().Add(entity);
        await _dbContext.SaveChangesAsync();

        var domainAggregate = BuildAggregate(gameId, userId);
        _repositoryMock.Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(domainAggregate);

        var command = new UpdateSharedGameCommand(
            GameId: gameId,
            Title: entity.Title,
            YearPublished: entity.YearPublished,
            Description: entity.Description,
            MinPlayers: entity.MinPlayers,
            MaxPlayers: entity.MaxPlayers,
            PlayingTimeMinutes: entity.PlayingTimeMinutes,
            MinAge: entity.MinAge,
            ComplexityRating: entity.ComplexityRating,
            AverageRating: entity.AverageRating,
            ImageUrl: entity.ImageUrl,
            ThumbnailUrl: entity.ThumbnailUrl,
            Rules: null,
            ModifiedBy: userId,
            Categories: new List<string> { "Strategy", "Negotiation" },
            Mechanics: new List<string> { "Dice Rolling" });

        MakeUnitOfWorkPersist();

        var handler = new UpdateSharedGameCommandHandler(
            _repositoryMock.Object, _unitOfWorkMock.Object, _dbContext, _loggerMock.Object);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        var reloaded = await _dbContext.Set<SharedGameEntity>()
            .Include(g => g.Categories)
            .Include(g => g.Mechanics)
            .FirstAsync(g => g.Id == gameId);

        reloaded.Categories.Select(c => c.Name).Should().BeEquivalentTo(new[] { "Strategy", "Negotiation" });
        reloaded.Mechanics.Select(m => m.Name).Should().BeEquivalentTo(new[] { "Dice Rolling" });
    }

    [Fact]
    public async Task Handle_WithBggIdProvided_UpdatesBggIdScalar()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var entity = SeedGameEntity(gameId, userId);
        entity.BggId = null;
        _dbContext.Set<SharedGameEntity>().Add(entity);
        await _dbContext.SaveChangesAsync();

        var domainAggregate = BuildAggregate(gameId, userId);
        _repositoryMock.Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(domainAggregate);

        var command = new UpdateSharedGameCommand(
            GameId: gameId,
            Title: entity.Title,
            YearPublished: entity.YearPublished,
            Description: entity.Description,
            MinPlayers: entity.MinPlayers,
            MaxPlayers: entity.MaxPlayers,
            PlayingTimeMinutes: entity.PlayingTimeMinutes,
            MinAge: entity.MinAge,
            ComplexityRating: entity.ComplexityRating,
            AverageRating: entity.AverageRating,
            ImageUrl: entity.ImageUrl,
            ThumbnailUrl: entity.ThumbnailUrl,
            Rules: null,
            ModifiedBy: userId,
            BggId: 13);

        MakeUnitOfWorkPersist();

        var handler = new UpdateSharedGameCommandHandler(
            _repositoryMock.Object, _unitOfWorkMock.Object, _dbContext, _loggerMock.Object);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        var reloaded = await _dbContext.Set<SharedGameEntity>().FirstAsync(g => g.Id == gameId);
        reloaded.BggId.Should().Be(13);
    }

    [Fact]
    public async Task Handle_WithNullCategories_PreservesExistingCategories()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var entity = SeedGameEntity(gameId, userId);
        entity.Categories.Add(new GameCategoryEntity { Id = Guid.NewGuid(), Name = "Strategy", Slug = "strategy", CreatedAt = DateTime.UtcNow });
        entity.Mechanics.Add(new GameMechanicEntity { Id = Guid.NewGuid(), Name = "Dice Rolling", Slug = "dice-rolling", CreatedAt = DateTime.UtcNow });
        _dbContext.Set<SharedGameEntity>().Add(entity);
        await _dbContext.SaveChangesAsync();

        var domainAggregate = BuildAggregate(gameId, userId);
        _repositoryMock.Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(domainAggregate);

        // Pass BggId only, leave taxonomy collections as null
        var command = new UpdateSharedGameCommand(
            GameId: gameId,
            Title: entity.Title,
            YearPublished: entity.YearPublished,
            Description: entity.Description,
            MinPlayers: entity.MinPlayers,
            MaxPlayers: entity.MaxPlayers,
            PlayingTimeMinutes: entity.PlayingTimeMinutes,
            MinAge: entity.MinAge,
            ComplexityRating: entity.ComplexityRating,
            AverageRating: entity.AverageRating,
            ImageUrl: entity.ImageUrl,
            ThumbnailUrl: entity.ThumbnailUrl,
            Rules: null,
            ModifiedBy: userId,
            BggId: 42);

        MakeUnitOfWorkPersist();

        var handler = new UpdateSharedGameCommandHandler(
            _repositoryMock.Object, _unitOfWorkMock.Object, _dbContext, _loggerMock.Object);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        var reloaded = await _dbContext.Set<SharedGameEntity>()
            .Include(g => g.Categories)
            .Include(g => g.Mechanics)
            .FirstAsync(g => g.Id == gameId);

        reloaded.Categories.Select(c => c.Name).Should().BeEquivalentTo(new[] { "Strategy" });
        reloaded.Mechanics.Select(m => m.Name).Should().BeEquivalentTo(new[] { "Dice Rolling" });
        reloaded.BggId.Should().Be(42);
    }

    /// <summary>
    /// Issue #4088: the admin edit drawer sends only the core fields, so these three arrive null.
    /// UpdateInfo is a total setter, so without the handler coalescing them against the stored
    /// aggregate every save wiped a rating that no job recomputes.
    /// </summary>
    [Fact]
    public async Task Handle_WithNullRatingsAndRules_PreservesStoredValues()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var storedRules = GameRules.Create("Place a bird, gain food.", "en");
        var domainAggregate = BuildAggregate(
            gameId, userId, complexityRating: 2.4m, averageRating: 8.1m, rules: storedRules);
        _repositoryMock.Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(domainAggregate);

        // Exactly the shape EditGameDrawer produces: core fields only.
        var command = new UpdateSharedGameCommand(
            GameId: gameId,
            Title: "Wingspan ITA",
            YearPublished: 2019,
            Description: "Bird-themed engine builder",
            MinPlayers: 1,
            MaxPlayers: 5,
            PlayingTimeMinutes: 60,
            MinAge: 10,
            ComplexityRating: null,
            AverageRating: null,
            ImageUrl: "https://cdn/old.webp",
            ThumbnailUrl: "https://cdn/old-thumb.webp",
            Rules: null,
            ModifiedBy: userId);

        var handler = new UpdateSharedGameCommandHandler(
            _repositoryMock.Object, _unitOfWorkMock.Object, _dbContext, _loggerMock.Object);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert — the edited field landed, the omitted ones survived.
        domainAggregate.Title.Should().Be("Wingspan ITA");
        domainAggregate.ComplexityRating.Should().Be(2.4m);
        domainAggregate.AverageRating.Should().Be(8.1m);
        domainAggregate.Rules.Should().NotBeNull();
        domainAggregate.Rules!.Content.Should().Be("Place a bird, gain food.");
    }

    /// <summary>
    /// Issue #4088: preserving on null must not make the fields read-only — a caller that does
    /// send values still overwrites.
    /// </summary>
    [Fact]
    public async Task Handle_WithNonNullRatingsAndRules_OverwritesStoredValues()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var domainAggregate = BuildAggregate(
            gameId,
            userId,
            complexityRating: 2.4m,
            averageRating: 8.1m,
            rules: GameRules.Create("Old rules.", "en"));
        _repositoryMock.Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(domainAggregate);

        var command = new UpdateSharedGameCommand(
            GameId: gameId,
            Title: "Wingspan",
            YearPublished: 2019,
            Description: "Bird-themed engine builder",
            MinPlayers: 1,
            MaxPlayers: 5,
            PlayingTimeMinutes: 60,
            MinAge: 10,
            ComplexityRating: 3.5m,
            AverageRating: 9.2m,
            ImageUrl: "https://cdn/old.webp",
            ThumbnailUrl: "https://cdn/old-thumb.webp",
            Rules: new GameRulesDto("New rules.", "it"),
            ModifiedBy: userId);

        var handler = new UpdateSharedGameCommandHandler(
            _repositoryMock.Object, _unitOfWorkMock.Object, _dbContext, _loggerMock.Object);

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        domainAggregate.ComplexityRating.Should().Be(3.5m);
        domainAggregate.AverageRating.Should().Be(9.2m);
        domainAggregate.Rules!.Content.Should().Be("New rules.");
        domainAggregate.Rules!.Language.Should().Be("it");
    }

    private static SharedGameEntity SeedGameEntity(Guid gameId, Guid createdBy)
    {
        return new SharedGameEntity
        {
            Id = gameId,
            Title = "Wingspan",
            Description = "Bird-themed engine builder",
            YearPublished = 2019,
            MinPlayers = 1,
            MaxPlayers = 5,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            ImageUrl = "https://cdn/old.webp",
            ThumbnailUrl = "https://cdn/old-thumb.webp",
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            CreatedBy = createdBy
        };
    }

    private static SharedGame BuildAggregate(
        Guid gameId,
        Guid createdBy,
        decimal? complexityRating = null,
        decimal? averageRating = null,
        GameRules? rules = null)
    {
        // Use the public Create factory; Id is generated internally.
        // We only need a valid domain object for the repository mock to return.
        return SharedGame.Create(
            title: "Wingspan",
            yearPublished: 2019,
            description: "Bird-themed engine builder",
            minPlayers: 1,
            maxPlayers: 5,
            playingTimeMinutes: 60,
            minAge: 10,
            complexityRating: complexityRating,
            averageRating: averageRating,
            imageUrl: "https://cdn/old.webp",
            thumbnailUrl: "https://cdn/old-thumb.webp",
            rules: rules,
            createdBy: createdBy,
            bggId: null);
    }
}
