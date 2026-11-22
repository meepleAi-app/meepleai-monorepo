using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.DocumentProcessing.Application.Validators;

/// <summary>
/// Validator for <see cref="RenderPdfPageCoverCommand"/>: the same 1-based page boundary as
/// <see cref="MaterializePdfCoverCommandValidator"/>.
/// </summary>
internal sealed class RenderPdfPageCoverCommandValidator : AbstractValidator<RenderPdfPageCoverCommand>
{
    public RenderPdfPageCoverCommandValidator()
    {
        RuleFor(x => x.PageNumber).GreaterThan(0);
    }
}
