using Api.BoundedContexts.UserLibrary.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.UserLibrary.Application.Validators;

/// <summary>
/// Validator for RemovePrivatePdfCommand.
/// Issue #3651: Validates that required Guids are not empty.
/// </summary>
internal sealed class RemovePrivatePdfCommandValidator : AbstractValidator<RemovePrivatePdfCommand>
{
    public RemovePrivatePdfCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.EntryId)
            .NotEmpty()
            .WithMessage("EntryId is required");
    }
}
