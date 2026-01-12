using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

internal sealed class PublishSharedGameCommandValidator : AbstractValidator<PublishSharedGameCommand>
{
    public PublishSharedGameCommandValidator()
    {
        RuleFor(x => x.GameId)
            .NotEmpty().WithMessage("GameId is required");

        RuleFor(x => x.PublishedBy)
            .NotEmpty().WithMessage("PublishedBy is required");
    }
}
