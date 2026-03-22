using Api.BoundedContexts.GameManagement.Application.Commands.RuleConflictFAQs;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators.RuleConflictFAQs;

/// <summary>
/// Validator for DeleteRuleConflictFaqCommand.
/// Ensures Id is a non-empty GUID.
/// </summary>
internal sealed class DeleteRuleConflictFaqCommandValidator : AbstractValidator<DeleteRuleConflictFaqCommand>
{
    public DeleteRuleConflictFaqCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("FAQ ID is required");
    }
}

/// <summary>
/// Validator for RecordFaqUsageCommand.
/// Ensures Id is a non-empty GUID.
/// </summary>
internal sealed class RecordFaqUsageCommandValidator : AbstractValidator<RecordFaqUsageCommand>
{
    public RecordFaqUsageCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("FAQ ID is required");
    }
}
