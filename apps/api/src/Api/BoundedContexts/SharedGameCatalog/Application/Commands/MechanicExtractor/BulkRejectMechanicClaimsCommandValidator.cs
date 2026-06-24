using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// FluentValidation rules for <see cref="BulkRejectMechanicClaimsCommand"/>. The 1..500 char
/// range on <c>Reason</c> matches <see cref="RejectMechanicClaimCommandValidator"/>; the
/// non-empty <c>ClaimIds</c> rule enforces decision D3 (explicit client-computed id set).
/// </summary>
internal sealed class BulkRejectMechanicClaimsCommandValidator
    : AbstractValidator<BulkRejectMechanicClaimsCommand>
{
    public const int MinReasonLength = 1;
    public const int MaxReasonLength = 500;

    public BulkRejectMechanicClaimsCommandValidator()
    {
        RuleFor(c => c.AnalysisId)
            .NotEmpty().WithMessage("AnalysisId is required.");

        RuleFor(c => c.ReviewerId)
            .NotEmpty().WithMessage("ReviewerId is required.");

        RuleFor(c => c.ClaimIds)
            .NotNull().WithMessage("ClaimIds is required.")
            .Must(ids => ids is { Count: > 0 }).WithMessage("ClaimIds must contain at least one claim.")
            .Must(ids => ids is null || ids.All(id => id != Guid.Empty))
                .WithMessage("ClaimIds cannot contain an empty id.");

        RuleFor(c => c.Reason)
            .NotEmpty().WithMessage("Rejection reason is required.")
            .Must(n => !string.IsNullOrWhiteSpace(n))
                .WithMessage("Rejection reason cannot be whitespace only.")
            .Length(MinReasonLength, MaxReasonLength)
                .WithMessage($"Rejection reason must be between {MinReasonLength} and {MaxReasonLength} characters.");
    }
}
