using FluentValidation;

namespace Api.BoundedContexts.GameToolkit.Application.Queries.GetToolkitVersions;

/// <summary>
/// Validator for <see cref="GetToolkitVersionsQuery"/>.
/// Wave 3 Phase 2, PR #732 §5.3.2 / Issue #805.
/// </summary>
internal sealed class GetToolkitVersionsQueryValidator
    : AbstractValidator<GetToolkitVersionsQuery>
{
    public GetToolkitVersionsQueryValidator()
    {
        RuleFor(x => x.ToolkitId)
            .NotEmpty()
            .WithMessage("ToolkitId is required.");

        RuleFor(x => x.ViewerId)
            .NotEmpty()
            .WithMessage("ViewerId is required (caller must be authenticated).");
    }
}
