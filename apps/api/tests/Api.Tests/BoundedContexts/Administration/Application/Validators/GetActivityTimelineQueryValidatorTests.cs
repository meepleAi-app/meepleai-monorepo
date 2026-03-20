using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Application.Validators;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.Administration.Application.Validators;

/// <summary>
/// Unit tests for GetActivityTimelineQueryValidator (Issue #3923).
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class GetActivityTimelineQueryValidatorTests
{
    private readonly GetActivityTimelineQueryValidator _validator = new();

    [Fact]
    public void Validate_ValidQuery_ShouldPass()
    {
        var query = new GetActivityTimelineQuery(Guid.NewGuid());
        var result = _validator.TestValidate(query);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyUserId_ShouldFail()
    {
        var query = new GetActivityTimelineQuery(Guid.Empty);
        var result = _validator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.UserId);
    }

    [Fact]
    public void Validate_NegativeSkip_ShouldFail()
    {
        var query = new GetActivityTimelineQuery(Guid.NewGuid(), Skip: -1);
        var result = _validator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.Skip);
    }

    [Fact]
    public void Validate_ZeroSkip_ShouldPass()
    {
        var query = new GetActivityTimelineQuery(Guid.NewGuid(), Skip: 0);
        var result = _validator.TestValidate(query);
        result.ShouldNotHaveValidationErrorFor(x => x.Skip);
    }

    [Fact]
    public void Validate_TakeZero_ShouldFail()
    {
        var query = new GetActivityTimelineQuery(Guid.NewGuid(), Take: 0);
        var result = _validator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.Take);
    }

    [Fact]
    public void Validate_TakeOverMax_ShouldFail()
    {
        var query = new GetActivityTimelineQuery(Guid.NewGuid(), Take: 101);
        var result = _validator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.Take);
    }

    [Fact]
    public void Validate_TakeAtMax_ShouldPass()
    {
        var query = new GetActivityTimelineQuery(Guid.NewGuid(), Take: 100);
        var result = _validator.TestValidate(query);
        result.ShouldNotHaveValidationErrorFor(x => x.Take);
    }

    [Fact]
    public void Validate_SearchTermTooLong_ShouldFail()
    {
        var longSearch = new string('a', 201);
        var query = new GetActivityTimelineQuery(Guid.NewGuid(), SearchTerm: longSearch);
        var result = _validator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.SearchTerm);
    }

    [Fact]
    public void Validate_SearchTermAtMaxLength_ShouldPass()
    {
        var maxSearch = new string('a', 200);
        var query = new GetActivityTimelineQuery(Guid.NewGuid(), SearchTerm: maxSearch);
        var result = _validator.TestValidate(query);
        result.ShouldNotHaveValidationErrorFor(x => x.SearchTerm);
    }

    [Fact]
    public void Validate_NullSearchTerm_ShouldPass()
    {
        var query = new GetActivityTimelineQuery(Guid.NewGuid(), SearchTerm: null);
        var result = _validator.TestValidate(query);
        result.ShouldNotHaveValidationErrorFor(x => x.SearchTerm);
    }

    [Fact]
    public void Validate_ValidTypes_ShouldPass()
    {
        var query = new GetActivityTimelineQuery(
            Guid.NewGuid(),
            Types: ["game_added", "session_completed"]);
        var result = _validator.TestValidate(query);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_InvalidType_ShouldFail()
    {
        var query = new GetActivityTimelineQuery(
            Guid.NewGuid(),
            Types: ["invalid_type"]);
        var result = _validator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.Types);
    }

    [Fact]
    public void Validate_NullTypes_ShouldPass()
    {
        var query = new GetActivityTimelineQuery(Guid.NewGuid(), Types: null);
        var result = _validator.TestValidate(query);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyTypesArray_ShouldPass()
    {
        var query = new GetActivityTimelineQuery(Guid.NewGuid(), Types: []);
        var result = _validator.TestValidate(query);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_AllValidTypes_ShouldPass()
    {
        var query = new GetActivityTimelineQuery(
            Guid.NewGuid(),
            Types: ["game_added", "session_completed", "chat_saved", "wishlist_added"]);
        var result = _validator.TestValidate(query);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_FullQuery_WithAllParameters_ShouldPass()
    {
        var query = new GetActivityTimelineQuery(
            Guid.NewGuid(),
            Types: ["game_added", "session_completed"],
            SearchTerm: "wingspan",
            DateFrom: new DateTime(2026, 1, 1),
            DateTo: new DateTime(2026, 6, 30),
            Skip: 10,
            Take: 50,
            Order: SortDirection.Ascending);
        var result = _validator.TestValidate(query);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_DateToBeforeDateFrom_ShouldFail()
    {
        var query = new GetActivityTimelineQuery(
            Guid.NewGuid(),
            DateFrom: new DateTime(2026, 6, 1),
            DateTo: new DateTime(2026, 1, 1));
        var result = _validator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.DateTo);
    }

    [Fact]
    public void Validate_DateRangeExceeds365Days_ShouldFail()
    {
        var query = new GetActivityTimelineQuery(
            Guid.NewGuid(),
            DateFrom: new DateTime(2025, 1, 1),
            DateTo: new DateTime(2026, 2, 1));
        var result = _validator.TestValidate(query);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Date range must not exceed 1 year (365 days)");
    }

    [Fact]
    public void Validate_DateRangeExactly365Days_ShouldPass()
    {
        var from = new DateTime(2025, 2, 14);
        var to = from.AddDays(365);
        var query = new GetActivityTimelineQuery(
            Guid.NewGuid(), DateFrom: from, DateTo: to);
        var result = _validator.TestValidate(query);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_OnlyDateFrom_ShouldPass()
    {
        var query = new GetActivityTimelineQuery(
            Guid.NewGuid(), DateFrom: new DateTime(2026, 1, 1));
        var result = _validator.TestValidate(query);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_SameDateFromAndDateTo_ShouldPass()
    {
        var date = new DateTime(2026, 6, 15);
        var query = new GetActivityTimelineQuery(
            Guid.NewGuid(), DateFrom: date, DateTo: date);
        var result = _validator.TestValidate(query);
        result.ShouldNotHaveAnyValidationErrors();
    }
}