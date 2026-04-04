using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for DeleteUserLlmDataCommand.
/// </summary>
internal sealed class DeleteUserLlmDataCommandValidator : AbstractValidator<DeleteUserLlmDataCommand>
{
    public DeleteUserLlmDataCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.RequestedByUserId)
            .NotEmpty()
            .WithMessage("RequestedByUserId is required");
    }
}
