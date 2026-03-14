using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application;

/// <summary>
/// Unit tests for ImportGamesFromExcelCommandHandler.
/// Validates Excel parsing, row-level validation, duplicate detection (DB + intra-file),
/// and partial-success semantics.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class ImportGamesFromExcelCommandTests
{
    private readonly Mock<ISharedGameRepository> _mockRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ILogger<ImportGamesFromExcelCommandHandler>> _mockLogger;
    private readonly Mock<TimeProvider> _mockTimeProvider;
    private readonly ImportGamesFromExcelCommandHandler _handler;

    public ImportGamesFromExcelCommandTests()
    {
        _mockRepository = new Mock<ISharedGameRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockLogger = new Mock<ILogger<ImportGamesFromExcelCommandHandler>>();
        _mockTimeProvider = new Mock<TimeProvider>();
        _mockTimeProvider.Setup(tp => tp.GetUtcNow()).Returns(DateTimeOffset.UtcNow);

        _handler = new ImportGamesFromExcelCommandHandler(
            _mockRepository.Object,
            _mockUnitOfWork.Object,
            _mockTimeProvider.Object,
            _mockLogger.Object);
    }

    #region Test 1: Valid 2-row Excel creates 2 games

    [Fact]
    public async Task Handle_ValidTwoRowExcel_ShouldCreateTwoGames()
    {
        // Arrange
        var file = CreateExcelFile(ws =>
        {
            ws.Cell(1, 1).Value = "Name";
            ws.Cell(1, 2).Value = "BggId";
            ws.Cell(2, 1).Value = "Catan";
            ws.Cell(2, 2).Value = 13;
            ws.Cell(3, 1).Value = "Wingspan";
            ws.Cell(3, 2).Value = 266192;
        });

        SetupNoDuplicates();

        var command = new ImportGamesFromExcelCommand(file, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Total.Should().Be(2);
        result.Created.Should().Be(2);
        result.Duplicates.Should().Be(0);
        result.Errors.Should().Be(0);
        result.RowErrors.Should().BeEmpty();

        _mockRepository.Verify(
            r => r.AddAsync(It.IsAny<Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates.SharedGame>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        _mockUnitOfWork.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    #endregion

    #region Test 2: Row with existing BggId in DB is skipped as duplicate

    [Fact]
    public async Task Handle_RowWithExistingBggIdInDb_ShouldSkipAsDuplicate()
    {
        // Arrange
        var file = CreateExcelFile(ws =>
        {
            ws.Cell(1, 1).Value = "Name";
            ws.Cell(1, 2).Value = "BggId";
            ws.Cell(2, 1).Value = "Catan";
            ws.Cell(2, 2).Value = 13;
        });

        _mockRepository
            .Setup(r => r.ExistsByBggIdAsync(13, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockRepository
            .Setup(r => r.ExistsByTitleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var command = new ImportGamesFromExcelCommand(file, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Total.Should().Be(1);
        result.Created.Should().Be(0);
        result.Duplicates.Should().Be(1);
        result.Errors.Should().Be(0);

        _mockRepository.Verify(
            r => r.AddAsync(It.IsAny<Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates.SharedGame>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region Test 3: Row with existing title (case-insensitive) in DB is skipped

    [Fact]
    public async Task Handle_RowWithExistingTitleInDb_ShouldSkipAsDuplicate()
    {
        // Arrange
        var file = CreateExcelFile(ws =>
        {
            ws.Cell(1, 1).Value = "Name";
            ws.Cell(1, 2).Value = "BggId";
            ws.Cell(2, 1).Value = "Catan";
        });

        _mockRepository
            .Setup(r => r.ExistsByTitleAsync("Catan", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockRepository
            .Setup(r => r.ExistsByBggIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var command = new ImportGamesFromExcelCommand(file, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Total.Should().Be(1);
        result.Created.Should().Be(0);
        result.Duplicates.Should().Be(1);
        result.Errors.Should().Be(0);
    }

    #endregion

    #region Test 4: Row with empty name produces error

    [Fact]
    public async Task Handle_RowWithEmptyName_ShouldReportError()
    {
        // Arrange
        var file = CreateExcelFile(ws =>
        {
            ws.Cell(1, 1).Value = "Name";
            ws.Cell(1, 2).Value = "BggId";
            ws.Cell(2, 1).Value = "";
            ws.Cell(2, 2).Value = 13;
        });

        var command = new ImportGamesFromExcelCommand(file, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Total.Should().Be(1);
        result.Created.Should().Be(0);
        result.Errors.Should().Be(1);
        result.RowErrors.Should().ContainSingle();
        result.RowErrors[0].RowNumber.Should().Be(2);
        result.RowErrors[0].ColumnName.Should().Be("Name");
    }

    #endregion

    #region Test 5: Row with whitespace-only name produces error

    [Fact]
    public async Task Handle_RowWithWhitespaceOnlyName_ShouldReportError()
    {
        // Arrange
        var file = CreateExcelFile(ws =>
        {
            ws.Cell(1, 1).Value = "Name";
            ws.Cell(1, 2).Value = "BggId";
            ws.Cell(2, 1).Value = "   ";
            ws.Cell(2, 2).Value = 42;
        });

        var command = new ImportGamesFromExcelCommand(file, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Total.Should().Be(1);
        result.Created.Should().Be(0);
        result.Errors.Should().Be(1);
        result.RowErrors.Should().ContainSingle();
        result.RowErrors[0].RowNumber.Should().Be(2);
        result.RowErrors[0].ColumnName.Should().Be("Name");
    }

    #endregion

    #region Test 6: Row with BggId <= 0 produces error

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-999)]
    public async Task Handle_RowWithBggIdLessThanOrEqualToZero_ShouldReportError(int invalidBggId)
    {
        // Arrange
        var file = CreateExcelFile(ws =>
        {
            ws.Cell(1, 1).Value = "Name";
            ws.Cell(1, 2).Value = "BggId";
            ws.Cell(2, 1).Value = "Catan";
            ws.Cell(2, 2).Value = invalidBggId;
        });

        var command = new ImportGamesFromExcelCommand(file, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Total.Should().Be(1);
        result.Created.Should().Be(0);
        result.Errors.Should().Be(1);
        result.RowErrors.Should().ContainSingle();
        result.RowErrors[0].RowNumber.Should().Be(2);
        result.RowErrors[0].ColumnName.Should().Be("BggId");
    }

    #endregion

    #region Test 7: Intra-file duplicate (same BggId) - second row skipped

    [Fact]
    public async Task Handle_IntraFileDuplicateBggId_ShouldSkipSecondRow()
    {
        // Arrange
        var file = CreateExcelFile(ws =>
        {
            ws.Cell(1, 1).Value = "Name";
            ws.Cell(1, 2).Value = "BggId";
            ws.Cell(2, 1).Value = "Catan";
            ws.Cell(2, 2).Value = 13;
            ws.Cell(3, 1).Value = "Catan Duplicate";
            ws.Cell(3, 2).Value = 13; // same BggId as row 2
        });

        SetupNoDuplicates();

        var command = new ImportGamesFromExcelCommand(file, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Total.Should().Be(2);
        result.Created.Should().Be(1);
        result.Duplicates.Should().Be(1);
        result.Errors.Should().Be(0);

        _mockRepository.Verify(
            r => r.AddAsync(It.IsAny<Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates.SharedGame>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Test 8: Row with name > 500 chars produces error

    [Fact]
    public async Task Handle_RowWithNameExceeding500Chars_ShouldReportError()
    {
        // Arrange
        var longName = new string('A', 501);
        var file = CreateExcelFile(ws =>
        {
            ws.Cell(1, 1).Value = "Name";
            ws.Cell(1, 2).Value = "BggId";
            ws.Cell(2, 1).Value = longName;
            ws.Cell(2, 2).Value = 42;
        });

        var command = new ImportGamesFromExcelCommand(file, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Total.Should().Be(1);
        result.Created.Should().Be(0);
        result.Errors.Should().Be(1);
        result.RowErrors.Should().ContainSingle();
        result.RowErrors[0].RowNumber.Should().Be(2);
        result.RowErrors[0].ColumnName.Should().Be("Name");
    }

    #endregion

    #region Test 9: Missing "Name" column produces error

    [Fact]
    public async Task Handle_MissingNameColumn_ShouldReturnErrorResult()
    {
        // Arrange
        var file = CreateExcelFile(ws =>
        {
            ws.Cell(1, 1).Value = "Title"; // wrong column name
            ws.Cell(1, 2).Value = "BggId";
            ws.Cell(2, 1).Value = "Catan";
            ws.Cell(2, 2).Value = 13;
        });

        var command = new ImportGamesFromExcelCommand(file, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert - handler returns error when required "Name" column is missing
        result.Errors.Should().Be(1);
        result.Created.Should().Be(0);
        result.RowErrors.Should().ContainSingle(e => e.ColumnName == "Name");
    }

    #endregion

    #region Test 10: Empty file (no data rows) returns Total=0

    [Fact]
    public async Task Handle_EmptyFileNoDataRows_ShouldReturnZeroTotalAndNoErrors()
    {
        // Arrange
        var file = CreateExcelFile(ws =>
        {
            ws.Cell(1, 1).Value = "Name";
            ws.Cell(1, 2).Value = "BggId";
            // No data rows
        });

        var command = new ImportGamesFromExcelCommand(file, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Total.Should().Be(0);
        result.Created.Should().Be(0);
        result.Duplicates.Should().Be(0);
        result.Errors.Should().Be(0);
        result.RowErrors.Should().BeEmpty();

        _mockRepository.Verify(
            r => r.AddAsync(It.IsAny<Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates.SharedGame>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _mockUnitOfWork.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region Helpers

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

    private void SetupNoDuplicates()
    {
        _mockRepository
            .Setup(r => r.ExistsByBggIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockRepository
            .Setup(r => r.ExistsByTitleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    #endregion
}
