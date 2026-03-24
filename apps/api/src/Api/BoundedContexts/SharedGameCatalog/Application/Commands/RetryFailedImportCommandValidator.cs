using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Validator for RetryFailedImportCommand.
/// </summary>
internal sealed class RetryFailedImportCommandValidator : AbstractValidator<RetryFailedImportCommand>
{
    public RetryFailedImportCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Id is required");
    }
}
