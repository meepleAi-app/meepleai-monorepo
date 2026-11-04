using Api.BoundedContexts.KnowledgeBase.Application.Queries.RagExecution;
using Api.BoundedContexts.KnowledgeBase.Application.Validators;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Tests for <see cref="GetRagExecutionsQueryValidator"/> and
/// <see cref="GetRagExecutionStatsQueryValidator"/>.
/// </summary>
/// <remarks>
/// An inverted window used to be rejected by accident: it produced the same Npgsql
/// <c>Kind=Unspecified</c> failure as every other dated request, so <c>dateFrom=2026-10-05&amp;
/// dateTo=2026-10-04</c> and the valid <c>dateFrom=dateTo=2026-10-04</c> returned the same 400
/// (measured 2026-10-04 against the local stack). Normalizing the window removes that accident, so
/// the ordering rule has to be stated — and must not take the degenerate equal-dates window with it,
/// since that is what the inspector sends on open.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Feature", "RagExecutionHistory")]
public sealed class RagExecutionDateWindowValidatorsTests
{
    private static readonly DateTime Oct3 = new(2026, 10, 3, 0, 0, 0, DateTimeKind.Unspecified);
    private static readonly DateTime Oct4 = new(2026, 10, 4, 0, 0, 0, DateTimeKind.Unspecified);
    private static readonly DateTime Oct5 = new(2026, 10, 5, 0, 0, 0, DateTimeKind.Unspecified);

    private readonly GetRagExecutionsQueryValidator _listValidator = new();
    private readonly GetRagExecutionStatsQueryValidator _statsValidator = new();

    [Fact]
    public void List_EqualDates_IsValid()
    {
        _listValidator.Validate(new GetRagExecutionsQuery(From: Oct4, To: Oct4))
            .IsValid.Should().BeTrue();
    }

    [Fact]
    public void List_AscendingDates_IsValid()
    {
        _listValidator.Validate(new GetRagExecutionsQuery(From: Oct3, To: Oct4))
            .IsValid.Should().BeTrue();
    }

    [Fact]
    public void List_InvertedDates_IsRejected()
    {
        var result = _listValidator.Validate(new GetRagExecutionsQuery(From: Oct5, To: Oct4));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("dateTo must be greater than or equal to dateFrom");
    }

    [Fact]
    public void List_OpenEndedOrAbsentWindow_IsValid()
    {
        _listValidator.Validate(new GetRagExecutionsQuery(From: Oct4)).IsValid.Should().BeTrue();
        _listValidator.Validate(new GetRagExecutionsQuery(To: Oct4)).IsValid.Should().BeTrue();
        _listValidator.Validate(new GetRagExecutionsQuery()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void List_UpperBoundEarlierOnTheSameDay_IsValidBecauseTheDayIsTheBound()
    {
        // dateFrom names an instant, dateTo names a day: the normalized window 15:00 → 23:59:59.9
        // is non-empty, so the validator must agree with the handler and accept it.
        var fromAfternoon = new DateTime(2026, 10, 4, 15, 0, 0, DateTimeKind.Unspecified);

        _listValidator.Validate(new GetRagExecutionsQuery(From: fromAfternoon, To: Oct4))
            .IsValid.Should().BeTrue();
    }

    [Fact]
    public void Stats_EqualDates_IsValid()
    {
        _statsValidator.Validate(new GetRagExecutionStatsQuery(From: Oct4, To: Oct4))
            .IsValid.Should().BeTrue();
    }

    [Fact]
    public void Stats_InvertedDates_IsRejected()
    {
        _statsValidator.Validate(new GetRagExecutionStatsQuery(From: Oct5, To: Oct4))
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public void Stats_NoWindow_IsValid()
    {
        _statsValidator.Validate(new GetRagExecutionStatsQuery()).IsValid.Should().BeTrue();
    }
}
