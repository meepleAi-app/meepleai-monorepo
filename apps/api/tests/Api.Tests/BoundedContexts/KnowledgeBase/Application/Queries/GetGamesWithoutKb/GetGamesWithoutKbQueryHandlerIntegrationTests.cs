using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetGamesWithoutKb;
using Api.Infrastructure;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Queries.GetGamesWithoutKb;

/// <summary>
/// Integration tests for <see cref="GetGamesWithoutKbQueryHandler"/>.
/// Validates the admin RAG onboarding flow: returns paginated SharedGames where
/// <c>HasKnowledgeBase = false</c>, counts PDFs and surfaces failed-state flag.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class GetGamesWithoutKbQueryHandlerIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private GetGamesWithoutKbQueryHandler? _handler;
    private ServiceProvider? _serviceProvider;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public GetGamesWithoutKbQueryHandlerIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_games_without_kb_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(connectionString);
        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await _dbContext.Database.MigrateAsync(TestCancellationToken);
                break;
            }
            catch (NpgsqlException) when (attempt < 2)
            {
                await Task.Delay(TestConstants.Timing.RetryDelay, TestCancellationToken);
            }
        }

        var logger = _serviceProvider.GetRequiredService<ILogger<GetGamesWithoutKbQueryHandler>>();
        _handler = new GetGamesWithoutKbQueryHandler(_dbContext, logger);
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext is not null)
        {
            await _dbContext.DisposeAsync();
        }

        if (_serviceProvider is not null)
        {
            await _serviceProvider.DisposeAsync();
        }

        if (!string.IsNullOrEmpty(_databaseName))
        {
            try { await _fixture.DropIsolatedDatabaseAsync(_databaseName); }
            catch { /* best-effort cleanup */ }
        }
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_ReturnsOnlyGamesWithoutKb()
    {
        // Arrange: 2 games without KB, 1 with KB
        _dbContext!.SharedGames.AddRange(
            CreateGame("Wingspan", hasKb: true),
            CreateGame("Catan", hasKb: false),
            CreateGame("Pandemic", hasKb: false)
        );
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _handler!.Handle(new GetGamesWithoutKbQuery(), TestCancellationToken);

        // Assert
        result.Items.Should().HaveCount(2);
        result.Items.Should().NotContain(i => i.Title == "Wingspan");
        result.Items.Select(i => i.Title).Should().BeEquivalentTo(new[] { "Catan", "Pandemic" });
        result.Total.Should().Be(2);
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_FiltersDeletedGames()
    {
        // Arrange: soft-deleted game should never surface (global query filter)
        _dbContext!.SharedGames.AddRange(
            CreateGame("Visible", hasKb: false),
            CreateGame("DeletedGame", hasKb: false, isDeleted: true)
        );
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _handler!.Handle(new GetGamesWithoutKbQuery(), TestCancellationToken);

        // Assert
        result.Items.Should().ContainSingle(i => i.Title == "Visible");
        result.Items.Should().NotContain(i => i.Title == "DeletedGame");
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_FiltersUnpublishedGames()
    {
        // Arrange: draft game should not appear (Status != 1)
        var draft = CreateGame("DraftGame", hasKb: false);
        draft.Status = 0; // Draft
        _dbContext!.SharedGames.AddRange(
            CreateGame("PublishedGame", hasKb: false),
            draft
        );
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _handler!.Handle(new GetGamesWithoutKbQuery(), TestCancellationToken);

        // Assert
        result.Items.Should().ContainSingle(i => i.Title == "PublishedGame");
        result.Items.Should().NotContain(i => i.Title == "DraftGame");
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_SearchFilters_ByTitle_CaseInsensitive()
    {
        // Arrange
        _dbContext!.SharedGames.AddRange(
            CreateGame("Agricola", hasKb: false),
            CreateGame("Terraforming Mars", hasKb: false)
        );
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _handler!.Handle(
            new GetGamesWithoutKbQuery(Search: "agricola"),
            TestCancellationToken);

        // Assert
        result.Items.Should().ContainSingle(i => i.Title == "Agricola");
        result.Items.Should().NotContain(i => i.Title == "Terraforming Mars");
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_PdfCountAndFailedCount_AreAccuratelySeparate()
    {
        // Arrange: a game with 3 PDFs, only 1 failed — verifies that the failed
        // count is distinct from the total PdfCount (regression for H3).
        var adminUserId = Guid.NewGuid();
        _dbContext!.Users.Add(CreateTestUser(adminUserId));

        var game = CreateGame("Root", hasKb: false);
        _dbContext.SharedGames.Add(game);

        _dbContext.PdfDocuments.AddRange(
            new PdfDocumentEntity
            {
                Id = Guid.NewGuid(),
                SharedGameId = game.Id,
                FileName = "root.pdf",
                FilePath = "/root.pdf",
                FileSizeBytes = 1000,
                ProcessingState = "Ready",
                UploadedByUserId = adminUserId,
                UploadedAt = DateTime.UtcNow
            },
            new PdfDocumentEntity
            {
                Id = Guid.NewGuid(),
                SharedGameId = game.Id,
                FileName = "root-supplement.pdf",
                FilePath = "/root-supplement.pdf",
                FileSizeBytes = 750,
                ProcessingState = "Ready",
                UploadedByUserId = adminUserId,
                UploadedAt = DateTime.UtcNow
            },
            new PdfDocumentEntity
            {
                Id = Guid.NewGuid(),
                SharedGameId = game.Id,
                FileName = "root-ext.pdf",
                FilePath = "/root-ext.pdf",
                FileSizeBytes = 500,
                ProcessingState = "Failed",
                UploadedByUserId = adminUserId,
                UploadedAt = DateTime.UtcNow
            }
        );
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _handler!.Handle(
            new GetGamesWithoutKbQuery(Search: "Root"),
            TestCancellationToken);

        // Assert — total PdfCount (3) vs failed-only count (1) are distinct values
        var item = result.Items.Should().ContainSingle().Subject;
        item.PdfCount.Should().Be(3);
        item.FailedPdfCount.Should().Be(1);
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_ReturnsZeroStats_WhenGameHasNoPdfs()
    {
        // Arrange
        _dbContext!.SharedGames.Add(CreateGame("BareGame", hasKb: false));
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _handler!.Handle(
            new GetGamesWithoutKbQuery(Search: "BareGame"),
            TestCancellationToken);

        // Assert
        var item = result.Items.Should().ContainSingle().Subject;
        item.PdfCount.Should().Be(0);
        item.FailedPdfCount.Should().Be(0);
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_Pagination_WorksCorrectly()
    {
        // Arrange: 5 games without KB
        for (var i = 0; i < 5; i++)
        {
            _dbContext!.SharedGames.Add(CreateGame($"Game_{i:00}", hasKb: false));
        }
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        // Act
        var page1 = await _handler!.Handle(
            new GetGamesWithoutKbQuery(Page: 1, PageSize: 3),
            TestCancellationToken);
        var page2 = await _handler!.Handle(
            new GetGamesWithoutKbQuery(Page: 2, PageSize: 3),
            TestCancellationToken);

        // Assert
        page1.Items.Should().HaveCount(3);
        page2.Items.Should().HaveCount(2);
        page1.Total.Should().Be(5);
        page1.TotalPages.Should().Be(2);
        page1.Page.Should().Be(1);
        page2.Page.Should().Be(2);
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_EmptyDatabase_ReturnsZeroResults()
    {
        // Act
        var result = await _handler!.Handle(new GetGamesWithoutKbQuery(), TestCancellationToken);

        // Assert
        result.Items.Should().BeEmpty();
        result.Total.Should().Be(0);
        result.TotalPages.Should().Be(0);
    }

    private static UserEntity CreateTestUser(Guid userId)
        => new()
        {
            Id = userId,
            Email = $"test-{userId}@example.com",
            DisplayName = "Test Admin",
            PasswordHash = "hashed_password",
            Role = "admin",
            Tier = "free",
            CreatedAt = DateTime.UtcNow,
            IsTwoFactorEnabled = false
        };

    private static SharedGameEntity CreateGame(string title, bool hasKb, bool isDeleted = false)
        => new()
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = string.Empty,
            ImageUrl = string.Empty,
            ThumbnailUrl = string.Empty,
            HasKnowledgeBase = hasKb,
            IsDeleted = isDeleted,
            Status = 1, // Published
            YearPublished = 2024,
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        };
}
