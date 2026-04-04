using Api.BoundedContexts.EntityRelationships.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.EntityRelationships.Application.Validators;

internal sealed class ImportBggExpansionsCommandValidator : AbstractValidator<ImportBggExpansionsCommand>
{
    public ImportBggExpansionsCommandValidator()
    {
        RuleFor(x => x.SharedGameId)
            .NotEmpty()
            .WithMessage("SharedGameId is required.");

        RuleFor(x => x.AdminUserId)
            .NotEmpty()
            .WithMessage("AdminUserId is required.");
    }
}
