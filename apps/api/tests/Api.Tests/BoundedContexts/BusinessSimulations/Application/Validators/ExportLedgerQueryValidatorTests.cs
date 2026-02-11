using Api.BoundedContexts.BusinessSimulations.Application.Queries;
using Api.BoundedContexts.BusinessSimulations.Domain.Enums;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.BusinessSimulations.Application.Validators;

/// <summary>
/// Unit tests for ExportLedgerQueryValidator (Issue #3724)
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "BusinessSimulations")]
public sealed class ExportLedgerQueryValidatorTests
{
    private readonly ExportLedgerQueryValidator _validator = new();

    [Fact]
    public void ValidQuery_ShouldPassValidation()
    {
        var query = new ExportLedgerQuery(LedgerExportFormat.Csv, null, null, null, null);
        var result = _validator.TestValidate(query);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void CsvFormat_ShouldPassValidation()
    {
        var query = new ExportLedgerQuery(LedgerExportFormat.Csv, null, null, null, null);
        var result = _validator.TestValidate(query);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ExcelFormat_ShouldPassValidation()
    {
        var query = new ExportLedgerQuery(LedgerExportFormat.Excel, null, null, null, null);
        var result = _validator.TestValidate(query);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void PdfFormat_ShouldPassValidation()
    {
        var query = new ExportLedgerQuery(LedgerExportFormat.Pdf, null, null, null, null);
        var result = _validator.TestValidate(query);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void InvalidFormat_ShouldFailValidation()
    {
        var query = new ExportLedgerQuery((LedgerExportFormat)99, null, null, null, null);
        var result = _validator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.Format);
    }

    [Fact]
    public void WithValidDateRange_ShouldPassValidation()
    {
        var query = new ExportLedgerQuery(
            LedgerExportFormat.Pdf,
            DateTime.UtcNow.AddDays(-30),
            DateTime.UtcNow,
            null, null);
        var result = _validator.TestValidate(query);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void WithSameDate_ShouldPassValidation()
    {
        var date = DateTime.UtcNow;
        var query = new ExportLedgerQuery(LedgerExportFormat.Excel, date, date, null, null);
        var result = _validator.TestValidate(query);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void DateToBeforeDateFrom_ShouldFailValidation()
    {
        var query = new ExportLedgerQuery(
            LedgerExportFormat.Csv,
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(-5),
            null, null);
        var result = _validator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.DateTo);
    }

    [Fact]
    public void OnlyDateFrom_ShouldPassValidation()
    {
        var query = new ExportLedgerQuery(
            LedgerExportFormat.Csv, DateTime.UtcNow.AddDays(-7), null, null, null);
        var result = _validator.TestValidate(query);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void OnlyDateTo_ShouldPassValidation()
    {
        var query = new ExportLedgerQuery(
            LedgerExportFormat.Csv, null, DateTime.UtcNow, null, null);
        var result = _validator.TestValidate(query);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void WithValidType_ShouldPassValidation()
    {
        var query = new ExportLedgerQuery(
            LedgerExportFormat.Csv, null, null, LedgerEntryType.Income, null);
        var result = _validator.TestValidate(query);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void WithInvalidType_ShouldFailValidation()
    {
        var query = new ExportLedgerQuery(
            LedgerExportFormat.Csv, null, null, (LedgerEntryType)99, null);
        var result = _validator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.Type);
    }

    [Fact]
    public void WithValidCategory_ShouldPassValidation()
    {
        var query = new ExportLedgerQuery(
            LedgerExportFormat.Csv, null, null, null, LedgerCategory.Subscription);
        var result = _validator.TestValidate(query);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void WithInvalidCategory_ShouldFailValidation()
    {
        var query = new ExportLedgerQuery(
            LedgerExportFormat.Csv, null, null, null, (LedgerCategory)99);
        var result = _validator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.Category);
    }

    [Fact]
    public void WithAllFilters_ShouldPassValidation()
    {
        var query = new ExportLedgerQuery(
            LedgerExportFormat.Pdf,
            DateTime.UtcNow.AddDays(-30),
            DateTime.UtcNow,
            LedgerEntryType.Expense,
            LedgerCategory.Infrastructure);
        var result = _validator.TestValidate(query);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
