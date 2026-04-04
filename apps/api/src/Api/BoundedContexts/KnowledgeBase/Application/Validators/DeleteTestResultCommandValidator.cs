using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for DeleteTestResultCommand.
/// </summary>
internal sealed class DeleteTestResultCommandValidator : AbstractValidator<DeleteTestResultCommand>
{
    public DeleteTestResultCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Id is required");

        RuleFor(x => x.RequestedBy)
            .NotEmpty()
            .WithMessage("RequestedBy is required");
    }
}
