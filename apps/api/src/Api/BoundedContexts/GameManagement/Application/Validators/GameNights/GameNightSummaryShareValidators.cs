using Api.BoundedContexts.GameManagement.Application.Commands.GameNights;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators.GameNights;

/// <summary>Validator for GenerateGameNightShareTokenCommand — Issue #2702.</summary>
internal sealed class GenerateGameNightShareTokenCommandValidator
    : AbstractValidator<GenerateGameNightShareTokenCommand>
{
    public GenerateGameNightShareTokenCommandValidator()
    {
        RuleFor(x => x.GameNightId).NotEmpty().WithMessage("Game night ID is required");
        RuleFor(x => x.UserId).NotEmpty().WithMessage("User ID is required");
    }
}

/// <summary>Validator for RevokeGameNightShareTokenCommand — Issue #2702.</summary>
internal sealed class RevokeGameNightShareTokenCommandValidator
    : AbstractValidator<RevokeGameNightShareTokenCommand>
{
    public RevokeGameNightShareTokenCommandValidator()
    {
        RuleFor(x => x.GameNightId).NotEmpty().WithMessage("Game night ID is required");
        RuleFor(x => x.UserId).NotEmpty().WithMessage("User ID is required");
    }
}

/// <summary>Validator for SetGameNightArchivedCommand — Issue #2702.</summary>
internal sealed class SetGameNightArchivedCommandValidator
    : AbstractValidator<SetGameNightArchivedCommand>
{
    public SetGameNightArchivedCommandValidator()
    {
        RuleFor(x => x.GameNightId).NotEmpty().WithMessage("Game night ID is required");
        RuleFor(x => x.UserId).NotEmpty().WithMessage("User ID is required");
    }
}
