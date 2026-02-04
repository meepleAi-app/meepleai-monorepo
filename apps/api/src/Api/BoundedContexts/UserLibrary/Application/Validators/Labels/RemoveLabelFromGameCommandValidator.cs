using Api.BoundedContexts.UserLibrary.Application.Commands.Labels;
using FluentValidation;

namespace Api.BoundedContexts.UserLibrary.Application.Validators.Labels;

/// <summary>
/// Validator for RemoveLabelFromGameCommand.
/// </summary>
internal sealed class RemoveLabelFromGameCommandValidator : AbstractValidator<RemoveLabelFromGameCommand>
{
    public RemoveLabelFromGameCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.GameId)
            .NotEmpty()
            .WithMessage("GameId is required");

        RuleFor(x => x.LabelId)
            .NotEmpty()
            .WithMessage("LabelId is required");
    }
}
