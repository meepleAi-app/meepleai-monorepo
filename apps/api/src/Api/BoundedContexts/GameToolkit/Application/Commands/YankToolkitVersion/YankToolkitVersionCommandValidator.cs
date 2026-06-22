using FluentValidation;

namespace Api.BoundedContexts.GameToolkit.Application.Commands.YankToolkitVersion;

/// <summary>
/// FluentValidation rules for <see cref="YankToolkitVersionCommand"/>.
/// Reason length cap mirrors the domain invariant (500 chars).
/// </summary>
internal sealed class YankToolkitVersionCommandValidator
    : AbstractValidator<YankToolkitVersionCommand>
{
    public YankToolkitVersionCommandValidator()
    {
        RuleFor(x => x.ToolkitId).NotEmpty().WithMessage("ToolkitId is required");
        RuleFor(x => x.VersionId).NotEmpty().WithMessage("VersionId is required");
        RuleFor(x => x.ViewerId).NotEmpty().WithMessage("ViewerId is required");

        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithMessage("Yank reason is required for audit")
            .MaximumLength(500)
            .WithMessage("Yank reason cannot exceed 500 characters");
    }
}
