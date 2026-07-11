using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// Validator for FinalizeMechanicDraftCommand.
/// </summary>
internal sealed class FinalizeMechanicDraftCommandValidator : AbstractValidator<FinalizeMechanicDraftCommand>
{
    public FinalizeMechanicDraftCommandValidator()
    {
        RuleFor(x => x.DraftId)
            .NotEmpty()
            .WithMessage("Draft ID is required");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required");
    }
}
