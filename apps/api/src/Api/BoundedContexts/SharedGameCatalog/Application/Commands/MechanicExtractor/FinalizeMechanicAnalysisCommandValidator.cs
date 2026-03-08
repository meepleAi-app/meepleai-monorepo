using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// Validator for FinalizeMechanicAnalysisCommand.
/// </summary>
internal sealed class FinalizeMechanicAnalysisCommandValidator : AbstractValidator<FinalizeMechanicAnalysisCommand>
{
    public FinalizeMechanicAnalysisCommandValidator()
    {
        RuleFor(x => x.DraftId)
            .NotEmpty()
            .WithMessage("Draft ID is required");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required");
    }
}
