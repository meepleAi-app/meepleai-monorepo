using Api.BoundedContexts.BusinessSimulations.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;
using FluentValidation;

namespace Api.BoundedContexts.BusinessSimulations.Application.Queries;

/// <summary>
/// Query to get saved cost scenarios for a user.
/// Issue #3725: Agent Cost Calculator (Epic #3688)
/// </summary>
internal sealed record GetCostScenariosQuery(
    Guid UserId,
    int Page,
    int PageSize) : IQuery<CostScenariosResponseDto>;

internal sealed class GetCostScenariosQueryValidator : AbstractValidator<GetCostScenariosQuery>
{
    public GetCostScenariosQueryValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Page must be at least 1");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("PageSize must be between 1 and 100");
    }
}
