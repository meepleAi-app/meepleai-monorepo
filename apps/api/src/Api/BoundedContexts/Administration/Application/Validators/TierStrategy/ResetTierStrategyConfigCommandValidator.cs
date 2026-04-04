using Api.BoundedContexts.Administration.Application.Commands.TierStrategy;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Validators.TierStrategy;

/// <summary>
/// Validator for ResetTierStrategyConfigCommand.
/// Ensures at least one reset option is selected to prevent no-op calls.
/// </summary>
internal sealed class ResetTierStrategyConfigCommandValidator : AbstractValidator<ResetTierStrategyConfigCommand>
{
    public ResetTierStrategyConfigCommandValidator()
    {
        RuleFor(x => x)
            .Must(x => x.ResetAccessMatrix || x.ResetModelMappings)
            .WithMessage("At least one of ResetAccessMatrix or ResetModelMappings must be true.");
    }
}
