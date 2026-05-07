using FluentValidation;

namespace Api.BoundedContexts.GameToolkit.Application.Commands.InstallToolkit;

/// <summary>
/// Validator for <see cref="InstallToolkitCommand"/>.
/// Wave 3 Phase 2, PR #732 §5.3.5 / Issue #805.
/// </summary>
internal sealed class InstallToolkitCommandValidator
    : AbstractValidator<InstallToolkitCommand>
{
    public InstallToolkitCommandValidator()
    {
        RuleFor(x => x.ToolkitId)
            .NotEmpty()
            .WithMessage("ToolkitId is required.");

        RuleFor(x => x.ViewerId)
            .NotEmpty()
            .WithMessage("ViewerId is required (caller must be authenticated).");
    }
}
