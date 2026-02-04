using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Validator for EnqueueBggBatchCommand
/// </summary>
internal class EnqueueBggBatchCommandValidator : AbstractValidator<EnqueueBggBatchCommand>
{
    public EnqueueBggBatchCommandValidator()
    {
        RuleFor(x => x.BggIds)
            .NotEmpty().WithMessage("At least one BGG ID must be provided")
            .Must(ids => ids != null && ids.Count > 0)
            .WithMessage("BGG IDs collection cannot be empty");

        RuleFor(x => x.BggIds)
            .Must(ids => ids != null && ids.All(id => id > 0))
            .WithMessage("All BGG IDs must be positive integers")
            .When(x => x.BggIds != null && x.BggIds.Count > 0);
    }
}
