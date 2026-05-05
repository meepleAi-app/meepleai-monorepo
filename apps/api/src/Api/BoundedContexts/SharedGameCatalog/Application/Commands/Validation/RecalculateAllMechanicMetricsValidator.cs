using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.Validation;

/// <summary>
/// Validator for <see cref="RecalculateAllMechanicMetricsCommand"/> (ADR-051 Sprint 1 / Task 25).
/// </summary>
/// <remarks>
/// The command carries no properties — it is a sentinel/dispatcher trigger — so the rule set is
/// intentionally empty. The validator exists purely for architectural consistency with the rest of
/// the command pipeline (every command in this bounded context has a paired validator registered
/// via the FluentValidation pipeline behavior).
/// </remarks>
internal sealed class RecalculateAllMechanicMetricsValidator
    : AbstractValidator<RecalculateAllMechanicMetricsCommand>
{
    public RecalculateAllMechanicMetricsValidator()
    {
    }
}
