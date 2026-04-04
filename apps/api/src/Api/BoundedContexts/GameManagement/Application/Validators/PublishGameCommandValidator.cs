using Api.BoundedContexts.GameManagement.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators;

/// <summary>
/// Validator for PublishGameCommand.
/// Ensures GameId is a non-empty GUID and Status is a valid enum value.
/// </summary>
internal sealed class PublishGameCommandValidator : AbstractValidator<PublishGameCommand>
{
    public PublishGameCommandValidator()
    {
        RuleFor(x => x.GameId)
            .NotEmpty().WithMessage("Game ID is required");

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Invalid approval status");
    }
}
