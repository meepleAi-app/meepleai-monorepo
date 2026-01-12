using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Validator for DeleteGameErrataCommand.
/// </summary>
internal sealed class DeleteGameErrataCommandValidator : AbstractValidator<DeleteGameErrataCommand>
{
    public DeleteGameErrataCommandValidator()
    {
        RuleFor(x => x.ErrataId)
            .NotEqual(Guid.Empty).WithMessage("ErrataId is required");
    }
}
