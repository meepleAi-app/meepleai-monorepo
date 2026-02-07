using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Commands.Resources;

/// <summary>
/// Validator for ClearCacheCommand.
/// Ensures confirmation is explicitly provided for this dangerous operation.
/// Issue #3695: Resources Monitoring - Clear cache validation
/// </summary>
internal class ClearCacheCommandValidator : AbstractValidator<ClearCacheCommand>
{
    public ClearCacheCommandValidator()
    {
        RuleFor(x => x.Confirmed)
            .Equal(true)
            .WithMessage("Confirmation is required to clear the cache. This action cannot be undone.");
    }
}
