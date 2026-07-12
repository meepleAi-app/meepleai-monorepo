using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>Validates <see cref="SubmitMechanicCardFeedbackCommand"/> (#533).</summary>
internal sealed class SubmitMechanicCardFeedbackCommandValidator
    : AbstractValidator<SubmitMechanicCardFeedbackCommand>
{
    private static readonly string[] AllowedErrorTypes = { "factual", "ambiguous", "contradicts_rule" };

    public SubmitMechanicCardFeedbackCommandValidator()
    {
        RuleFor(x => x.CardId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.ClaimId).NotEmpty();

        // ErrorType only applies to a 👎 and, when present, must be a known category
        // (mirrors the DB CHECK). A 👍 must not carry an error type.
        When(x => x.IsPositive, () =>
        {
            RuleFor(x => x.ErrorType)
                .Null()
                .WithMessage("A positive feedback must not carry an error type.");
        });

        When(x => !string.IsNullOrWhiteSpace(x.ErrorType), () =>
        {
            RuleFor(x => x.ErrorType!)
                .Must(t => AllowedErrorTypes.Contains(t, StringComparer.Ordinal))
                .WithMessage("ErrorType must be one of: factual, ambiguous, contradicts_rule.");
        });

        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.SuggestedCitation).MaximumLength(1000);
    }
}
