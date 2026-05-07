using FluentValidation;

namespace Api.BoundedContexts.GameToolkit.Application.Queries.GetToolkitDetail;

/// <summary>
/// Validator for <see cref="GetToolkitDetailQuery"/>.
/// Wave 3 Phase 2, PR #732 §5.3.1 / Issue #805.
/// </summary>
internal sealed class GetToolkitDetailQueryValidator
    : AbstractValidator<GetToolkitDetailQuery>
{
    public GetToolkitDetailQueryValidator()
    {
        RuleFor(x => x.ToolkitId)
            .NotEmpty()
            .WithMessage("ToolkitId is required.");

        RuleFor(x => x.ViewerId)
            .NotEmpty()
            .WithMessage("ViewerId is required (caller must be authenticated).");
    }
}
