using Api.BoundedContexts.BusinessSimulations.Application.Queries;
using Api.BoundedContexts.BusinessSimulations.Domain.Entities;
using Api.BoundedContexts.BusinessSimulations.Domain.Enums;
using Api.BoundedContexts.BusinessSimulations.Domain.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.BusinessSimulations.Application.Queries;

/// <summary>
/// Unit tests for ExportLedgerQueryHandler (Issue #3724)
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "BusinessSimulations")]
public sealed class ExportLedgerQueryHandlerTests
{
    private readonly Mock<ILedgerEntryRepository> _repositoryMock;
    private readonly ExportLedgerQueryHandler _handler;

    public ExportLedgerQueryHandlerTests()
    {
        _repositoryMock = new Mock<ILedgerEntryRepository>();
        _handler = new ExportLedgerQueryHandler(_repositoryMock.Object);
    }

    private static List<LedgerEntry> CreateTestEntries() =>
    [
        LedgerEntry.CreateAutoEntry(
            new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc),
            LedgerEntryType.Income, LedgerCategory.Subscription, 299.99m, "EUR", "Pro subscription"),
        LedgerEntry.CreateAutoEntry(
            new DateTime(2026, 1, 20, 0, 0, 0, DateTimeKind.Utc),
            LedgerEntryType.Expense, LedgerCategory.Infrastructure, 45.50m, "EUR", "Cloud hosting"),
        LedgerEntry.CreateManualEntry(
            new DateTime(2026, 1, 25, 0, 0, 0, DateTimeKind.Utc),
            LedgerEntryType.Expense, LedgerCategory.Operational, 75m, Guid.NewGuid(), "EUR", "Office supplies"),
    ];

    private void SetupRepository(List<LedgerEntry> entries)
    {
        _repositoryMock
            .Setup(r => r.GetFilteredAsync(
                It.IsAny<LedgerEntryType?>(),
                It.IsAny<LedgerCategory?>(),
                It.IsAny<LedgerEntrySource?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                1,
                10000,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((entries.AsReadOnly(), entries.Count));
    }

    #region CSV Export

    [Fact]
    public async Task Handle_CsvFormat_ShouldReturnCsvContent()
    {
        // Arrange
        var entries = CreateTestEntries();
        SetupRepository(entries);
        var query = new ExportLedgerQuery(LedgerExportFormat.Csv, null, null, null, null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.ContentType.Should().Be("text/csv");
        result.FileName.Should().EndWith(".csv");
        result.Content.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_CsvFormat_ShouldContainHeader()
    {
        // Arrange
        var entries = CreateTestEntries();
        SetupRepository(entries);
        var query = new ExportLedgerQuery(LedgerExportFormat.Csv, null, null, null, null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);
        var csv = System.Text.Encoding.UTF8.GetString(result.Content);

        // Assert
        csv.Should().StartWith("Date,Type,Category,Amount,Currency,Source,Description,CreatedAt");
    }

    [Fact]
    public async Task Handle_CsvFormat_ShouldContainAllEntries()
    {
        // Arrange
        var entries = CreateTestEntries();
        SetupRepository(entries);
        var query = new ExportLedgerQuery(LedgerExportFormat.Csv, null, null, null, null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);
        var csv = System.Text.Encoding.UTF8.GetString(result.Content);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Assert (header + 3 data rows)
        lines.Should().HaveCount(4);
    }

    [Fact]
    public async Task Handle_CsvFormat_EmptyEntries_ShouldReturnHeaderOnly()
    {
        // Arrange
        SetupRepository([]);
        var query = new ExportLedgerQuery(LedgerExportFormat.Csv, null, null, null, null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);
        var csv = System.Text.Encoding.UTF8.GetString(result.Content);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Assert
        lines.Should().HaveCount(1);
        lines[0].Should().StartWith("Date,");
    }

    [Fact]
    public async Task Handle_CsvFormat_WithDateRange_ShouldBuildFileName()
    {
        // Arrange
        SetupRepository([]);
        var query = new ExportLedgerQuery(
            LedgerExportFormat.Csv,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc),
            null, null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.FileName.Should().Be("ledger-export-20260101-20260131.csv");
    }

    #endregion

    #region Excel Export

    [Fact]
    public async Task Handle_ExcelFormat_ShouldReturnExcelContent()
    {
        // Arrange
        var entries = CreateTestEntries();
        SetupRepository(entries);
        var query = new ExportLedgerQuery(LedgerExportFormat.Excel, null, null, null, null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.ContentType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        result.FileName.Should().EndWith(".xlsx");
        result.Content.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_ExcelFormat_ShouldContainValidXlsx()
    {
        // Arrange
        var entries = CreateTestEntries();
        SetupRepository(entries);
        var query = new ExportLedgerQuery(LedgerExportFormat.Excel, null, null, null, null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert - XLSX files start with PK (zip header)
        result.Content[0].Should().Be(0x50); // 'P'
        result.Content[1].Should().Be(0x4B); // 'K'
    }

    [Fact]
    public async Task Handle_ExcelFormat_EmptyEntries_ShouldReturnValidXlsx()
    {
        // Arrange
        SetupRepository([]);
        var query = new ExportLedgerQuery(LedgerExportFormat.Excel, null, null, null, null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Content.Should().NotBeEmpty();
        result.Content[0].Should().Be(0x50); // 'P'
    }

    #endregion

    #region PDF Export

    [Fact]
    public async Task Handle_PdfFormat_ShouldReturnPdfContent()
    {
        // Arrange
        var entries = CreateTestEntries();
        SetupRepository(entries);
        var query = new ExportLedgerQuery(LedgerExportFormat.Pdf, null, null, null, null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.ContentType.Should().Be("application/pdf");
        result.FileName.Should().EndWith(".pdf");
        result.Content.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_PdfFormat_ShouldContainValidPdf()
    {
        // Arrange
        var entries = CreateTestEntries();
        SetupRepository(entries);
        var query = new ExportLedgerQuery(LedgerExportFormat.Pdf, null, null, null, null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert - PDF files start with %PDF
        var header = System.Text.Encoding.ASCII.GetString(result.Content, 0, 4);
        header.Should().Be("%PDF");
    }

    [Fact]
    public async Task Handle_PdfFormat_EmptyEntries_ShouldReturnValidPdf()
    {
        // Arrange
        SetupRepository([]);
        var query = new ExportLedgerQuery(LedgerExportFormat.Pdf, null, null, null, null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Content.Should().NotBeEmpty();
        var header = System.Text.Encoding.ASCII.GetString(result.Content, 0, 4);
        header.Should().Be("%PDF");
    }

    [Fact]
    public async Task Handle_PdfFormat_WithDateRange_ShouldBuildReportFileName()
    {
        // Arrange
        SetupRepository([]);
        var query = new ExportLedgerQuery(
            LedgerExportFormat.Pdf,
            new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 2, 28, 0, 0, 0, DateTimeKind.Utc),
            null, null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.FileName.Should().Be("ledger-report-20260201-20260228.pdf");
    }

    #endregion

    #region Filters

    [Fact]
    public async Task Handle_WithTypeFilter_ShouldPassToRepository()
    {
        // Arrange
        SetupRepository([]);
        var query = new ExportLedgerQuery(LedgerExportFormat.Csv, null, null, LedgerEntryType.Income, null);

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        _repositoryMock.Verify(r => r.GetFilteredAsync(
            LedgerEntryType.Income, null, null, null, null, 1, 10000,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithCategoryFilter_ShouldPassToRepository()
    {
        // Arrange
        SetupRepository([]);
        var query = new ExportLedgerQuery(LedgerExportFormat.Csv, null, null, null, LedgerCategory.Subscription);

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        _repositoryMock.Verify(r => r.GetFilteredAsync(
            null, LedgerCategory.Subscription, null, null, null, 1, 10000,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithDateFilters_ShouldPassToRepository()
    {
        // Arrange
        SetupRepository([]);
        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc);
        var query = new ExportLedgerQuery(LedgerExportFormat.Csv, from, to, null, null);

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        _repositoryMock.Verify(r => r.GetFilteredAsync(
            null, null, null, from, to, 1, 10000,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NullRequest_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _handler.Handle(null!, CancellationToken.None));
    }

    #endregion
}
