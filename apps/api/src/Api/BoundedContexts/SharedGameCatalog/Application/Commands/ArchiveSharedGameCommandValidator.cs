using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

internal sealed class ArchiveSharedGameCommandValidator : AbstractValidator<ArchiveSharedGameCommand>
{
    public ArchiveSharedGameCommandValidator()
    {
        RuleFor(x => x.GameId)
            .NotEmpty().WithMessage("GameId is required");

        RuleFor(x => x.ArchivedBy)
            .NotEmpty().WithMessage("ArchivedBy is required");
    }
}
