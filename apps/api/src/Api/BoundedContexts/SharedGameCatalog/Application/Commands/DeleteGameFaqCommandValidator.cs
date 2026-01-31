using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Validator for DeleteGameFaqCommand.
/// </summary>
internal sealed class DeleteGameFaqCommandValidator : AbstractValidator<DeleteGameFaqCommand>
{
    public DeleteGameFaqCommandValidator()
    {
        RuleFor(x => x.FaqId)
            .NotEqual(Guid.Empty).WithMessage("FaqId is required");
    }
}
