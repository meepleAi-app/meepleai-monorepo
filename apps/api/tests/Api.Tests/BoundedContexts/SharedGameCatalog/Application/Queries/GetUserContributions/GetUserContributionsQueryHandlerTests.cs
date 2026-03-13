using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetUserContributions;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Interfaces;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Queries.GetUserContributions;

/// <summary>
/// Integration tests for GetUserContributionsQueryHandler.
/// Issue #2726: Application - Query per Dashboard Utente
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class GetUserContributionsQueryHandlerTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private MeepleAiDbContext _dbContext = null!;
    private GetUserContributionsQueryHandler _handler = null!;
    private static readonly Guid TestUserId = Guid.NewGuid();
    private static readonly Guid OtherUserId = Guid.NewGuid();

    public GetUserContributionsQueryHandlerTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"contributions_test_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(connectionString, o => o.UseVector()) // Issue #3547
            .Options;

        var mediatorMock = new Mock<IMediator>();
        var eventCollectorMock = new Mock<IDomainEventCollector>();
        eventCollectorMock.Setup(x => x.GetAndClearEvents())
            .Returns(new List<IDomainEvent>().AsReadOnly());

        _dbContext = new MeepleAiDbContext(options, mediatorMock.Object, eventCollectorMock.Object);
        await _dbContext.Database.MigrateAsync();

        await SeedTestDataAsync();

        var loggerMock = new Mock<ILogger<GetUserContributionsQueryHandler>>();
        _handler = new GetUserContributionsQueryHandler(_dbContext, loggerMock.Object);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    private async Task SeedTestDataAsync()
    {
        // Seed test users
        var users = new[]
        {
            new UserEntity { Id = TestUserId, Email = "testuser@meepleai.dev", Role = "user", CreatedAt = DateTime.UtcNow },
            new UserEntity { Id = OtherUserId, Email = "other@meepleai.dev", Role = "user", CreatedAt = DateTime.UtcNow }
        };
        _dbContext.Set<UserEntity>().AddRange(users);

        // Seed test games
        var games = new[]
        {
            CreateSharedGame("Catan", 13),
            CreateSharedGame("Ticket to Ride", 9209)
        };
        _dbContext.Set<SharedGameEntity>().AddRange(games);
        await _dbContext.SaveChangesAsync();

        // Seed contributors
        var contributor1 = new ContributorEntity
        {
            Id = Guid.NewGuid(),
            UserId = TestUserId,
            SharedGameId = games[0].Id,
            IsPrimaryContributor = true,
            CreatedAt = DateTime.UtcNow.AddDays(-30)
        };

        var contributor2 = new ContributorEntity
        {
            Id = Guid.NewGuid(),
            UserId = TestUserId,
            SharedGameId = games[1].Id,
            IsPrimaryContributor = false,
            CreatedAt = DateTime.UtcNow.AddDays(-15)
        };

        var otherContributor = new ContributorEntity
        {
            Id = Guid.NewGuid(),
            UserId = OtherUserId,
            SharedGameId = games[0].Id,
            IsPrimaryContributor = false,
            CreatedAt = DateTime.UtcNow.AddDays(-10)
        };

        _dbContext.Set<ContributorEntity>().AddRange(contributor1, contributor2, otherContributor);
        await _dbContext.SaveChangesAsync();

        // Seed contribution records
        var records = new[]
        {
            CreateContributionRecord(contributor1.Id, ContributionRecordType.InitialSubmission, 1, DateTime.UtcNow.AddDays(-30)),
            CreateContributionRecord(contributor1.Id, ContributionRecordType.DocumentAddition, 2, DateTime.UtcNow.AddDays(-20)),
            CreateContributionRecord(contributor2.Id, ContributionRecordType.DocumentAddition, 1, DateTime.UtcNow.AddDays(-15))
        };
        _dbContext.Set<ContributionRecordEntity>().AddRange(records);
        await _dbContext.SaveChangesAsync();
    }

    private static SharedGameEntity CreateSharedGame(string title, int bggId) => new()
    {
        Id = Guid.NewGuid(),
        BggId = bggId,
        Title = title,
        YearPublished = 2000,
        Description = $"Description for {title}",
        MinPlayers = 2,
        MaxPlayers = 4,
        PlayingTimeMinutes = 60,
        MinAge = 10,
        ImageUrl = $"https://example.com/{title}.jpg",
        ThumbnailUrl = $"https://example.com/{title}_thumb.jpg",
        Status = (int)GameStatus.Published,
        CreatedBy = TestUserId,
        CreatedAt = DateTime.UtcNow
    };

    private static ContributionRecordEntity CreateContributionRecord(
        Guid contributorId,
        ContributionRecordType type,
        int version,
        DateTime contributedAt) => new()
        {
            Id = Guid.NewGuid(),
            ContributorId = contributorId,
            Type = (int)type,
            Description = $"Contribution v{version}",
            Version = version,
            ContributedAt = contributedAt,
            IncludesGameData = type == ContributionRecordType.InitialSubmission,
            IncludesMetadata = true,
            DocumentIdsJson = type == ContributionRecordType.DocumentAddition
            ? JsonSerializer.Serialize(new List<Guid> { Guid.NewGuid(), Guid.NewGuid() })
            : null
        };

    [Fact]
    public async Task Handle_ShouldReturnOnlyUserContributions()
    {
        // Arrange
        var query = new GetUserContributionsQuery(TestUserId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Total.Should().Be(2);
        result.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_ShouldIncludeGameTitleAndThumbnail()
    {
        // Arrange
        var query = new GetUserContributionsQuery(TestUserId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Items.Should().AllSatisfy(c =>
        {
            c.GameTitle.Should().NotBeNullOrEmpty();
            c.GameTitle.Should().NotBe("Unknown Game");
        });
    }

    [Fact]
    public async Task Handle_ShouldIncludeContributionRecords()
    {
        // Arrange
        var query = new GetUserContributionsQuery(TestUserId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        var primaryContribution = result.Items.FirstOrDefault(c => c.IsPrimaryContributor);
        primaryContribution.Should().NotBeNull();
        primaryContribution!.Contributions.Should().HaveCount(2);
        primaryContribution.ContributionCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_ShouldCalculateFirstAndLastContributionDates()
    {
        // Arrange
        var query = new GetUserContributionsQuery(TestUserId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        var primaryContribution = result.Items.FirstOrDefault(c => c.IsPrimaryContributor);
        primaryContribution.Should().NotBeNull();
        primaryContribution!.FirstContributionAt.Should().BeBefore(primaryContribution.LastContributionAt);
    }

    [Fact]
    public async Task Handle_ShouldCountDocumentsInContributions()
    {
        // Arrange
        var query = new GetUserContributionsQuery(TestUserId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        var docAdditions = result.Items
            .SelectMany(c => c.Contributions)
            .Where(r => r.Type == ContributionRecordType.DocumentAddition);

        docAdditions.Should().AllSatisfy(r => r.DocumentCount.Should().BeGreaterThan(0));
    }

    [Fact]
    public async Task Handle_WithPagination_ShouldReturnCorrectPage()
    {
        // Arrange
        var query = new GetUserContributionsQuery(TestUserId, PageNumber: 1, PageSize: 1);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Total.Should().Be(2);
        result.Items.Should().HaveCount(1);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ForUserWithNoContributions_ShouldReturnEmptyResult()
    {
        // Arrange
        var newUserId = Guid.NewGuid();
        _dbContext.Set<UserEntity>().Add(new UserEntity
        {
            Id = newUserId,
            Email = "newuser@meepleai.dev",
            Role = "user",
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        var query = new GetUserContributionsQuery(newUserId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Total.Should().Be(0);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldIdentifyPrimaryContributor()
    {
        // Arrange
        var query = new GetUserContributionsQuery(TestUserId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Items.Should().ContainSingle(c => c.IsPrimaryContributor);
        result.Items.Should().ContainSingle(c => !c.IsPrimaryContributor);
    }
}
