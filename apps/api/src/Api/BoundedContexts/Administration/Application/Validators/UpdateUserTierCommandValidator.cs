using Api.BoundedContexts.Administration.Application.Commands;
using Api.SharedKernel.Domain.ValueObjects;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Validators;

/// <summary>
/// Validator for UpdateUserTierCommand.
/// Ensures user ID, tier name, and requester ID are valid.
/// </summary>
internal sealed class UpdateUserTierCommandValidator : AbstractValidator<UpdateUserTierCommand>
{

    public UpdateUserTierCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        // #3842 — qui c'era un elenco proprio, { Free, Basic, Pro, Enterprise }, confrontato in
        // modo case-sensitive. Sbagliato tre volte: conteneva "Basic", che il dominio non conosce;
        // ometteva "normal" e "premium", che invece riconosce; e rifiutava le minuscole, cioe' la
        // forma in cui i tier sono scritti nel database. Ogni richiesta finiva in 422.
        //
        // Il vocabolario e' uno solo, e sta in UserTier.
        RuleFor(x => x.NewTier)
            .NotEmpty()
            .WithMessage("NewTier is required")
            .Must(UserTier.IsValid)
            .WithMessage($"NewTier must be one of: {string.Join(", ", UserTier.All)}");

        RuleFor(x => x.RequesterUserId)
            .NotEmpty()
            .WithMessage("RequesterUserId is required");
    }
}
