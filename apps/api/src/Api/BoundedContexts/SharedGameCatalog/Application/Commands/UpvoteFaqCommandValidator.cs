using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Validator for UpvoteFaqCommand.
/// Issue #2681: Public FAQs endpoints
/// </summary>
internal sealed class UpvoteFaqCommandValidator : AbstractValidator<UpvoteFaqCommand>
{
    public UpvoteFaqCommandValidator()
    {
        RuleFor(x => x.FaqId)
            .NotEqual(Guid.Empty).WithMessage("FaqId is required");
    }
}
