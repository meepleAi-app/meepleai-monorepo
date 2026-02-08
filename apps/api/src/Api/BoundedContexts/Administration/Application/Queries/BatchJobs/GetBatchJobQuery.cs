using Api.BoundedContexts.Administration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Queries.BatchJobs;

/// <summary>
/// Query to get a batch job by ID (Issue #3693)
/// </summary>
internal sealed record GetBatchJobQuery(Guid JobId) : IQuery<BatchJobDto?>;

/// <summary>
/// Validator for GetBatchJobQuery (Issue #3693)
/// </summary>
internal sealed class GetBatchJobQueryValidator : AbstractValidator<GetBatchJobQuery>
{
    public GetBatchJobQueryValidator()
    {
        RuleFor(x => x.JobId)
            .NotEmpty()
            .WithMessage("Job ID is required");
    }
}
