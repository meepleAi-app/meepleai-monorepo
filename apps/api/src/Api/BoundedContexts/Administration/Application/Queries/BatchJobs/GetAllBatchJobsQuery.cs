using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Domain.Enums;
using Api.SharedKernel.Application.Interfaces;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Queries.BatchJobs;

/// <summary>
/// Query to get all batch jobs with optional filtering and pagination (Issue #3693)
/// </summary>
internal sealed record GetAllBatchJobsQuery(
    JobStatus? Status = null,
    int Page = 1,
    int PageSize = 20) : IQuery<BatchJobListDto>;

/// <summary>
/// Validator for GetAllBatchJobsQuery (Issue #3693)
/// </summary>
internal sealed class GetAllBatchJobsQueryValidator : AbstractValidator<GetAllBatchJobsQuery>
{
    public GetAllBatchJobsQueryValidator()
    {
        RuleFor(x => x.Status)
            .IsInEnum()
            .When(x => x.Status.HasValue)
            .WithMessage("Invalid job status");

        RuleFor(x => x.Page)
            .GreaterThan(0)
            .WithMessage("Page must be greater than 0");

        RuleFor(x => x.PageSize)
            .GreaterThan(0)
            .WithMessage("Page size must be greater than 0")
            .LessThanOrEqualTo(100)
            .WithMessage("Page size must not exceed 100");
    }
}
