using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.SystemConfiguration.Application.Validators;

internal sealed class RollbackConfigCommandValidator : AbstractValidator<RollbackConfigCommand>
{
    public RollbackConfigCommandValidator()
    {
        RuleFor(x => x.ConfigurationId)
            .NotEmpty()
            .WithMessage("ConfigurationId is required");

        RuleFor(x => x.TargetVersion)
            .GreaterThan(0)
            .WithMessage("TargetVersion must be greater than 0");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");
    }
}
