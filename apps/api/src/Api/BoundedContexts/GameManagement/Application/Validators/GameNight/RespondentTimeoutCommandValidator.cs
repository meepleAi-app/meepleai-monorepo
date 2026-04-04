using Api.BoundedContexts.GameManagement.Application.Commands.GameNight;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators.GameNight;

/// <summary>
/// Validates <see cref="RespondentTimeoutCommand"/> inputs.
/// </summary>
internal sealed class RespondentTimeoutCommandValidator : AbstractValidator<RespondentTimeoutCommand>
{
    public RespondentTimeoutCommandValidator()
    {
        RuleFor(x => x.DisputeId)
            .NotEmpty()
            .WithMessage("Dispute ID is required");
    }
}
