using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Validator for ReleaseReviewCommand.
/// </summary>
internal sealed class ReleaseReviewCommandValidator : AbstractValidator<ReleaseReviewCommand>
{
    public ReleaseReviewCommandValidator()
    {
        RuleFor(x => x.ShareRequestId)
            .NotEmpty().WithMessage("ShareRequestId is required");

        RuleFor(x => x.ReviewingAdminId)
            .NotEmpty().WithMessage("ReviewingAdminId is required");
    }
}
