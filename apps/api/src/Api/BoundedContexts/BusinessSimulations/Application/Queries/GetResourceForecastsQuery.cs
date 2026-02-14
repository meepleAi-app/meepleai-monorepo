using Api.BoundedContexts.BusinessSimulations.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;
using FluentValidation;

namespace Api.BoundedContexts.BusinessSimulations.Application.Queries;

/// <summary>
/// Query to get saved resource forecast scenarios for a user.
/// Issue #3726: Resource Forecasting Simulator (Epic #3688)
/// </summary>
internal sealed record GetResourceForecastsQuery(
    Guid UserId,
    int Page,
    int PageSize) : IQuery<ResourceForecastsResponseDto>;

internal sealed class GetResourceForecastsQueryValidator : AbstractValidator<GetResourceForecastsQuery>
{
    public GetResourceForecastsQueryValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be at least 1");

        RuleFor(x => x.PageSize)
            .GreaterThanOrEqualTo(1)
            .LessThanOrEqualTo(100)
            .WithMessage("PageSize must be between 1 and 100");
    }
}
