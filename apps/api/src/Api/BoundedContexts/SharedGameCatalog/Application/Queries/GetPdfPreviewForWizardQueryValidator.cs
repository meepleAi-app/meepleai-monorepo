using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Validator for GetPdfPreviewForWizardQuery.
/// Ensures FilePath and UserId are valid before processing.
/// Issue #4139: Backend - API Endpoints PDF Wizard
/// </summary>
internal sealed class GetPdfPreviewForWizardQueryValidator : AbstractValidator<GetPdfPreviewForWizardQuery>
{
    public GetPdfPreviewForWizardQueryValidator()
    {
        RuleFor(x => x.FilePath)
            .NotEmpty()
            .WithMessage("File path is required")
            .MaximumLength(500)
            .WithMessage("File path must not exceed 500 characters");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required");
    }
}
