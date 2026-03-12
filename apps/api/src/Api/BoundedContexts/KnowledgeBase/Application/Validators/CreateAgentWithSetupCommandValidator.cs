using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for CreateAgentWithSetupCommand.
/// Issue #4772: Agent Creation Orchestration Flow.
/// </summary>
internal sealed class CreateAgentWithSetupCommandValidator : AbstractValidator<CreateAgentWithSetupCommand>
{
    private const int MaxDocumentIds = 10;

    public CreateAgentWithSetupCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEqual(Guid.Empty).WithMessage("UserId is required");

        RuleFor(x => x.GameId)
            .NotEqual(Guid.Empty).WithMessage("GameId is required");

        RuleFor(x => x.AgentType)
            .NotEmpty().WithMessage("AgentType is required");

        RuleFor(x => x.UserTier)
            .NotEmpty().WithMessage("UserTier is required");

        RuleFor(x => x.UserRole)
            .NotEmpty().WithMessage("UserRole is required");

        // Optional: SharedGameId must not be Guid.Empty when provided
        RuleFor(x => x.SharedGameId)
            .NotEqual(Guid.Empty)
            .When(x => x.SharedGameId.HasValue)
            .WithMessage("SharedGameId must not be empty when provided");

        // Optional: DocumentIds validation when provided
        When(x => x.DocumentIds is { Count: > 0 }, () =>
        {
            RuleFor(x => x.DocumentIds!.Count)
                .LessThanOrEqualTo(MaxDocumentIds)
                .WithMessage($"Cannot attach more than {MaxDocumentIds} documents at once");

            RuleForEach(x => x.DocumentIds)
                .NotEqual(Guid.Empty)
                .WithMessage("Each DocumentId must not be empty");
        });
    }
}
