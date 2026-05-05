using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// FluentValidation rules for <see cref="SuppressMechanicAnalysisCommand"/>. The reason field
/// is legally significant — enforced length 20–500 chars at the Application boundary. The
/// orthogonal-suppression invariant ("already suppressed") is enforced by the aggregate and
/// surfaces as <c>ConflictException</c> (409) from the handler.
/// </summary>
internal sealed class SuppressMechanicAnalysisCommandValidator
    : AbstractValidator<SuppressMechanicAnalysisCommand>
{
    public SuppressMechanicAnalysisCommandValidator()
    {
        RuleFor(c => c.AnalysisId)
            .NotEmpty().WithMessage("AnalysisId is required.");

        RuleFor(c => c.ActorId)
            .NotEmpty().WithMessage("ActorId is required.");

        RuleFor(c => c.Reason)
            .NotEmpty().WithMessage("Reason is required (legal evidence chain).")
            .MinimumLength(20).WithMessage("Reason must be at least 20 characters.")
            .MaximumLength(500).WithMessage("Reason must not exceed 500 characters.");

        RuleFor(c => c.RequestSource)
            .IsInEnum().WithMessage("RequestSource must be a valid enum value (Email, Legal, Other).");

        RuleFor(c => c.RequestedAt)
            .Must(ts => !ts.HasValue || ts.Value.Kind == DateTimeKind.Utc || ts.Value.Kind == DateTimeKind.Unspecified)
            .WithMessage("RequestedAt must be expressed in UTC.");
    }
}
