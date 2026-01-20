using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain;
using Api.SharedKernel.Domain.Interfaces;
using Api.Tests.Infrastructure;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Integration tests for GetGameFaqsQueryHandler.
/// Issue #2681: Public FAQs endpoints
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class GetGameFaqsQueryHandlerTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private MeepleAiDbContext _dbContext = null!;
    private GetGameFaqsQueryHandler _handler = null!;
    private static readonly Guid TestUserId = Guid.NewGuid();
    private Guid _publishedGameId;
    private Guid _draftGameId;

    public GetGameFaqsQueryHandlerTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"getfaqs_handler_test_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        var mediatorMock = new Mock<IMediator>();
        var eventCollectorMock = new Mock<IDomainEventCollector>();
        eventCollectorMock.Setup(x => x.GetAndClearEvents())
            .Returns(new List<IDomainEvent>().AsReadOnly());

        _dbContext = new MeepleAiDbContext(options, mediatorMock.Object, eventCollectorMock.Object);
        await _dbContext.Database.MigrateAsync();

        await SeedTestDataAsync();

        var loggerMock = new Mock<ILogger<GetGameFaqsQueryHandler>>();
        _handler = new GetGameFaqsQueryHandler(_dbContext, loggerMock.Object);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    private async Task SeedTestDataAsync()
    {
        // Seed test user
        var user = new UserEntity
        {
            Id = TestUserId,
            Email = "testuser@meepleai.dev",
            Role = "user",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Set<UserEntity>().Add(user);

        // Seed published game with FAQs
        _publishedGameId = Guid.NewGuid();
        var publishedGame = new SharedGameEntity
        {
            Id = _publishedGameId,
            Title = "Published Game",
            YearPublished = 2020,
            Description = "A published game",
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            ImageUrl = "https://example.com/game.jpg",
            ThumbnailUrl = "https://example.com/thumb.jpg",
            Status = (int)GameStatus.Published,
            CreatedBy = TestUserId,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Set<SharedGameEntity>().Add(publishedGame);

        // Seed draft game (should not return FAQs for public endpoint)
        _draftGameId = Guid.NewGuid();
        var draftGame = new SharedGameEntity
        {
            Id = _draftGameId,
            Title = "Draft Game",
            YearPublished = 2024,
            Description = "A draft game",
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            ImageUrl = "https://example.com/draft.jpg",
            ThumbnailUrl = "https://example.com/draft-thumb.jpg",
            Status = (int)GameStatus.Draft,
            CreatedBy = TestUserId,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Set<SharedGameEntity>().Add(draftGame);

        await _dbContext.SaveChangesAsync();

        // Seed FAQs for published game
        var faqs = new[]
        {
            new GameFaqEntity
            {
                Id = Guid.NewGuid(),
                SharedGameId = _publishedGameId,
                Question = "First question?",
                Answer = "First answer",
                Order = 0,
                UpvoteCount = 5,
                CreatedAt = DateTime.UtcNow
            },
            new GameFaqEntity
            {
                Id = Guid.NewGuid(),
                SharedGameId = _publishedGameId,
                Question = "Second question?",
                Answer = "Second answer",
                Order = 1,
                UpvoteCount = 10,
                CreatedAt = DateTime.UtcNow
            },
            new GameFaqEntity
            {
                Id = Guid.NewGuid(),
                SharedGameId = _publishedGameId,
                Question = "Third question?",
                Answer = "Third answer",
                Order = 2,
                UpvoteCount = 3,
                CreatedAt = DateTime.UtcNow
            }
        };
        _dbContext.Set<GameFaqEntity>().AddRange(faqs);

        // Seed FAQ for draft game
        var draftFaq = new GameFaqEntity
        {
            Id = Guid.NewGuid(),
            SharedGameId = _draftGameId,
            Question = "Draft question?",
            Answer = "Draft answer",
            Order = 0,
            UpvoteCount = 0,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Set<GameFaqEntity>().Add(draftFaq);

        await _dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_WithPublishedGame_ReturnsFaqs()
    {
        // Arrange
        var query = new GetGameFaqsQuery(_publishedGameId, Limit: 10, Offset: 0);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Faqs.Should().HaveCount(3);
        result.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task Handle_WithDraftGame_ReturnsEmptyResult()
    {
        // Arrange
        var query = new GetGameFaqsQuery(_draftGameId, Limit: 10, Offset: 0);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Faqs.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithNonExistentGame_ReturnsEmptyResult()
    {
        // Arrange
        var query = new GetGameFaqsQuery(Guid.NewGuid(), Limit: 10, Offset: 0);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Faqs.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithPagination_ReturnsCorrectPage()
    {
        // Arrange
        var query = new GetGameFaqsQuery(_publishedGameId, Limit: 2, Offset: 0);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Faqs.Should().HaveCount(2);
        result.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task Handle_WithOffset_ReturnsRemainingFaqs()
    {
        // Arrange
        var query = new GetGameFaqsQuery(_publishedGameId, Limit: 2, Offset: 2);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Faqs.Should().HaveCount(1);
        result.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task Handle_OrdersByOrderThenByUpvoteCount()
    {
        // Arrange
        var query = new GetGameFaqsQuery(_publishedGameId, Limit: 10, Offset: 0);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Faqs.Should().HaveCount(3);
        var faqList = result.Faqs.ToList();
        // Should be ordered by Order (0, 1, 2)
        faqList[0].Order.Should().Be(0);
        faqList[1].Order.Should().Be(1);
        faqList[2].Order.Should().Be(2);
    }

    [Fact]
    public async Task Handle_ReturnsCorrectDtoFields()
    {
        // Arrange
        var query = new GetGameFaqsQuery(_publishedGameId, Limit: 10, Offset: 0);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        var faq = result.Faqs.First();
        faq.Id.Should().NotBe(Guid.Empty);
        faq.GameId.Should().Be(_publishedGameId);
        faq.Question.Should().NotBeNullOrWhiteSpace();
        faq.Answer.Should().NotBeNullOrWhiteSpace();
        faq.Order.Should().BeGreaterThanOrEqualTo(0);
        faq.Upvotes.Should().BeGreaterThanOrEqualTo(0);
        faq.CreatedAt.Should().BeAfter(DateTime.MinValue);
    }
}
