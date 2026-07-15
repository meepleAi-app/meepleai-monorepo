using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.DocumentProcessing.Application.Validators;

/// <summary>
/// Validator for <see cref="MaterializePdfCoverCommand"/> (Issue #2949 Task 3).
/// Defense-in-depth: <c>PageNumber</c> is 1-based (render/query contract). The
/// handler converts it to a 0-based index via <c>PageNumber - 1</c>, so a value of
/// 0 or below is semantically invalid and would surface downstream as a 500. This
/// validator enforces the 1-based invariant at the boundary, yielding a clean 400
/// for any future direct sender.
/// </summary>
internal sealed class MaterializePdfCoverCommandValidator : AbstractValidator<MaterializePdfCoverCommand>
{
    public MaterializePdfCoverCommandValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThan(0);
    }
}
