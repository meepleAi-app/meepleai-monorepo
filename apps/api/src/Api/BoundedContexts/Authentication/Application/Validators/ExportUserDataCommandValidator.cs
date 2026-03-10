using Api.BoundedContexts.Authentication.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.Authentication.Application.Validators;

/// <summary>
/// Validator for ExportUserDataCommand (GDPR Art. 20).
/// </summary>
internal sealed class ExportUserDataCommandValidator : AbstractValidator<ExportUserDataCommand>
{
    public ExportUserDataCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required for data export.");
    }
}
