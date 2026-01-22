using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Validator for StartReviewCommand.
/// </summary>
internal sealed class StartReviewCommandValidator : AbstractValidator<StartReviewCommand>
{
    public StartReviewCommandValidator()
    {
        RuleFor(x => x.ShareRequestId)
            .NotEmpty().WithMessage("ShareRequestId is required");

        RuleFor(x => x.ReviewingAdminId)
            .NotEmpty().WithMessage("ReviewingAdminId is required");
    }
}
