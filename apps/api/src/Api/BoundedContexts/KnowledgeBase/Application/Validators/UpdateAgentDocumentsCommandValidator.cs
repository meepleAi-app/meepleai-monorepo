using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for UpdateAgentDocumentsCommand.
/// Issue #2399: Knowledge Base Document Selection.
/// </summary>
internal sealed class UpdateAgentDocumentsCommandValidator : AbstractValidator<UpdateAgentDocumentsCommand>
{
    private const int MaxDocuments = 50;

    public UpdateAgentDocumentsCommandValidator()
    {
        RuleFor(x => x.AgentId)
            .NotEqual(Guid.Empty).WithMessage("AgentId is required");

        RuleFor(x => x.DocumentIds)
            .NotNull().WithMessage("DocumentIds cannot be null")
            .Must(ids => ids.Count <= MaxDocuments)
            .WithMessage($"Cannot select more than {MaxDocuments} documents")
            .Must(ids => ids.All(id => id != Guid.Empty))
            .WithMessage("DocumentIds cannot contain empty GUIDs")
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("DocumentIds cannot contain duplicates");
    }
}
