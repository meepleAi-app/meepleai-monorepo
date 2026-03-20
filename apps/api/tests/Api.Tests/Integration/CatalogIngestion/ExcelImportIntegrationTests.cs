using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Domain.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using ClosedXML.Excel;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.Integration.CatalogIngestion;

/// <summary>
/// Integration tests for Excel import with real PostgreSQL database.
/// Validates end-to-end: Excel parsing → skeleton creation → DB persistence → dedup detection.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class ExcelImportIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private MeepleAiDbContext _dbContext = null!;
    private ImportGamesFromExcelCommandHandler _handler = null!;

    private static readonly Guid AdminUserId = Guid.NewGuid();

    public ExcelImportIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"excelimp_test_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(connectionString, o => o.UseVector())
            .Options;

        var mockMediator = new Mock<IMediator>();
        var eventCollectorMock = new Mock<Api.SharedKernel.Application.Services.IDomainEventCollector>();
        eventCollectorMock.Setup(x => x.GetAndClearEvents())
            .Returns(new List<IDomainEvent>().AsReadOnly());

        _dbContext = new MeepleAiDbContext(options, mockMediator.Object, eventCollectorMock.Object);
        await _dbContext.Database.MigrateAsync();

        var repository = new Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories.SharedGameRepository(_dbContext, eventCollectorMock.Object);
        var unitOfWork = new Api.SharedKernel.Infrastructure.Persistence.UnitOfWork(_dbContext);

        _handler = new ImportGamesFromExcelCommandHandler(
            repository,
            unitOfWork,
            TimeProvider.System,
            Mock.Of<ILogger<ImportGamesFromExcelCommandHandler>>());
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task Import_ValidExcel_CreatesSkeletonGamesInDb()
    {
        // Arrange
        var file = CreateExcelFile(
            ("Catan", null),
            ("Wingspan", 266192),
            ("Gloomhaven", 174430));

        var command = new ImportGamesFromExcelCommand(file, AdminUserId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Total.Should().Be(3);
        result.Created.Should().Be(3);
        result.Duplicates.Should().Be(0);
        result.Errors.Should().Be(0);

        // Verify persisted in DB
        var games = await _dbContext.SharedGames
            .AsNoTracking()
            .Where(g => g.CreatedBy == AdminUserId)
            .ToListAsync();

        games.Should().HaveCount(3);
        games.Should().Contain(g => g.Title == "Catan");
        games.Should().Contain(g => g.Title == "Wingspan" && g.BggId == 266192);
        games.Should().Contain(g => g.Title == "Gloomhaven" && g.BggId == 174430);

        // All should be Skeleton status
        games.Should().OnlyContain(g => g.GameDataStatus == (int)GameDataStatus.Skeleton);
    }

    [Fact]
    public async Task Import_DuplicateTitle_SkipsSecondImport()
    {
        // Arrange: Import once
        var file1 = CreateExcelFile(("Catan", null));
        await _handler.Handle(new ImportGamesFromExcelCommand(file1, AdminUserId), CancellationToken.None);

        // Act: Import same title again
        var file2 = CreateExcelFile(("Catan", null));
        var result = await _handler.Handle(new ImportGamesFromExcelCommand(file2, AdminUserId), CancellationToken.None);

        // Assert
        result.Total.Should().Be(1);
        result.Created.Should().Be(0);
        result.Duplicates.Should().Be(1);
    }

    [Fact]
    public async Task Import_DuplicateBggId_SkipsSecondImport()
    {
        // Arrange: Import once with BggId
        var file1 = CreateExcelFile(("Catan", 13));
        await _handler.Handle(new ImportGamesFromExcelCommand(file1, AdminUserId), CancellationToken.None);

        // Act: Import different title but same BggId
        var file2 = CreateExcelFile(("Die Siedler von Catan", 13));
        var result = await _handler.Handle(new ImportGamesFromExcelCommand(file2, AdminUserId), CancellationToken.None);

        // Assert
        result.Total.Should().Be(1);
        result.Created.Should().Be(0);
        result.Duplicates.Should().Be(1);
    }

    [Fact]
    public async Task Import_IntraFileDuplicates_DetectsAndSkips()
    {
        // Arrange: Excel with duplicate rows
        var file = CreateExcelFile(
            ("Catan", null),
            ("Catan", null),  // duplicate name
            ("Wingspan", 100),
            ("Azul", 100));   // duplicate BggId

        // Act
        var result = await _handler.Handle(new ImportGamesFromExcelCommand(file, AdminUserId), CancellationToken.None);

        // Assert
        result.Total.Should().Be(4);
        result.Created.Should().Be(2);  // Catan + Wingspan
        result.Duplicates.Should().Be(2); // second Catan + Azul (same BggId)
    }

    [Fact]
    public async Task Import_MissingNameColumn_ReturnsError()
    {
        // Arrange: Excel with wrong header
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Games");
        worksheet.Cell(1, 1).Value = "Title"; // Not "Name"
        worksheet.Cell(2, 1).Value = "Catan";

        var file = WorkbookToFormFile(workbook);
        var command = new ImportGamesFromExcelCommand(file, AdminUserId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Total.Should().Be(0);
        result.Errors.Should().Be(1);
        result.RowErrors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Contain("Name");
    }

    [Fact]
    public async Task Import_EmptyName_ReportsRowError()
    {
        // Arrange
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Games");
        worksheet.Cell(1, 1).Value = "Name";
        worksheet.Cell(1, 2).Value = "BggId";
        worksheet.Cell(2, 1).Value = ""; // Empty name
        worksheet.Cell(2, 2).Value = "13";

        var file = WorkbookToFormFile(workbook);
        var command = new ImportGamesFromExcelCommand(file, AdminUserId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Errors.Should().Be(1);
        result.RowErrors.Should().ContainSingle()
            .Which.ColumnName.Should().Be("Name");
    }

    [Fact]
    public async Task Import_InvalidBggId_ReportsRowError()
    {
        // Arrange
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Games");
        worksheet.Cell(1, 1).Value = "Name";
        worksheet.Cell(1, 2).Value = "BggId";
        worksheet.Cell(2, 1).Value = "Test Game";
        worksheet.Cell(2, 2).Value = "not-a-number";

        var file = WorkbookToFormFile(workbook);
        var command = new ImportGamesFromExcelCommand(file, AdminUserId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Errors.Should().Be(1);
        result.RowErrors.Should().ContainSingle()
            .Which.ColumnName.Should().Be("BggId");
    }

    [Fact]
    public async Task Import_NoBggIdColumn_SucceedsWithNullBggId()
    {
        // Arrange: Excel with only Name column
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Games");
        worksheet.Cell(1, 1).Value = "Name";
        worksheet.Cell(2, 1).Value = "Catan";

        var file = WorkbookToFormFile(workbook);
        var command = new ImportGamesFromExcelCommand(file, AdminUserId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Created.Should().Be(1);

        var game = await _dbContext.SharedGames
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Title == "Catan");
        game.Should().NotBeNull();
        game!.BggId.Should().BeNull();
    }

    [Fact]
    public async Task Import_LargeFile_HandlesMultipleRows()
    {
        // Arrange: 50 games
        var games = Enumerable.Range(1, 50)
            .Select(i => ($"Game_{i}", (int?)i))
            .ToArray();

        var file = CreateExcelFile(games);
        var command = new ImportGamesFromExcelCommand(file, AdminUserId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Total.Should().Be(50);
        result.Created.Should().Be(50);
        result.Duplicates.Should().Be(0);
        result.Errors.Should().Be(0);

        var count = await _dbContext.SharedGames
            .AsNoTracking()
            .Where(g => g.CreatedBy == AdminUserId)
            .CountAsync();
        count.Should().Be(50);
    }

    #region Helpers

    private static IFormFile CreateExcelFile(params (string Name, int? BggId)[] games)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Games");

        worksheet.Cell(1, 1).Value = "Name";
        worksheet.Cell(1, 2).Value = "BggId";

        for (var i = 0; i < games.Length; i++)
        {
            worksheet.Cell(i + 2, 1).Value = games[i].Name;
            if (games[i].BggId.HasValue)
                worksheet.Cell(i + 2, 2).Value = games[i].BggId.Value;
        }

        return WorkbookToFormFile(workbook);
    }

    private static IFormFile WorkbookToFormFile(XLWorkbook workbook)
    {
        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        return new FormFile(stream, 0, stream.Length, "file", "games.xlsx")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        };
    }

    #endregion
}
