using FluentValidation;

namespace Api.BoundedContexts.Authentication.Application.Commands.AccessRequest;

/// <summary>
/// Validates SetRegistrationModeCommand.
/// Ensures admin ID is provided.
/// </summary>
internal sealed class SetRegistrationModeCommandValidator : AbstractValidator<SetRegistrationModeCommand>
{
    public SetRegistrationModeCommandValidator()
    {
        RuleFor(x => x.AdminId)
            .NotEmpty()
            .WithMessage("Admin ID is required");
    }
}
