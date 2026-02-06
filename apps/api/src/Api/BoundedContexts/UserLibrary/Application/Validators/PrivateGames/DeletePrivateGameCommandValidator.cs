using Api.BoundedContexts.UserLibrary.Application.Commands.PrivateGames;
using FluentValidation;

namespace Api.BoundedContexts.UserLibrary.Application.Validators.PrivateGames;

/// <summary>
/// Validator for DeletePrivateGameCommand.
/// Issue #3663: Phase 2 - Private Game CRUD Operations.
/// </summary>
internal sealed class DeletePrivateGameCommandValidator : AbstractValidator<DeletePrivateGameCommand>
{
    public DeletePrivateGameCommandValidator()
    {
        RuleFor(x => x.PrivateGameId)
            .NotEmpty()
            .WithMessage("PrivateGameId is required");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");
    }
}
