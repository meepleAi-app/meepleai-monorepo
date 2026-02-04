using Api.BoundedContexts.UserLibrary.Application.Commands.Labels;
using FluentValidation;

namespace Api.BoundedContexts.UserLibrary.Application.Validators.Labels;

/// <summary>
/// Validator for AddLabelToGameCommand.
/// </summary>
internal sealed class AddLabelToGameCommandValidator : AbstractValidator<AddLabelToGameCommand>
{
    public AddLabelToGameCommandValidator()
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
