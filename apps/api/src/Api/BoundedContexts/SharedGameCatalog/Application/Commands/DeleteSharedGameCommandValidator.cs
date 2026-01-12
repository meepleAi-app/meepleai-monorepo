using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

internal sealed class DeleteSharedGameCommandValidator : AbstractValidator<DeleteSharedGameCommand>
{
    public DeleteSharedGameCommandValidator()
    {
        RuleFor(x => x.GameId)
            .NotEmpty().WithMessage("GameId is required");

        RuleFor(x => x.DeletedBy)
            .NotEmpty().WithMessage("DeletedBy is required");
    }
}
