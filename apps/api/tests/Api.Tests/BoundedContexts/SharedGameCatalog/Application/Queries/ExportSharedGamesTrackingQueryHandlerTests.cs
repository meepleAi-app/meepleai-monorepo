using Api.BoundedContexts.SharedGameCatalog.Application.Queries.ExportSharedGamesTracking;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using ClosedXML.Excel;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Unit tests for ExportSharedGamesTrackingQueryHandler.
/// Verifies Excel output content, status formatting, and soft-delete exclusion.
/// Uses InMemoryDatabase for isolation — no external dependencies.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class ExportSharedGamesTrackingQueryHandlerTests
{
    // ------------------------------------------------------------------ helpers

    private static Api.Infrastructure.MeepleAiDbContext CreateContext() =>
        TestDbContextFactory.CreateInMemoryDbContext();

    private static ExportSharedGamesTrackingQueryHandler CreateHandler(
        Api.Infrastructure.MeepleAiDbContext ctx) =>
        new(ctx);

    private static SharedGameEntity BuildGame(
        string title = "Test Game",
        int gameDataStatus = 5,
        int status = 2,
        bool hasUploadedPdf = false,
        int yearPublished = 2020,
        int minPlayers = 2,
        int maxPlayers = 4,
        decimal? complexityRating = 2.5m,
        bool isDeleted = false)
    {
        return new SharedGameEntity
        {
            Id = Guid.NewGuid(),
            Title = title,
            GameDataStatus = gameDataStatus,
            Status = status,
            HasUploadedPdf = hasUploadedPdf,
            YearPublished = yearPublished,
            MinPlayers = minPlayers,
            MaxPlayers = maxPlayers,
            ComplexityRating = complexityRating,
            IsDeleted = isDeleted,
            CreatedAt = new DateTime(2025, 1, 15, 10, 0, 0, DateTimeKind.Utc),
            CreatedBy = Guid.NewGuid(),
        };
    }

    /// <summary>
    /// Loads the byte[] into an XLWorkbook and returns the "Tracking" worksheet.
    /// </summary>
    private static IXLWorksheet OpenTrackingSheet(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        var wb = new XLWorkbook(ms);
        return wb.Worksheet("Tracking");
    }

    // ------------------------------------------------------------------ tests

    [Fact]
    public async Task Handle_EmptyDatabase_ReturnsValidExcelWithHeaderOnly()
    {
        // Arrange
        using var ctx = CreateContext();
        var handler = CreateHandler(ctx);
        var query = new ExportSharedGamesTrackingQuery();

        // Act
        var bytes = await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert — non-empty byte array representing a valid xlsx
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0, "Expected non-empty xlsx bytes");

        var ws = OpenTrackingSheet(bytes);

        // Header row should exist
        Assert.Equal("Title", ws.Cell(1, 1).GetString());
        Assert.Equal("BGG ID", ws.Cell(1, 2).GetString());
        Assert.Equal("Data Status", ws.Cell(1, 3).GetString());
        Assert.Equal("Game Status", ws.Cell(1, 4).GetString());
        Assert.Equal("Has PDF", ws.Cell(1, 5).GetString());
        Assert.Equal("RAG Ready", ws.Cell(1, 6).GetString());
        Assert.Equal("Created At", ws.Cell(1, 7).GetString());
        Assert.Equal("Year", ws.Cell(1, 8).GetString());
        Assert.Equal("Players", ws.Cell(1, 9).GetString());
        Assert.Equal("Complexity", ws.Cell(1, 10).GetString());

        // No data rows
        Assert.True(ws.Cell(2, 1).IsEmpty(), "Expected no data in row 2 for empty DB");
    }

    [Fact]
    public async Task Handle_WithSkeletonGames_ReturnsCorrectStatusAndFormatting()
    {
        // Arrange
        using var ctx = CreateContext();

        var skeletonGame = BuildGame(
            title: "Skeleton Game",
            gameDataStatus: 0,   // Skeleton
            status: 0,           // Draft
            hasUploadedPdf: false,
            yearPublished: 0,    // Should render as "-"
            minPlayers: 0,       // Should render as "-"
            maxPlayers: 0,
            complexityRating: null); // Should render as "-"

        ctx.SharedGames.Add(skeletonGame);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        var handler = CreateHandler(ctx);
        var query = new ExportSharedGamesTrackingQuery();

        // Act
        var bytes = await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        var ws = OpenTrackingSheet(bytes);

        // Row 2 should contain the skeleton game
        Assert.Equal("Skeleton Game", ws.Cell(2, 1).GetString());
        Assert.Equal("Skeleton", ws.Cell(2, 3).GetString());   // GameDataStatus=0
        Assert.Equal("Draft", ws.Cell(2, 4).GetString());       // Status=0
        Assert.Equal("No", ws.Cell(2, 5).GetString());          // HasUploadedPdf=false
        Assert.Equal("No", ws.Cell(2, 6).GetString());          // Not RAG ready
        Assert.Equal("-", ws.Cell(2, 8).GetString());           // YearPublished=0
        Assert.Equal("-", ws.Cell(2, 9).GetString());           // MinPlayers=0
        Assert.Equal("-", ws.Cell(2, 10).GetString());          // ComplexityRating=null

        // CreatedAt formatted as "yyyy-MM-dd HH:mm"
        Assert.Equal("2025-01-15 10:00", ws.Cell(2, 7).GetString());

        // LightCoral background on Data Status cell for Skeleton (status=0)
        var statusCell = ws.Cell(2, 3);
        Assert.Equal(XLColor.LightCoral, statusCell.Style.Fill.BackgroundColor);
    }

    [Fact]
    public async Task Handle_ExcludesSoftDeletedGames()
    {
        // Arrange
        using var ctx = CreateContext();

        var activeGame = BuildGame(title: "Active Game", isDeleted: false);
        var deletedGame = BuildGame(title: "Deleted Game", isDeleted: true);

        ctx.SharedGames.AddRange(activeGame, deletedGame);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        var handler = CreateHandler(ctx);
        var query = new ExportSharedGamesTrackingQuery();

        // Act
        var bytes = await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        var ws = OpenTrackingSheet(bytes);

        // Only the active game should appear; row 2 = active game, row 3 = empty
        Assert.Equal("Active Game", ws.Cell(2, 1).GetString());
        Assert.True(ws.Cell(3, 1).IsEmpty(), "Soft-deleted game must not appear in output");
    }
}
