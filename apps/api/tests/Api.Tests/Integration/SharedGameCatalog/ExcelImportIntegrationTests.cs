using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using ClosedXML.Excel;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Api.Tests.Integration.SharedGameCatalog;

/// <summary>
/// Integration tests for the admin bulk Excel import feature.
/// Tests the full CQRS pipeline: command -> handler -> repository -> database.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class ExcelImportIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;

    public ExcelImportIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    private IServiceProvider ServiceProvider => _serviceProvider ?? throw new InvalidOperationException("Service provider not initialized.");
    private MeepleAiDbContext DbContext => _dbContext ?? throw new InvalidOperationException("DbContext not initialized.");
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_excelimp_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseNpgsql(_isolatedDbConnectionString, o => o.UseVector());
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        // Register required services
        services.AddScoped<ISharedGameRepository, SharedGameRepository>();
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();
        services.AddScoped<IDomainEventCollector, DomainEventCollector>();
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await DbContext.Database.MigrateAsync(TestCancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }

        if (_serviceProvider is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
    }

    #region Test Cases

    [Fact]
    public async Task ImportExcel_TwoValidRows_PersistsToDatabase()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var file = CreateExcelFile(ws =>
        {
            ws.Cell(1, 1).Value = "Name";
            ws.Cell(1, 2).Value = "BggId";
            ws.Cell(2, 1).Value = "Gloomhaven";
            ws.Cell(2, 2).Value = "174430";
            ws.Cell(3, 1).Value = "Pandemic";
            ws.Cell(3, 2).Value = "30549";
        });

        var command = new ImportGamesFromExcelCommand(file, userId);
        var mediator = ServiceProvider.GetRequiredService<IMediator>();

        // Act
        var result = await mediator.Send(command, TestCancellationToken);

        // Assert
        result.Total.Should().Be(2);
        result.Created.Should().Be(2);
        result.Duplicates.Should().Be(0);
        result.Errors.Should().Be(0);
        result.RowErrors.Should().BeEmpty();

        var games = await DbContext.SharedGames
            .Where(g => !g.IsDeleted)
            .OrderBy(g => g.Title)
            .ToListAsync(TestCancellationToken);

        games.Should().HaveCount(2);

        var gloomhaven = games.First(g => g.Title == "Gloomhaven");
        gloomhaven.BggId.Should().Be(174430);
        gloomhaven.GameDataStatus.Should().Be((int)GameDataStatus.Skeleton);

        var pandemic = games.First(g => g.Title == "Pandemic");
        pandemic.BggId.Should().Be(30549);
        pandemic.GameDataStatus.Should().Be((int)GameDataStatus.Skeleton);
    }

    [Fact]
    public async Task ImportExcel_DuplicateBggId_SkipsSecondRow()
    {
        // Arrange - pre-seed a game with BggId 174430
        var userId = Guid.NewGuid();
        var existingGame = Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates.SharedGame.CreateSkeleton(
            "Existing Gloomhaven", userId, TimeProvider.System, bggId: 174430);

        var repository = ServiceProvider.GetRequiredService<ISharedGameRepository>();
        var unitOfWork = ServiceProvider.GetRequiredService<IUnitOfWork>();
        await repository.AddAsync(existingGame, TestCancellationToken);
        await unitOfWork.SaveChangesAsync(TestCancellationToken);

        var file = CreateExcelFile(ws =>
        {
            ws.Cell(1, 1).Value = "Name";
            ws.Cell(1, 2).Value = "BggId";
            ws.Cell(2, 1).Value = "Gloomhaven Import";
            ws.Cell(2, 2).Value = "174430";
        });

        var command = new ImportGamesFromExcelCommand(file, userId);
        var mediator = ServiceProvider.GetRequiredService<IMediator>();

        // Act
        var result = await mediator.Send(command, TestCancellationToken);

        // Assert
        result.Total.Should().Be(1);
        result.Created.Should().Be(0);
        result.Duplicates.Should().Be(1);
        result.Errors.Should().Be(0);

        var gamesInDb = await DbContext.SharedGames
            .Where(g => !g.IsDeleted)
            .ToListAsync(TestCancellationToken);

        gamesInDb.Should().HaveCount(1);
        gamesInDb[0].Title.Should().Be("Existing Gloomhaven");
    }

    [Fact]
    public async Task ImportExcel_DuplicateTitle_SkipsExistingGame()
    {
        // Arrange - pre-seed a game with title "Catan"
        var userId = Guid.NewGuid();
        var existingGame = Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates.SharedGame.CreateSkeleton(
            "Catan", userId, TimeProvider.System);

        var repository = ServiceProvider.GetRequiredService<ISharedGameRepository>();
        var unitOfWork = ServiceProvider.GetRequiredService<IUnitOfWork>();
        await repository.AddAsync(existingGame, TestCancellationToken);
        await unitOfWork.SaveChangesAsync(TestCancellationToken);

        var file = CreateExcelFile(ws =>
        {
            ws.Cell(1, 1).Value = "Name";
            ws.Cell(2, 1).Value = "Catan";
        });

        var command = new ImportGamesFromExcelCommand(file, userId);
        var mediator = ServiceProvider.GetRequiredService<IMediator>();

        // Act
        var result = await mediator.Send(command, TestCancellationToken);

        // Assert
        result.Total.Should().Be(1);
        result.Created.Should().Be(0);
        result.Duplicates.Should().Be(1);
        result.Errors.Should().Be(0);

        var gamesInDb = await DbContext.SharedGames
            .Where(g => !g.IsDeleted)
            .ToListAsync(TestCancellationToken);

        gamesInDb.Should().HaveCount(1);
        gamesInDb[0].Title.Should().Be("Catan");
    }

    [Fact]
    public async Task ImportExcel_PartialSuccess_SomeSavedSomeFailed()
    {
        // Arrange - 3 rows: valid game, empty name, valid game
        var userId = Guid.NewGuid();
        var file = CreateExcelFile(ws =>
        {
            ws.Cell(1, 1).Value = "Name";
            ws.Cell(1, 2).Value = "BggId";
            ws.Cell(2, 1).Value = "Terraforming Mars";
            ws.Cell(2, 2).Value = "167791";
            ws.Cell(3, 1).Value = "";         // empty name - should error
            ws.Cell(3, 2).Value = "99999";
            ws.Cell(4, 1).Value = "Wingspan";
            ws.Cell(4, 2).Value = "266192";
        });

        var command = new ImportGamesFromExcelCommand(file, userId);
        var mediator = ServiceProvider.GetRequiredService<IMediator>();

        // Act
        var result = await mediator.Send(command, TestCancellationToken);

        // Assert
        result.Total.Should().Be(3);
        result.Created.Should().Be(2);
        result.Errors.Should().Be(1);
        result.Duplicates.Should().Be(0);
        result.RowErrors.Should().HaveCount(1);
        result.RowErrors[0].RowNumber.Should().Be(3);
        result.RowErrors[0].ColumnName.Should().Be("Name");

        var gamesInDb = await DbContext.SharedGames
            .Where(g => !g.IsDeleted)
            .OrderBy(g => g.Title)
            .ToListAsync(TestCancellationToken);

        gamesInDb.Should().HaveCount(2);
        gamesInDb.Select(g => g.Title).Should().BeEquivalentTo(["Terraforming Mars", "Wingspan"]);
    }

    #endregion

    #region Helper Methods

    private static IFormFile CreateExcelFile(Action<IXLWorksheet> configure)
    {
        var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Games");
        configure(ws);
        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return new FormFile(stream, 0, stream.Length, "file", "games.xlsx");
    }

    #endregion
}
