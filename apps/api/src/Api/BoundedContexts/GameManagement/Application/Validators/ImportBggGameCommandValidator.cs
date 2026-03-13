using Api.BoundedContexts.GameManagement.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators;

/// <summary>
/// FluentValidation validator for ImportBggGameCommand.
/// Game Night Improvvisata - E1-2: Import BGG game with tier enforcement.
/// </summary>
internal sealed class ImportBggGameCommandValidator : AbstractValidator<ImportBggGameCommand>
{
    public ImportBggGameCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required.");

        RuleFor(x => x.BggId)
            .GreaterThan(0)
            .WithMessage("BGG ID must be a positive integer.");
    }
}
