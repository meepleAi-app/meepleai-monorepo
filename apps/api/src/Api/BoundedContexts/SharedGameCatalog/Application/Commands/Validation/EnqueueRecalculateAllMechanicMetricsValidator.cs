using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.Validation;

/// <summary>
/// Validator for <see cref="EnqueueRecalculateAllMechanicMetricsCommand"/>
/// (ADR-051 M2.1, Sprint 2 / Task 9).
/// </summary>
/// <remarks>
/// Enforces a non-empty <c>TriggeredByUserId</c> at the application boundary so the validation
/// pipeline behavior surfaces a 400 before the handler is invoked. The aggregate's
/// <see cref="Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates.MechanicRecalcJob.Enqueue"/>
/// factory enforces the same invariant for defense-in-depth.
/// </remarks>
internal sealed class EnqueueRecalculateAllMechanicMetricsValidator
    : AbstractValidator<EnqueueRecalculateAllMechanicMetricsCommand>
{
    public EnqueueRecalculateAllMechanicMetricsValidator()
    {
        RuleFor(x => x.TriggeredByUserId)
            .NotEmpty()
            .WithMessage("TriggeredByUserId is required.");
    }
}
