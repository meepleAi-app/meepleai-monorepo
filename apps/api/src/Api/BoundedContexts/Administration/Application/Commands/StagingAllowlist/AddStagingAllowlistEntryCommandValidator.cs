using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Commands.StagingAllowlist;

internal sealed class AddStagingAllowlistEntryCommandValidator : AbstractValidator<AddStagingAllowlistEntryCommand>
{
    public AddStagingAllowlistEntryCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("A valid email address is required")
            .MaximumLength(320).WithMessage("Email cannot exceed 320 characters");

        RuleFor(x => x.Note)
            .MaximumLength(500).WithMessage("Note cannot exceed 500 characters");
    }
}
