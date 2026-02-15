using Api.BoundedContexts.KnowledgeBase.Application.Commands.PlaygroundTestScenario;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators.PlaygroundTestScenario;

/// <summary>
/// Validator for CreatePlaygroundTestScenarioCommand.
/// Issue #4396: PlaygroundTestScenario Entity + CRUD
/// </summary>
internal sealed class CreatePlaygroundTestScenarioCommandValidator
    : AbstractValidator<CreatePlaygroundTestScenarioCommand>
{
    private static readonly string[] s_allowedRoles = { "user", "assistant", "system" };

    public CreatePlaygroundTestScenarioCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MinimumLength(3).WithMessage("Name must be at least 3 characters")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required")
            .MaximumLength(2000).WithMessage("Description must not exceed 2000 characters");

        RuleFor(x => x.Category)
            .IsInEnum().WithMessage("Invalid scenario category");

        RuleFor(x => x.Messages)
            .NotNull().WithMessage("Messages cannot be null")
            .Must(m => m != null && m.Count > 0).WithMessage("At least one message is required")
            .Must(m => m == null || m.Count <= 50).WithMessage("Cannot have more than 50 messages");

        RuleForEach(x => x.Messages)
            .ChildRules(msg =>
            {
                msg.RuleFor(m => m.Role)
                    .NotEmpty().WithMessage("Message role is required")
                    .Must(r => s_allowedRoles.Contains(r, StringComparer.OrdinalIgnoreCase))
                    .WithMessage("Message role must be one of: user, assistant, system");

                msg.RuleFor(m => m.Content)
                    .NotEmpty().WithMessage("Message content is required")
                    .MaximumLength(5000).WithMessage("Message content must not exceed 5000 characters");

                msg.RuleFor(m => m.DelayMs)
                    .InclusiveBetween(0, 30000)
                    .When(m => m.DelayMs.HasValue)
                    .WithMessage("Delay must be between 0 and 30000 milliseconds");
            });

        RuleFor(x => x.CreatedBy)
            .NotEqual(Guid.Empty).WithMessage("CreatedBy is required");

        RuleFor(x => x.ExpectedOutcome)
            .MaximumLength(2000).WithMessage("Expected outcome must not exceed 2000 characters")
            .When(x => x.ExpectedOutcome != null);

        RuleFor(x => x.Tags)
            .Must(t => t == null || t.Count <= 20).WithMessage("Cannot have more than 20 tags");

        RuleForEach(x => x.Tags)
            .MaximumLength(50).WithMessage("Tag must not exceed 50 characters")
            .When(x => x.Tags != null);
    }
}
