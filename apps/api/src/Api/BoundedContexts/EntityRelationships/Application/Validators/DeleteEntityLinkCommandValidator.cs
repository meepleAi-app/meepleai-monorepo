using Api.BoundedContexts.EntityRelationships.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.EntityRelationships.Application.Validators;

internal sealed class DeleteEntityLinkCommandValidator : AbstractValidator<DeleteEntityLinkCommand>
{
    public DeleteEntityLinkCommandValidator()
    {
        RuleFor(x => x.EntityLinkId)
            .NotEmpty()
            .WithMessage("EntityLinkId is required.");

        RuleFor(x => x.RequestingUserId)
            .NotEmpty()
            .WithMessage("RequestingUserId is required.");
    }
}
