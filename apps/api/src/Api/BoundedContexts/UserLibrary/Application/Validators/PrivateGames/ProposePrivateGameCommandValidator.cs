using Api.BoundedContexts.UserLibrary.Application.Commands.PrivateGames;
using FluentValidation;

namespace Api.BoundedContexts.UserLibrary.Application.Validators.PrivateGames;

/// <summary>
/// Validator for <see cref="ProposePrivateGameCommand"/>. The description rule of #5074 needs the stored game, so it
/// lives in the handler (<c>PrivateGame.CanBeProposedToCatalog</c>).
/// </summary>
internal sealed class ProposePrivateGameCommandValidator : AbstractValidator<ProposePrivateGameCommand>
{
    public ProposePrivateGameCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.PrivateGameId)
            .NotEmpty()
            .WithMessage("PrivateGameId is required");

        RuleFor(x => x.Notes)
            .MaximumLength(2000)
            .WithMessage("Notes cannot exceed 2000 characters")
            .When(x => !string.IsNullOrWhiteSpace(x.Notes));
    }
}
