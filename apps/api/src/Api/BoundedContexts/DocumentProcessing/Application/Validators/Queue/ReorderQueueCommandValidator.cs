using Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;
using FluentValidation;

namespace Api.BoundedContexts.DocumentProcessing.Application.Validators.Queue;

/// <summary>
/// Validator for ReorderQueueCommand.
/// Issue #4731: Queue command validation.
/// </summary>
internal sealed class ReorderQueueCommandValidator : AbstractValidator<ReorderQueueCommand>
{
    public ReorderQueueCommandValidator()
    {
        RuleFor(x => x.OrderedJobIds)
            .NotNull()
            .WithMessage("Ordered job IDs list is required.")
            .Must(ids => ids.Count > 0)
            .WithMessage("Ordered job IDs list cannot be empty.")
            .Must(ids => ids.Count <= 100)
            .WithMessage("Cannot reorder more than 100 jobs at once.")
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("Ordered job IDs must not contain duplicates.");
    }
}
