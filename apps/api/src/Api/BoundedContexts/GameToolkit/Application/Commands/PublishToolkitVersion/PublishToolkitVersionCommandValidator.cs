using FluentValidation;

namespace Api.BoundedContexts.GameToolkit.Application.Commands.PublishToolkitVersion;

/// <summary>
/// FluentValidation rules for <see cref="PublishToolkitVersionCommand"/>.
/// Semver shape check duplicates the domain factory regex so the validator
/// can surface a 400 BadRequest before the handler dispatch — matches the
/// existing pattern (Wiegers spec-panel: validate at boundary).
/// </summary>
internal sealed class PublishToolkitVersionCommandValidator
    : AbstractValidator<PublishToolkitVersionCommand>
{
    public PublishToolkitVersionCommandValidator()
    {
        RuleFor(x => x.ToolkitId).NotEmpty().WithMessage("ToolkitId is required");
        RuleFor(x => x.ViewerId).NotEmpty().WithMessage("ViewerId is required");

        RuleFor(x => x.VersionNumber)
            .NotEmpty()
            .WithMessage("VersionNumber is required")
            .Matches(@"^\d+\.\d+\.\d+$")
            .WithMessage("VersionNumber must match semver MAJOR.MINOR.PATCH (e.g. '1.2.3')");

        RuleFor(x => x.Changelog)
            .MaximumLength(4000)
            .When(x => x.Changelog is not null)
            .WithMessage("Changelog cannot exceed 4000 characters");
    }
}
