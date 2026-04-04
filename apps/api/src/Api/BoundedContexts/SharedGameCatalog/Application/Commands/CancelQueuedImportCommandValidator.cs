using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Validator for CancelQueuedImportCommand.
/// </summary>
internal sealed class CancelQueuedImportCommandValidator : AbstractValidator<CancelQueuedImportCommand>
{
    public CancelQueuedImportCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Id is required");
    }
}
