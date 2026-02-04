using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Validator for ApproveDocumentForRagProcessingCommand.
/// </summary>
internal sealed class ApproveDocumentForRagProcessingCommandValidator : AbstractValidator<ApproveDocumentForRagProcessingCommand>
{
    public ApproveDocumentForRagProcessingCommandValidator()
    {
        RuleFor(x => x.DocumentId)
            .NotEqual(Guid.Empty).WithMessage("DocumentId is required");

        RuleFor(x => x.ApprovedBy)
            .NotEqual(Guid.Empty).WithMessage("ApprovedBy is required for audit trail");

        RuleFor(x => x.Notes)
            .MaximumLength(500).WithMessage("Notes cannot exceed 500 characters")
            .When(x => !string.IsNullOrEmpty(x.Notes));
    }
}
