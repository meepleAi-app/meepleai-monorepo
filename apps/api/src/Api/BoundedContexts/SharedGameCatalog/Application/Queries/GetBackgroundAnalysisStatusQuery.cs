using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;
using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Query for retrieving background analysis status and progress.
/// Issue #2454: Background Processing for Large Rulebooks
/// </summary>
public sealed record GetBackgroundAnalysisStatusQuery(
    string TaskId,
    Guid SharedGameId,
    Guid PdfDocumentId
) : IQuery<BackgroundAnalysisStatusDto>;

/// <summary>
/// Validator for GetBackgroundAnalysisStatusQuery.
/// </summary>
public sealed class GetBackgroundAnalysisStatusQueryValidator : AbstractValidator<GetBackgroundAnalysisStatusQuery>
{
    public GetBackgroundAnalysisStatusQueryValidator()
    {
        RuleFor(x => x.TaskId)
            .NotEmpty()
            .WithMessage("Task ID is required");

        RuleFor(x => x.SharedGameId)
            .NotEmpty()
            .WithMessage("Shared game ID is required");

        RuleFor(x => x.PdfDocumentId)
            .NotEmpty()
            .WithMessage("PDF document ID is required");
    }
}
