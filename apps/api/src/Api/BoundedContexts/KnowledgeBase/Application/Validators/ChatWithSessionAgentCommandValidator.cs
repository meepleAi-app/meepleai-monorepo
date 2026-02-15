using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for ChatWithSessionAgentCommand.
/// Issue #3184 (AGT-010): Session-Based Agent Lifecycle.
/// Issue #4386: SSE Stream → ChatThread Persistence Hook
/// </summary>
internal sealed class ChatWithSessionAgentCommandValidator : AbstractValidator<ChatWithSessionAgentCommand>
{
    private const int MinQueryLength = 3;
    private const int MaxQueryLength = 5000;

    public ChatWithSessionAgentCommandValidator()
    {
        RuleFor(x => x.AgentSessionId)
            .NotEqual(Guid.Empty).WithMessage("AgentSessionId is required");

        RuleFor(x => x.UserQuestion)
            .NotEmpty().WithMessage("UserQuestion is required")
            .MinimumLength(MinQueryLength).WithMessage($"UserQuestion must be at least {MinQueryLength} characters")
            .MaximumLength(MaxQueryLength).WithMessage($"UserQuestion cannot exceed {MaxQueryLength} characters");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required");

        RuleFor(x => x.ChatThreadId)
            .Must(id => !id.HasValue || id.Value != Guid.Empty)
            .WithMessage("ChatThreadId must not be an empty GUID");
    }
}
