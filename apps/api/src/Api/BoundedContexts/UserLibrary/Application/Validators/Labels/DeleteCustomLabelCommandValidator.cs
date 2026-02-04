using Api.BoundedContexts.UserLibrary.Application.Commands.Labels;
using FluentValidation;

namespace Api.BoundedContexts.UserLibrary.Application.Validators.Labels;

/// <summary>
/// Validator for DeleteCustomLabelCommand.
/// </summary>
internal sealed class DeleteCustomLabelCommandValidator : AbstractValidator<DeleteCustomLabelCommand>
{
    public DeleteCustomLabelCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.LabelId)
            .NotEmpty()
            .WithMessage("LabelId is required");
    }
}
