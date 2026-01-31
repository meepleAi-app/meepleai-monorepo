using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Validator for UpdateGameErrataCommand.
/// </summary>
internal sealed class UpdateGameErrataCommandValidator : AbstractValidator<UpdateGameErrataCommand>
{
    public UpdateGameErrataCommandValidator()
    {
        RuleFor(x => x.ErrataId)
            .NotEqual(Guid.Empty).WithMessage("ErrataId is required");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required");

        RuleFor(x => x.PageReference)
            .NotEmpty().WithMessage("PageReference is required")
            .MaximumLength(100).WithMessage("PageReference cannot exceed 100 characters");

        RuleFor(x => x.PublishedDate)
            .LessThanOrEqualTo(DateTime.UtcNow).WithMessage("PublishedDate cannot be in the future");
    }
}
