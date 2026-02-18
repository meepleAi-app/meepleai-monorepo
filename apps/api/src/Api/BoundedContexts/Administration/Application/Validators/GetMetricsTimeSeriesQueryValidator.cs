using Api.BoundedContexts.Administration.Application.Queries;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Validators;

/// <summary>
/// Validator for GetMetricsTimeSeriesQuery.
/// Issue #901: Validates the time range is a defined enum value.
/// </summary>
internal sealed class GetMetricsTimeSeriesQueryValidator : AbstractValidator<GetMetricsTimeSeriesQuery>
{
    public GetMetricsTimeSeriesQueryValidator()
    {
        RuleFor(x => x.Range)
            .IsInEnum()
            .WithMessage("Invalid time range. Allowed values: OneHour, SixHours, TwentyFourHours, SevenDays");
    }
}
