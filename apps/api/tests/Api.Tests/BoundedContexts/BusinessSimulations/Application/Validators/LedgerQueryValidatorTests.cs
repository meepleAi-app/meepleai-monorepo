using Api.BoundedContexts.BusinessSimulations.Application.Queries;
using Api.BoundedContexts.BusinessSimulations.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.BusinessSimulations.Application.Validators;

/// <summary>
/// Unit tests for ledger CRUD query validators (Issue #3722)
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "BusinessSimulations")]
public sealed class LedgerQueryValidatorTests
{
    #region GetLedgerEntriesQueryValidator

    private readonly GetLedgerEntriesQueryValidator _entriesValidator = new();

    [Fact]
    public void Entries_ValidQuery_ShouldPassValidation()
    {
        var query = new GetLedgerEntriesQuery(1, 20, null, null, null, null, null);
        var result = _entriesValidator.TestValidate(query);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Entries_WithAllFilters_ShouldPassValidation()
    {
        var query = new GetLedgerEntriesQuery(
            2, 50, LedgerEntryType.Income, LedgerCategory.Subscription,
            LedgerEntrySource.Manual, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);

        var result = _entriesValidator.TestValidate(query);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Entries_PageZero_ShouldFailValidation()
    {
        var query = new GetLedgerEntriesQuery(0, 20, null, null, null, null, null);
        var result = _entriesValidator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.Page);
    }

    [Fact]
    public void Entries_PageSizeTooLarge_ShouldFailValidation()
    {
        var query = new GetLedgerEntriesQuery(1, 101, null, null, null, null, null);
        var result = _entriesValidator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.PageSize);
    }

    [Fact]
    public void Entries_PageSizeZero_ShouldFailValidation()
    {
        var query = new GetLedgerEntriesQuery(1, 0, null, null, null, null, null);
        var result = _entriesValidator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.PageSize);
    }

    [Fact]
    public void Entries_DateToBeforeDateFrom_ShouldFailValidation()
    {
        var query = new GetLedgerEntriesQuery(
            1, 20, null, null, null, DateTime.UtcNow, DateTime.UtcNow.AddDays(-5));

        var result = _entriesValidator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.DateTo);
    }

    [Fact]
    public void Entries_OnlyDateFrom_ShouldPassValidation()
    {
        var query = new GetLedgerEntriesQuery(1, 20, null, null, null, DateTime.UtcNow.AddDays(-7), null);
        var result = _entriesValidator.TestValidate(query);
        result.ShouldNotHaveAnyValidationErrors();
    }

    #endregion

    #region GetLedgerEntryByIdQueryValidator

    private readonly GetLedgerEntryByIdQueryValidator _byIdValidator = new();

    [Fact]
    public void ById_ValidId_ShouldPassValidation()
    {
        var query = new GetLedgerEntryByIdQuery(Guid.NewGuid());
        var result = _byIdValidator.TestValidate(query);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ById_EmptyId_ShouldFailValidation()
    {
        var query = new GetLedgerEntryByIdQuery(Guid.Empty);
        var result = _byIdValidator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.Id);
    }

    #endregion

    #region GetLedgerSummaryQueryValidator

    private readonly GetLedgerSummaryQueryValidator _summaryValidator = new();

    [Fact]
    public void Summary_ValidDateRange_ShouldPassValidation()
    {
        var query = new GetLedgerSummaryQuery(DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);
        var result = _summaryValidator.TestValidate(query);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Summary_SameDate_ShouldPassValidation()
    {
        var date = DateTime.UtcNow;
        var query = new GetLedgerSummaryQuery(date, date);
        var result = _summaryValidator.TestValidate(query);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Summary_DateToBeforeDateFrom_ShouldFailValidation()
    {
        var query = new GetLedgerSummaryQuery(DateTime.UtcNow, DateTime.UtcNow.AddDays(-5));
        var result = _summaryValidator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.DateTo);
    }

    #endregion
}
