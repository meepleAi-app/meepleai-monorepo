using FluentValidation;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Validator for BulkDeletePdfsCommand.
/// PDF Storage Management Hub: Phase 4.
/// </summary>
internal sealed class BulkDeletePdfsCommandValidator : AbstractValidator<BulkDeletePdfsCommand>
{
    public BulkDeletePdfsCommandValidator()
    {
        RuleFor(x => x.PdfIds)
            .NotEmpty()
            .WithMessage("At least one PDF ID must be provided")
            .Must(ids => ids.Count <= 50)
            .WithMessage("Cannot delete more than 50 documents at once");
    }
}
