using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for the Unified Agent Query command.
/// Issue #4338: Unified API Gateway
/// </summary>
internal sealed class UnifiedAgentQueryCommandValidator : AbstractValidator<UnifiedAgentQueryCommand>
{
    public UnifiedAgentQueryCommandValidator()
    {
        RuleFor(x => x.Query)
            .NotEmpty().WithMessage("Query cannot be empty")
            .MaximumLength(2000).WithMessage("Query must be 2000 characters or less");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required");

        RuleFor(x => x.ChatThreadId)
            .Must(id => !id.HasValue || id.Value != Guid.Empty)
            .WithMessage("ChatThreadId must not be an empty GUID");

        RuleFor(x => x.PreferredAgentId)
            .Must(id => !id.HasValue || id.Value != Guid.Empty)
            .WithMessage("PreferredAgentId must not be an empty GUID");
    }
}
