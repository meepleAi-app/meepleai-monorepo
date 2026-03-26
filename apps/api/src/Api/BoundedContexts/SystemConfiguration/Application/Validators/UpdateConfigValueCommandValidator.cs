using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.SystemConfiguration.Application.Validators;

internal sealed class UpdateConfigValueCommandValidator : AbstractValidator<UpdateConfigValueCommand>
{
    public UpdateConfigValueCommandValidator()
    {
        RuleFor(x => x.ConfigId)
            .NotEmpty()
            .WithMessage("ConfigId is required");

        RuleFor(x => x.NewValue)
            .NotEmpty()
            .WithMessage("NewValue is required")
            .MaximumLength(10000)
            .WithMessage("NewValue must not exceed 10000 characters");

        RuleFor(x => x.UpdatedByUserId)
            .NotEmpty()
            .WithMessage("UpdatedByUserId is required");
    }
}
