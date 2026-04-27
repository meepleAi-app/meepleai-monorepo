using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// FluentValidation rules for <see cref="RejectMechanicClaimCommand"/>. The 1..500 char range
/// on <c>Note</c> matches the admin-suppression note convention and keeps audit messages short
/// enough to render inline in the claims list.
/// </summary>
internal sealed class RejectMechanicClaimCommandValidator
    : AbstractValidator<RejectMechanicClaimCommand>
{
    public const int MinNoteLength = 1;
    public const int MaxNoteLength = 500;

    public RejectMechanicClaimCommandValidator()
    {
        RuleFor(c => c.AnalysisId)
            .NotEmpty().WithMessage("AnalysisId is required.");

        RuleFor(c => c.ClaimId)
            .NotEmpty().WithMessage("ClaimId is required.");

        RuleFor(c => c.ReviewerId)
            .NotEmpty().WithMessage("ReviewerId is required.");

        RuleFor(c => c.Note)
            .NotEmpty().WithMessage("Rejection note is required.")
            .Must(n => !string.IsNullOrWhiteSpace(n))
                .WithMessage("Rejection note cannot be whitespace only.")
            .Length(MinNoteLength, MaxNoteLength)
                .WithMessage($"Rejection note must be between {MinNoteLength} and {MaxNoteLength} characters.");
    }
}
